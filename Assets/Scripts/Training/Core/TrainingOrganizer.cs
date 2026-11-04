using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Rocks;
using Rover;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace Training
{
    public class TrainingOrganizer : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private LoadMap loadMap;
        [SerializeField] private RockPlacer rockPlacer;
        [SerializeField] private GameObject roverPrefab;
        [Tooltip("Optional. Lets the Play button start a run with the panel's current settings.")]
        [SerializeField] private SimulationManager simulationManager;
        [Tooltip("Optional. Where the checkpoint folder and .onnx file name are read from.")]
        [SerializeField] private ONNXFilePicker outputPicker;
        [Tooltip("Parent for the spawned instances. Defaults to this object.")]
        [SerializeField] private Transform instancesRoot;

        [Header("Training")]
        [Tooltip("Rovers trained side by side on the same tile, each in its own arena.")]
        [SerializeField, Min(1)] private int instanceCount = 1;
        [SerializeField] private TrainingDefaults defaults = new TrainingDefaults();
        [SerializeField] private ArenaSettings arena = new ArenaSettings();
        [SerializeField] private EpisodeRules rules = new EpisodeRules();
        [SerializeField] private EarlyStoppingSettings earlyStopping = new EarlyStoppingSettings();
        [SerializeField] private bool logEpisodes = true;

        [Header("Python")]
        [SerializeField] private PythonSetupSettings python = new PythonSetupSettings();
        [SerializeField] private TrainerLaunchSettings trainer = new TrainerLaunchSettings();
        [SerializeField] private TrainerConfig trainerConfig = new TrainerConfig();

        private readonly List<TrainingInstance> instances = new List<TrainingInstance>();
        private readonly TrainerSession session = new TrainerSession();
        private readonly PythonEnvironment pythonEnvironment = new PythonEnvironment();
        private Coroutine setupRoutine;
        private string configPath;
        private int decisionPeriod = 1;
        private TrainingRequest request;
        private TrainingStats stats;
        private Epoch epoch;
        private string behaviorName;
        private string stopReason;
        private bool stopIsError;
        private bool stopWasConnected;
        private float requestedSpeed = -1f;
        private bool speedApplied;
        private float runStartTime;
        private float originalTimeScale = 1f;
        private int originalCaptureFramerate;
        private bool originalRunInBackground;

        public event Action<TrainingState> StateChanged;
        public event Action<string> StatusChanged;
        public event Action<float> SpeedChanged;

        public TrainingState State { get; private set; }
        public string Status { get; private set; } = "Ready";
        public TrainingRequest Request => request;
        public TrainingStats Stats => stats;
        public Epoch CurrentEpoch => epoch;
        public IReadOnlyList<TrainingInstance> Instances => instances;
        public bool CanStartFromPlay => simulationManager != null;
        public float Speed => requestedSpeed > 0f ? requestedSpeed : trainer.TimeScale;
        public bool IsTraining => State == TrainingState.Running || State == TrainingState.Paused;
        public float SimulatedSeconds => IsTraining ? Time.time - runStartTime : 0f;

        public bool StartTraining(SimulationSettings uiSettings)
        {
            if (State != TrainingState.Idle)
            {
                Debug.LogWarning($"[Training] Can't start training while {State}.", this);
                return false;
            }

            if (uiSettings == null)
            {
                ReportFailure("No settings were passed to StartTraining.");
                return false;
            }

            Debug.Log($"[Training] UI settings:\n{JsonUtility.ToJson(uiSettings, true)}", this);
            request = TrainingRequestBuilder.Build(uiSettings, outputPicker, defaults, trainerConfig);
            WarnAboutUnsupported(uiSettings);

            string error;
            if (!Validate(out error))
            {
                ReportFailure(error);
                return false;
            }

            request.MaxTrainerSteps = TrainerStepBudget.Estimate(request, instanceCount, decisionPeriod, Time.fixedDeltaTime);
            Debug.Log($"[Training] Starting training with:\n{request.ToLogString()}", this);

            if (!TrainerConfigWriter.TryWrite(request, behaviorName, trainerConfig, out configPath, out error))
            {
                ReportFailure(error);
                return false;
            }

            stats = new TrainingStats(earlyStopping.Window);
            originalTimeScale = Time.timeScale;
            originalCaptureFramerate = Time.captureFramerate;
            originalRunInBackground = Application.runInBackground;
            Application.runInBackground = true;

            SetState(TrainingState.SettingUpPython, "Checking Python...");
            setupRoutine = StartCoroutine(SetUpPythonAndLaunch());
            return true;
        }

        public void Play()
        {
            if (State == TrainingState.Paused)
                Resume();
            else if (State == TrainingState.Idle && simulationManager != null)
                simulationManager.StartSimulation();
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
            speedApplied = true;
            SetState(TrainingState.Running, RunningStatus());
        }

        public void Stop()
        {
            BeginStop("Training stopped.", false);
        }

        public void SetTimeScale(float speed)
        {
            requestedSpeed = SpeedLimits.Clamp(speed);
            trainer.TimeScale = Mathf.Max(SpeedLimits.MinAtLaunch, requestedSpeed);

            if (State == TrainingState.Running && speedApplied)
                Time.timeScale = requestedSpeed;

            if (SpeedChanged != null)
                SpeedChanged.Invoke(requestedSpeed);
        }

        public void SetTimeScale(string text)
        {
            float value;
            if (TryParsePositive(text, out value))
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

        public void SetEpochMinutes(string text)
        {
            float value;
            if (TryParsePositive(text, out value))
                defaults.EpochMinutes = value;
        }

        public void SetEpisodeSeconds(string text)
        {
            float value;
            if (TryParsePositive(text, out value))
                defaults.EpisodeSeconds = value;
        }

        private IEnumerator SetUpPythonAndLaunch()
        {
            yield return pythonEnvironment.Ensure(python);
            setupRoutine = null;

            if (State != TrainingState.SettingUpPython)
                yield break;

            if (!pythonEnvironment.IsReady || !session.BeginLaunch(trainer, request, configPath, pythonEnvironment.Python))
            {
                RestoreTime();
                ReportFailure(pythonEnvironment.IsReady ? session.Error : pythonEnvironment.Error);
                SetState(TrainingState.Idle, Status);
                yield break;
            }

            SetState(TrainingState.StartingTrainer, "Starting the Python trainer...");
        }

        private void Update()
        {
            session.Update();

            switch (State)
            {
                case TrainingState.SettingUpPython:
                    if (pythonEnvironment.Status != Status && !string.IsNullOrEmpty(pythonEnvironment.Status))
                        SetStatus(pythonEnvironment.Status);
                    break;

                case TrainingState.StartingTrainer:
                    if (!string.IsNullOrEmpty(session.Progress) && session.Progress != Status)
                        SetStatus(session.Progress);
                    if (session.IsReady)
                        BeginRun();
                    else if (session.IsClosed)
                        FinishFailedLaunch();
                    break;

                case TrainingState.Running:
                    ApplySpeedOnceTrainerStarted();
                    CheckRunEnded();
                    break;

                case TrainingState.Paused:
                    CheckRunEnded();
                    break;

                case TrainingState.Stopping:
                    if (session.IsClosed)
                        FinishStop();
                    break;
            }
        }

        private void FixedUpdate()
        {
            if (State != TrainingState.Running)
                return;

            epoch.Advance(Time.fixedDeltaTime);
            if (epoch.Timer.Expired)
                AdvanceEpoch();
        }

        private void BeginRun()
        {
            SetState(TrainingState.Preparing, "Setting up the map...");

            int seed = request.UseFixedSeed ? request.Seed : Environment.TickCount;
            epoch = new Epoch(new TileProvider(loadMap), rockPlacer, arena, new System.Random(seed));

            string error;
            if (!epoch.Begin(request, instanceCount, false, out error))
            {
                BeginStop(error, true);
                return;
            }

            request.StartTile = epoch.Tile.name;

            if (!session.Connect())
            {
                BeginStop(session.Error, true);
                return;
            }

            for (int i = 0; i < instanceCount; i++)
            {
                if (!CreateInstance(i, seed, out error))
                {
                    BeginStop(error, true);
                    return;
                }
            }

            Debug.Log($"[Training] Run '{session.RunId}' started on tile '{epoch.Tile.name}' with {instances.Count} rover(s).", this);
            speedApplied = false;
            runStartTime = Time.time;
            SetState(TrainingState.Running, RunningStatus());
        }

        private bool CreateInstance(int index, int baseSeed, out string error)
        {
            var instanceObject = new GameObject($"Training Instance {index}");
            instanceObject.transform.SetParent(instancesRoot != null ? instancesRoot : transform, false);

            var instance = instanceObject.AddComponent<TrainingInstance>();
            instance.EpisodeEnded += OnEpisodeEnded;
            instances.Add(instance);

            var setup = new InstanceSetup
            {
                Index = index,
                RoverPrefab = roverPrefab,
                Epoch = epoch,
                Request = request,
                Arena = arena,
                Rules = rules,
                Seed = unchecked(baseSeed * 31 + index + 1)
            };

            return instance.Initialize(setup, out error);
        }

        private void AdvanceEpoch()
        {
            stats.RecordEpoch();
            Debug.Log($"[Training] Epoch {epoch.Number} on '{epoch.Tile.name}' finished.", this);

            if (request.IterationUnit == IterationUnit.Epochs && stats.CompletedEpochs >= request.MaxIterations)
            {
                BeginStop($"Reached {request.MaxIterations} epochs.", false);
                return;
            }

            string error;
            if (!epoch.Begin(request, instanceCount, request.NewTileEachEpoch, out error))
            {
                BeginStop(error, true);
                return;
            }

            foreach (TrainingInstance instance in instances)
                instance.RestartEpisode();

            Debug.Log($"[Training] Epoch {epoch.Number} started on '{epoch.Tile.name}'.", this);
            SetStatus(RunningStatus());
        }

        private void OnEpisodeEnded(TrainingInstance instance, EpisodeOutcome outcome)
        {
            stats.RecordEpisode(outcome, instance.Timer.Elapsed);

            if (logEpisodes)
            {
                Debug.Log($"[Training] Rover {instance.Index}, episode {stats.Episodes}: {outcome} after {instance.Timer.Elapsed:0.0} s, " +
                          $"reward {instance.Rover.GetCumulativeReward():0.00}. Recent success rate {stats.RecentSuccessRate:P0}.", this);
            }

            if (State == TrainingState.Running)
                SetStatus(RunningStatus());
        }

        private void ApplySpeedOnceTrainerStarted()
        {
            if (speedApplied || !Academy.IsInitialized || Academy.Instance.TotalStepCount == 0)
                return;

            Time.timeScale = Speed;
            speedApplied = true;
        }

        private void CheckRunEnded()
        {
            string reason;
            if (session.QuitReceived)
                BeginStop($"The trainer reached its {request.MaxTrainerSteps:N0}-step budget.", false);
            else if (session.ProcessExited)
                BeginStop($"The Python trainer exited unexpectedly with code {session.ExitCode}. Last output:\n{session.RecentOutput()}", true);
            else if (request.IterationUnit == IterationUnit.Episodes && stats.Episodes >= request.MaxIterations)
                BeginStop($"Reached {request.MaxIterations} episodes.", false);
            else if (request.EarlyStopping && EarlyStopping.ShouldStop(stats, earlyStopping, out reason))
                BeginStop(reason, false);
        }

        private void BeginStop(string reason, bool isError)
        {
            if (State == TrainingState.Idle || State == TrainingState.Stopping)
                return;

            if (State == TrainingState.SettingUpPython)
            {
                CancelPythonSetup();
                RestoreTime();
                SetState(TrainingState.Idle, reason);
                return;
            }

            stopReason = reason;
            stopIsError = isError;
            stopWasConnected = session.IsConnected;

            RestoreTime();
            session.BeginClose();
            DeactivateInstances();
            DestroyInstances();
            if (epoch != null)
                epoch.End();

            SetState(TrainingState.Stopping, stopWasConnected ? "Saving the model..." : "Stopping...");
        }

        private void FinishStop()
        {
            string message = stopReason;
            string model = stopWasConnected && session.RunId == request.RunId
                ? ModelExporter.FindModel(session.RunDirectory, behaviorName)
                : null;

            if (model != null)
            {
                string copy = ModelExporter.CopyTo(model, request.ModelOutputPath);
                message += $" Model saved to {copy ?? model}";
            }
            else if (stopWasConnected && !stopIsError)
            {
                message += " No model was exported. Check the [Trainer] lines above for an error from Python.";
            }

            if (stopIsError)
                Debug.LogError($"[Training] {message}", this);
            else
                Debug.Log($"[Training] {message}", this);

            SetState(TrainingState.Idle, message);
        }

        private void FinishFailedLaunch()
        {
            RestoreTime();
            ReportFailure(session.Error ?? "The Python trainer stopped before it was ready.");
            SetState(TrainingState.Idle, Status);
        }

        private bool Validate(out string error)
        {
            error = null;

            if (roverPrefab == null)
                error = "No rover prefab is assigned on the TrainingOrganizer.";
            else if (roverPrefab.GetComponentInChildren<RoverAIController>(true) == null)
                error = $"'{roverPrefab.name}' has no {nameof(RoverAIController)}. Use an AI controlled rover prefab.";
            else if (new TileProvider(loadMap).CurrentTile() == null)
                error = "No map tile is loaded. Pick a map (or load a tile from the map grid) before training.";

            if (error != null)
                return false;

            var behavior = roverPrefab.GetComponentInChildren<BehaviorParameters>(true);
            behaviorName = behavior != null ? behavior.BehaviorName : "";
            if (string.IsNullOrEmpty(behaviorName))
            {
                error = $"'{roverPrefab.name}' has no Behavior Parameters with a behavior name.";
                return false;
            }

            var requester = roverPrefab.GetComponentInChildren<DecisionRequester>(true);
            decisionPeriod = requester != null ? Mathf.Max(1, requester.DecisionPeriod) : 1;

            if (request.RocksEnabled && request.RockDensity > 0f && rockPlacer == null)
                Debug.LogWarning("[Training] Obstacle density is set but no RockPlacer is assigned; training without rocks.", this);
            else if (request.RocksEnabled && request.RockDensity <= 0f)
                Debug.Log("[Training] Obstacle density is 0; training without rocks.", this);

            return true;
        }

        private void WarnAboutUnsupported(SimulationSettings uiSettings)
        {
            if (uiSettings.pits || uiSettings.craters || uiSettings.items)
                Debug.Log("[Training] Pits, craters and items are not generated yet; only rocks are placed.", this);
        }

        private void CancelPythonSetup()
        {
            if (setupRoutine != null)
            {
                StopCoroutine(setupRoutine);
                setupRoutine = null;
            }

            pythonEnvironment.Cancel();
        }

        private void DeactivateInstances()
        {
            foreach (TrainingInstance instance in instances)
            {
                if (instance != null)
                    instance.gameObject.SetActive(false);
            }
        }

        private void DestroyInstances()
        {
            foreach (TrainingInstance instance in instances)
            {
                if (instance == null)
                    continue;

                instance.EpisodeEnded -= OnEpisodeEnded;
                instance.Teardown();
                Destroy(instance.gameObject);
            }

            instances.Clear();
        }

        private void RestoreTime()
        {
            Time.timeScale = originalTimeScale;
            Time.captureFramerate = originalCaptureFramerate;
            Application.runInBackground = originalRunInBackground;
        }

        private string RunningStatus()
        {
            string progress;
            switch (request.IterationUnit)
            {
                case IterationUnit.Episodes:
                    progress = $"episode {stats.Episodes}/{request.MaxIterations}";
                    break;
                case IterationUnit.Epochs:
                    progress = $"epoch {epoch.Number}/{request.MaxIterations}, {stats.Episodes} episodes";
                    break;
                default:
                    progress = $"epoch {epoch.Number}, {stats.Episodes} episodes, up to {request.MaxTrainerSteps:N0} steps";
                    break;
            }

            return $"Training on {epoch.Tile.name}: {progress}, success {stats.RecentSuccessRate:P0}";
        }

        private void ReportFailure(string error)
        {
            Debug.LogError($"[Training] {error}", this);
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

        private static bool TryParsePositive(string text, out float value)
        {
            return (float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
                    float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) && value > 0f;
        }

        private void OnDestroy()
        {
            if (State == TrainingState.Idle)
                return;

            CancelPythonSetup();
            RestoreTime();
            session.CloseImmediately();
            DeactivateInstances();
            DestroyInstances();
            if (epoch != null)
                epoch.End();
        }
    }
}
