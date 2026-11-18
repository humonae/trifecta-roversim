using System;
using System.Collections.Generic;
using System.Globalization;
using Rocks;
using Rover;
using Training;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace Evaluation
{
    // Runs a trained model for the Evaluation panel's number of episodes and reports success rate,
    // time to goal, timeouts, collisions and why episodes failed.
    //
    // Mirrors TrainingOrganizer: the same tiles (LoadMap), rocks (RockPlacer), arenas (Epoch) and
    // spawn/goal picking (EpisodeLayoutPicker), but with no Python: the model runs inside Unity.
    // The episodes are run in layouts of EvaluationDefaults.EpisodesPerLayout: each layout is one
    // tile (from the "Test on" choice) with freshly placed rocks.
    public class EvaluationOrganizer : MonoBehaviour, ISimulationRun
    {
        [Header("Scene")]
        [Tooltip("Optional. Found automatically if empty.")]
        [SerializeField] private LoadMap loadMap;
        [Tooltip("Optional. Found automatically if empty.")]
        [SerializeField] private RockPlacer rockPlacer;
        [Tooltip("Optional when Match Training is on: the TrainingOrganizer's rover prefab is used.")]
        [SerializeField] private GameObject roverPrefab;
        [Tooltip("Optional. Found automatically if empty. Lets the Play button start an evaluation.")]
        [SerializeField] private SimulationManager simulationManager;
        [Tooltip("Parent for the spawned instances. Defaults to this object.")]
        [SerializeField] private Transform instancesRoot;

        [Header("Match training")]
        [Tooltip("Optional. Found automatically if empty.")]
        [SerializeField] private TrainingOrganizer trainingOrganizer;
        [Tooltip("Optional. Found automatically if empty.")]
        [SerializeField] private TrainingSettings trainingSettings;
        [Tooltip("Use training's arena, episode rules, episode time limit and obstacle scale, so the model is tested under the conditions it trained in.")]
        [SerializeField] private bool matchTraining = true;

        [Header("Used when Match Training is off")]
        [SerializeField] private ArenaSettings arena = new ArenaSettings();
        [Tooltip("Only the goal radius, tilt limit and stall settings matter here.")]
        [SerializeField] private EpisodeRules rules = new EpisodeRules();

        [Header("Other")]
        [Tooltip("End an episode early, as a timeout, when the rover stops getting closer to the goal (the rules' Stall Seconds and Stall Progress Meters). Saves time on long episodes. Off runs every episode to the full time limit, like evaluations before this setting existed.")]
        [SerializeField] private bool endStalledEpisodes = true;
        [Tooltip("Optional. Used when no model was picked in the UI.")]
        [SerializeField] private ModelAsset fallbackModel;
        [SerializeField] private bool logEpisodes = true;

        private readonly List<EvaluationInstance> instances = new List<EvaluationInstance>();
        private readonly List<string> tileOrder = new List<string>();
        private EvaluationSettings uiSettings;
        private EvaluationRequest request;
        private EvaluationReport report;
        private EvaluationReport previousRun;
        private TileProvider tiles;
        private Epoch epoch;
        private System.Random rng;
        private ArenaSettings activeArena;
        private EpisodeRules activeRules;
        private GameObject activePrefab;
        private ModelAsset model;
        private bool modelImported;
        private int layoutIndex;
        private int episodesAssigned;
        private string pendingFailure;
        private float requestedSpeed = -1f;
        private float runStartTime;
        private float originalTimeScale = 1f;
        private bool originalRunInBackground;

        public event Action<TrainingState> StateChanged;
        public event Action<string> StatusChanged;
        public event Action<float> SpeedChanged;
        // A run has started; the report is empty but has its run number, selection and settings.
        public event Action<EvaluationReport> RunStarted;
        // An episode finished; the report already includes it.
        public event Action<AttemptResult, EvaluationReport> EpisodeFinished;
        // The run ended (finished or stopped). The path is the saved JSON, or null.
        public event Action<EvaluationReport, string> Finished;

        public TrainingState State { get; private set; }
        public string Status { get; private set; } = "Ready";
        public EvaluationRequest Request => request;
        // The current run's report, updated after every episode. Null before the first run.
        public EvaluationReport Report => report;
        public EvaluationReport LastReport { get; private set; }
        public string LastReportPath { get; private set; }
        public IReadOnlyList<EvaluationInstance> Instances => instances;
        public bool CanStartFromPlay => simulationManager != null;
        public float Speed => requestedSpeed > 0f ? requestedSpeed : 1f;
        public bool IsEvaluating => State == TrainingState.Running || State == TrainingState.Paused;
        public float SimulatedSeconds => IsEvaluating ? Time.time - runStartTime : 0f;

        public string ProgressDetail
        {
            get
            {
                if (!IsEvaluating || instances.Count == 0 || request == null)
                    return null;

                EpisodeTimer timer = instances[0].Timer;
                return $"Episode {Math.Min(report.AttemptLog.Count + 1, request.Episodes)}/{request.Episodes}  |  {timer.Elapsed:0}/{timer.Limit:0} s";
            }
        }

        private void Awake()
        {
            if (loadMap == null)
                loadMap = FindAnyObjectByType<LoadMap>();
            if (rockPlacer == null)
                rockPlacer = FindAnyObjectByType<RockPlacer>();
            if (trainingOrganizer == null)
                trainingOrganizer = FindAnyObjectByType<TrainingOrganizer>();
            if (trainingSettings == null)
                trainingSettings = FindAnyObjectByType<TrainingSettings>();
            if (simulationManager == null)
                simulationManager = FindAnyObjectByType<SimulationManager>();
        }

        public bool StartEvaluation(EvaluationSettings settings)
        {
            if (State != TrainingState.Idle)
            {
                Debug.LogWarning($"[Evaluation] Can't start an evaluation while {State}.", this);
                return false;
            }

            if (settings == null)
            {
                ReportFailure("No EvaluationSettings were passed to StartEvaluation.");
                return false;
            }

            if (trainingOrganizer != null && trainingOrganizer.State != TrainingState.Idle)
            {
                ReportFailure("Stop training before running an evaluation.");
                return false;
            }

            uiSettings = settings;
            bool match = matchTraining && trainingOrganizer != null;
            activeArena = match ? trainingOrganizer.Arena : arena;
            activeRules = match ? trainingOrganizer.Rules : rules;
            activePrefab = roverPrefab != null ? roverPrefab : trainingOrganizer != null ? trainingOrganizer.RoverPrefab : null;

            tiles = new TileProvider(loadMap);
            request = EvaluationRequestBuilder.Build(settings, match ? trainingSettings : null, tiles.MapName);

            string error;
            if (!ValidateScene(out error) || !ValidatePickedPositions(out error) || !PlanTiles(out error) || !LoadModel(out error))
            {
                ReportFailure(error);
                return false;
            }

            if (!request.RandomPosition)
                request.Selection += " (picked start and goal)";

            Debug.Log($"[Evaluation] Starting evaluation with:\n{request.ToLogString()}", this);

            previousRun = EvaluationHistory.Latest();
            report = EvaluationReport.Create(request, EvaluationHistory.NextRunNumber(), previousRun, DateTime.Now);
            epoch = new Epoch(tiles, rockPlacer, activeArena, new System.Random(rng.Next()));
            if (!request.RandomPosition)
                UsePickedPositions(epoch);
            layoutIndex = -1;
            episodesAssigned = 0;
            pendingFailure = null;

            originalTimeScale = Time.timeScale;
            originalRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            Application.logMessageReceived += WatchForInferenceErrors;

            // No trainer: dispose any Academy left from training and stop the next one from looking for Python.
            AcademyConnection.Disconnect();
            CommunicatorFactory.Enabled = false;

            SetState(TrainingState.Preparing, "Setting up the first layout...");
            if (RunStarted != null)
                RunStarted.Invoke(report);

            if (!BeginNextLayout(out error))
            {
                EndRun(false, error, true);
                return false;
            }

            runStartTime = Time.time;
            Time.timeScale = Speed;
            SetState(TrainingState.Running, RunningStatus());
            return true;
        }

        public void Play()
        {
            if (State == TrainingState.Paused)
                Resume();
            else if (State == TrainingState.Idle && simulationManager != null)
                simulationManager.StartEvaluation();
        }

        public void Pause()
        {
            if (State != TrainingState.Running)
                return;

            Time.timeScale = 0f;
            SetState(TrainingState.Paused, "Paused");
        }

        public void Resume()
        {
            if (State != TrainingState.Paused)
                return;

            Time.timeScale = Speed;
            SetState(TrainingState.Running, RunningStatus());
        }

        public void Stop()
        {
            if (State != TrainingState.Idle)
                EndRun(false, "Evaluation stopped.", false);
        }

        public void SetTimeScale(float speed)
        {
            requestedSpeed = SpeedLimits.Clamp(speed);
            if (State == TrainingState.Running)
                Time.timeScale = requestedSpeed;

            if (SpeedChanged != null)
                SpeedChanged.Invoke(requestedSpeed);
        }

        public void SetTimeScale(string text)
        {
            float value;
            if ((float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
                 float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) && value > 0f)
                SetTimeScale(value);
        }

        public void SpeedUp()
        {
            SetTimeScale(SpeedLimits.Faster(Speed));
        }

        public void SlowDown()
        {
            SetTimeScale(SpeedLimits.Slower(Speed));
        }

        private void Update()
        {
            if (pendingFailure != null && State != TrainingState.Idle)
            {
                string failure = pendingFailure;
                pendingFailure = null;
                EndRun(false, failure, true);
            }
        }

        private void FixedUpdate()
        {
            if (State != TrainingState.Running || instances.Count == 0)
                return;

            foreach (EvaluationInstance instance in instances)
            {
                if (!instance.FinishedLayout)
                    return;
            }

            if (episodesAssigned >= request.Episodes)
            {
                EndRun(true, $"Finished all {request.Episodes} episodes.", false);
                return;
            }

            string error;
            if (!BeginNextLayout(out error))
                EndRun(false, error, true);
            else
                SetStatus(RunningStatus());
        }

        // Works out which tiles "Test on" means and in what order they are visited.
        private bool PlanTiles(out string error)
        {
            rng = new System.Random(request.Seed);
            tileOrder.Clear();

            if (request.TestOn == EvaluationTiles.SelectedTile)
            {
                GameObject current = tiles.CurrentTile();
                tileOrder.Add(null);
                request.Selection = $"Selected Tile: {DescribeTile(current)}";
                error = null;
                return true;
            }

            if (string.IsNullOrEmpty(tiles.MapName))
            {
                error = "Open a map first, so there are tiles to test on.";
                return false;
            }

            List<string> labels = tiles.TileLabels();
            labels.Sort(StringComparer.Ordinal);

            if (request.TestOn == EvaluationTiles.HeldOutTiles)
            {
                TrainingRunInfo info;
                if (!TrainingRunInfo.TryLoad(request.ModelPath, out info))
                {
                    error = $"There's no training record ({System.IO.Path.GetFileName(TrainingRunInfo.PathFor(request.ModelPath ?? "model.onnx"))}) next to the model, " +
                            "so it isn't known which tiles it trained on. Models trained before this record existed can use Selected Tile or All Tiles.";
                    return false;
                }

                if (info.Map == tiles.MapName)
                    labels.RemoveAll(label => tiles.TileFor(label) != null && info.Tiles.Contains(tiles.TileFor(label).name));
                else
                    Debug.Log($"[Evaluation] The model trained on {info.Map}, so every {tiles.MapName} tile is held out.", this);

                if (labels.Count == 0)
                {
                    error = $"The model trained on every {tiles.MapName} tile, so there are no held-out tiles.";
                    return false;
                }
            }

            if (labels.Count == 0)
            {
                error = $"{tiles.MapName} has no tiles.";
                return false;
            }

            Shuffle(labels, rng);
            tileOrder.AddRange(labels);
            string kind = request.TestOn == EvaluationTiles.HeldOutTiles ? "Held-out Tiles" : "All Tiles";
            request.Selection = $"{kind}: {tiles.MapName} ({labels.Count})";
            error = null;
            return true;
        }

        private bool BeginNextLayout(out string error)
        {
            layoutIndex++;

            string label = tileOrder[layoutIndex % tileOrder.Count];
            Func<GameObject> chooseTile = label == null ? (Func<GameObject>)tiles.CurrentTile : () => tiles.LoadTile(label);
            if (!epoch.Begin(request.ToEpochRequest(), request.RoverCount, chooseTile, out error))
                return false;

            if (instances.Count == 0)
            {
                for (int i = 0; i < request.RoverCount; i++)
                {
                    if (!CreateInstance(i, out error))
                        return false;
                }
            }

            // Split this layout's episodes between the rovers; the last layout may be short.
            int episodes = Math.Min(request.EpisodesPerLayout, request.Episodes - episodesAssigned);
            episodesAssigned += episodes;
            for (int i = 0; i < instances.Count; i++)
            {
                int share = episodes / instances.Count + (i < episodes % instances.Count ? 1 : 0);
                instances[i].BeginLayout(layoutIndex, share);
            }

            if (!request.RandomPosition && !PickedPositionsOnTile(out error))
                return false;

            Debug.Log($"[Evaluation] Layout {layoutIndex + 1} on '{epoch.Tile.name}': {episodes} episode(s).", this);
            error = null;
            return true;
        }

        private bool CreateInstance(int index, out string error)
        {
            var instanceObject = new GameObject($"Evaluation Instance {index}");
            instanceObject.transform.SetParent(instancesRoot != null ? instancesRoot : transform, false);

            var instance = instanceObject.AddComponent<EvaluationInstance>();
            instance.AttemptFinished += OnEpisodeFinished;
            instances.Add(instance);

            EvaluationDefaults defaults = uiSettings.defaults ?? new EvaluationDefaults();
            var setup = new EvaluationInstanceSetup
            {
                Index = index,
                RoverPrefab = activePrefab,
                Model = model,
                InferenceDevice = defaults.InferenceDevice,
                DeterministicInference = request.DeterministicInference,
                Epoch = epoch,
                Request = request,
                Arena = activeArena,
                Rules = activeRules,
                EndStalledEpisodes = endStalledEpisodes,
                Seed = unchecked(request.Seed * 31 + index + 1),
                FixedStart = request.RandomPosition ? (Vector3?)null : request.StartPos,
                FixedGoal = request.RandomPosition ? (Vector3?)null : request.GoalPos
            };

            return instance.Initialize(setup, out error);
        }

        private void OnEpisodeFinished(EvaluationInstance instance, AttemptResult result)
        {
            result.Episode = report.AttemptLog.Count;
            report.Record(result, previousRun);

            if (logEpisodes)
            {
                string outcome = result.Success ? $"reached the goal in {result.Seconds:0.0} s" : $"{result.Outcome} after {result.Seconds:0.0} s";
                Debug.Log($"[Evaluation] Episode {result.Episode + 1}/{request.Episodes} on {result.Tile}, rover {result.Rover}: {outcome}" +
                          (result.HitRock ? ", hit a rock" : "") +
                          $". Success so far {EvaluationReport.Percent(report.SuccessRate)}.", this);
            }

            if (EpisodeFinished != null)
                EpisodeFinished.Invoke(result, report);

            if (State == TrainingState.Running)
                SetStatus(RunningStatus());
        }

        private void EndRun(bool completed, string reason, bool isError)
        {
            if (State == TrainingState.Idle)
                return;

            Application.logMessageReceived -= WatchForInferenceErrors;
            RestoreTime();
            DestroyInstances();
            if (epoch != null)
                epoch.End();

            if (Academy.IsInitialized)
                Academy.Instance.Dispose();
            CommunicatorFactory.Enabled = true;
            ReleaseModel();

            string message = reason;
            if (report != null && report.AttemptLog.Count > 0)
            {
                report.Finish(completed, completed ? "" : reason, DateTime.Now, previousRun);
                EvaluationDefaults defaults = uiSettings.defaults ?? new EvaluationDefaults();
                LastReportPath = EvaluationHistory.Save(report, defaults.ReportFolder);
                LastReport = report;

                string saved = LastReportPath != null ? $"Saved to {LastReportPath}" : "The run could not be saved; see the warnings above.";
                Debug.Log($"[Evaluation] {report.Summary()}\n{report.TileTable()}\n{saved}", this);

                if (!isError)
                {
                    message = $"Run {report.RunNumber} {(completed ? "finished" : "stopped")}: success {EvaluationReport.Percent(report.SuccessRate)} " +
                              $"({report.Successes}/{report.Episodes}), time to goal {EvaluationReport.Seconds(report.AverageSecondsToGoal)}";
                }

                if (Finished != null)
                    Finished.Invoke(report, LastReportPath);
            }

            if (isError)
                Debug.LogError($"[Evaluation] {reason}", this);

            SetState(TrainingState.Idle, message);
        }

        private bool ValidateScene(out string error)
        {
            error = null;

            if (activePrefab == null)
                error = "No rover prefab is assigned on the EvaluationOrganizer or the TrainingOrganizer.";
            else if (activePrefab.GetComponentInChildren<RoverAIController>(true) == null)
                error = $"'{activePrefab.name}' has no {nameof(RoverAIController)}. Use an AI controlled rover prefab.";
            else if (activePrefab.GetComponentInChildren<BehaviorParameters>(true) == null)
                error = $"'{activePrefab.name}' has no Behavior Parameters.";
            else if (request.TestOn == EvaluationTiles.SelectedTile && tiles.CurrentTile() == null)
                error = "No tile is loaded. Pick a map and tile under Test on before running an evaluation.";

            if (error == null && request.RocksEnabled && request.RockDensity > 0f && rockPlacer == null)
                Debug.LogWarning("[Evaluation] Obstacle density is set but no RockPlacer was found; evaluating without rocks.", this);

            return error == null;
        }

        // Start and Goal: Set on Preview. The points belong to the tile they were picked on, and several
        // rovers would all spawn on the same spot, so this runs one rover on the Selected Tile.
        private bool ValidatePickedPositions(out string error)
        {
            error = null;
            if (request.RandomPosition)
                return true;

            if (request.TestOn != EvaluationTiles.SelectedTile)
                error = "A picked start and goal only work with Test on: Selected Tile, because the points belong to that tile.";
            else if (HorizontalDistance(request.StartPos, request.GoalPos) < 1f)
                error = "Pick both a start and a goal on the preview (at least 1 m apart), or set Start and Goal to Random.";

            if (error == null && request.RoverCount > 1)
            {
                Debug.Log("[Evaluation] A picked start and goal use one rover.", this);
                request.RoverCount = 1;
            }

            return error == null;
        }

        // One arena around the route instead of randomly placed ones, big enough that the rover can
        // detour without leaving it, and rocks kept off the start and goal.
        private void UsePickedPositions(Epoch target)
        {
            Vector3 start = request.StartPos;
            Vector3 goal = request.GoalPos;
            float side = Mathf.Max(activeArena.Size, HorizontalDistance(start, goal) + 2f * (activeArena.EdgeMargin + 20f));
            var centre = new Vector2((start.x + goal.x) * 0.5f, (start.z + goal.z) * 0.5f);

            target.FixedArena = new Rect(centre.x - side * 0.5f, centre.y - side * 0.5f, side, side);
            target.KeepClear.Add(new Epoch.ClearSpot { Position = start, Radius = activeArena.SpawnClearRadius });
            target.KeepClear.Add(new Epoch.ClearSpot { Position = goal, Radius = activeArena.GoalClearRadius });
        }

        private bool PickedPositionsOnTile(out string error)
        {
            Vector3 ground, normal;
            bool onTile = epoch.Surface.TrySample(new Vector2(request.StartPos.x, request.StartPos.z), out ground, out normal) &&
                          epoch.Surface.TrySample(new Vector2(request.GoalPos.x, request.GoalPos.z), out ground, out normal);
            error = onTile ? null : $"The picked start or goal isn't on '{epoch.Tile.name}'. Pick them again on the loaded tile.";
            return onTile;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return new Vector2(a.x - b.x, a.z - b.z).magnitude;
        }

        private bool LoadModel(out string error)
        {
            if (!EvaluationModelLoader.TryLoad(request.ModelPath, fallbackModel, out model, out modelImported, out error))
                return false;

            if (!EvaluationModelLoader.Validate(model, out error))
            {
                ReleaseModel();
                return false;
            }

            if (string.IsNullOrEmpty(request.ModelName))
                request.ModelName = model.name;
            return true;
        }

        private void ReleaseModel()
        {
            if (modelImported && model != null)
                EvaluationModelLoader.Release(model);

            model = null;
            modelImported = false;
        }

        // An incompatible model (wrong observation or action sizes) throws on every decision. Stop instead of
        // recording a run of rovers that never move.
        private void WatchForInferenceErrors(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception || pendingFailure != null || State == TrainingState.Idle)
                return;

            if (condition.Contains("UnityAgentsException") || condition.Contains("InferenceEngine") ||
                (stackTrace != null && (stackTrace.Contains("Unity.MLAgents.Inference") || stackTrace.Contains("SentisPolicy"))))
            {
                pendingFailure = $"The model failed while running, so it probably doesn't match this rover's observations or actions: {condition}";
            }
        }

        private void DestroyInstances()
        {
            foreach (EvaluationInstance instance in instances)
            {
                if (instance == null)
                    continue;

                instance.AttemptFinished -= OnEpisodeFinished;
                instance.Abort();
                instance.Teardown();
                Destroy(instance.gameObject);
            }

            instances.Clear();
        }

        private void RestoreTime()
        {
            Time.timeScale = originalTimeScale;
            Application.runInBackground = originalRunInBackground;
        }

        private string RunningStatus()
        {
            string tile = epoch != null && epoch.Tile != null ? epoch.Tile.name : "?";
            int done = report.Episodes + report.Skipped;
            return $"Evaluating {request.ModelName} on {tile}: {done}/{request.Episodes} episodes, success {EvaluationReport.Percent(report.SuccessRate)}";
        }

        // "Jezero C6" from a tile named "Jezero_C6".
        private string DescribeTile(GameObject tile)
        {
            if (tile == null)
                return "?";

            string name = tile.name;
            int split = name.LastIndexOf('_');
            string label = split >= 0 ? name.Substring(split + 1) : name;
            return string.IsNullOrEmpty(tiles.MapName) ? name : $"{tiles.MapName} {label}";
        }

        private static void Shuffle(List<string> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                string swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
        }

        private void ReportFailure(string error)
        {
            Debug.LogError($"[Evaluation] {error}", this);
            SetStatus(error);
        }

        private void SetState(TrainingState state, string status)
        {
            Status = status;
            if (State != state)
            {
                State = state;
                if (StateChanged != null)
                    StateChanged.Invoke(state);
            }

            if (StatusChanged != null)
                StatusChanged.Invoke(status);
        }

        private void SetStatus(string status)
        {
            Status = status;
            if (StatusChanged != null)
                StatusChanged.Invoke(status);
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= WatchForInferenceErrors;
            if (State == TrainingState.Idle)
                return;

            RestoreTime();
            DestroyInstances();
            if (epoch != null)
                epoch.End();
            CommunicatorFactory.Enabled = true;
            ReleaseModel();
        }
    }
}
