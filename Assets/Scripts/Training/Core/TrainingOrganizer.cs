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
    public class TrainingOrganizer : MonoBehaviour, ISimulationRun
    {
        [Header("Scene")]
        [SerializeField] private LoadMap loadMap;
        [SerializeField] private RockPlacer rockPlacer;
        [SerializeField] private GameObject roverPrefab;
        [Tooltip("Optional. Lets the Play button start a run with the panel's current settings.")]
        [SerializeField] private SimulationManager simulationManager;
        [Tooltip("Parent for the spawned instances. Defaults to this object.")]
        [SerializeField] private Transform instancesRoot;

        [Header("Training")]
        [Tooltip("Rovers trained side by side on the same tile, each in its own arena.")]
        [SerializeField, Min(1)] private int instanceCount = 1;
        [SerializeField] private ArenaSettings arena = new ArenaSettings();
        [SerializeField] private EpisodeRules rules = new EpisodeRules();
        [Tooltip("Window and optional time target. The success target comes from the Training panel.")]
        [SerializeField] private EarlyStoppingSettings earlyStopping = new EarlyStoppingSettings();
        [SerializeField] private bool logEpisodes = true;
        [Tooltip("What \"recent\" means in the training report, its learning-curve blocks, and how often it's saved during a run.")]
        [SerializeField] private TrainingReportSettings reportSettings = new TrainingReportSettings();

        [Header("Python")]
        [SerializeField] private PythonSetupSettings python = new PythonSetupSettings();
        [SerializeField] private TrainerLaunchSettings trainer = new TrainerLaunchSettings();
        [SerializeField] private TrainerConfig trainerConfig = new TrainerConfig();

        private readonly List<TrainingInstance> instances = new List<TrainingInstance>();
        private readonly TrainerSession session = new TrainerSession();
        private readonly PythonEnvironment pythonEnvironment = new PythonEnvironment();
        private readonly CheckpointResume checkpoint = new CheckpointResume();
        private bool discardModel;
        private Coroutine setupRoutine;
        private string configPath;
        private int decisionPeriod = 1;
        private TrainingRequest request;
        private EarlyStoppingSettings activeEarlyStopping;
        private readonly List<string> trainedTiles = new List<string>();
        private TrainingStats stats;
        private Epoch epoch;
        private string behaviorName;
        private string stopReason;
        private bool stopIsError;
        private bool stopWasConnected;
        // The run reached its iteration target, step budget or early-stopping goal (not Stop or an error).
        private bool stopCompleted;
        private float runEndTime;
        private TrainingReport report;
        private TrainingReport previousRun;
        private float requestedSpeed = -1f;
        private bool speedApplied;
        private float runStartTime;
        private float originalTimeScale = 1f;
        private int originalCaptureFramerate;
        private bool originalRunInBackground;

        public event Action<TrainingState> StateChanged;
        public event Action<string> StatusChanged;
        public event Action<float> SpeedChanged;
        // Same pattern as EvaluationOrganizer:
        // a run has started; the report is empty but has its run number and settings.
        public event Action<TrainingReport> RunStarted;
        // An episode finished; the report already includes it.
        public event Action<TrainingAttempt, TrainingReport> EpisodeFinished;
        // The run ended; the report is final and saved (path, or null if saving failed).
        public event Action<TrainingReport, string> Finished;

        // The current run's report, updated after every episode. Null before the first run.
        public TrainingReport Report => report;
        public TrainingReport LastReport { get; private set; }
        public string LastReportPath { get; private set; }

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

        // Read by EvaluationOrganizer so models are tested under the conditions they were trained in.
        public GameObject RoverPrefab => roverPrefab;
        public ArenaSettings Arena => arena;
        public EpisodeRules Rules => rules;

        public string ProgressDetail
        {
            get
            {
                if (!IsTraining || epoch == null || instances.Count == 0)
                    return null;

                EpisodeTimer episode = instances[0].Timer;
                return $"Epoch {epoch.Timer.Remaining / 60f:0.0} min left  |  Episode {episode.Elapsed:0}/{episode.Limit:0} s";
            }
        }

        public bool StartTraining(TrainingSettings uiSettings)
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
            request = TrainingRequestBuilder.Build(uiSettings, trainerConfig, new TileProvider(loadMap).MapName);

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
            activeEarlyStopping = new EarlyStoppingSettings
            {
                Window = earlyStopping.Window,
                MinSuccessRate = request.SuccessTarget,
                MaxAverageGoalSeconds = earlyStopping.MaxAverageGoalSeconds
            };
            trainedTiles.Clear();
            discardModel = false;
            stopCompleted = false;
            checkpoint.Cancel();
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
                simulationManager.StartTraining();
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

        private IEnumerator SetUpPythonAndLaunch()
        {
            yield return pythonEnvironment.Ensure(python);

            // Resuming: read the checkpoint with the trainer's Python, then rewrite the config with its network.
            string checkpointError = null;
            if (State == TrainingState.SettingUpPython && pythonEnvironment.IsReady && request.ResumesFromCheckpoint)
            {
                SetStatus("Reading the checkpoint...");
                yield return checkpoint.Prepare(pythonEnvironment.Python, request, roverPrefab, trainerConfig);
                checkpointError = checkpoint.Error;
                if (checkpointError == null && State == TrainingState.SettingUpPython)
                    TrainerConfigWriter.TryWrite(request, behaviorName, trainerConfig, out configPath, out checkpointError);
            }

            setupRoutine = null;

            if (State != TrainingState.SettingUpPython)
                yield break;

            if (checkpointError != null || !pythonEnvironment.IsReady || !session.BeginLaunch(trainer, request, configPath, pythonEnvironment.Python))
            {
                RestoreTime();
                ReportFailure(checkpointError ?? (pythonEnvironment.IsReady ? session.Error : pythonEnvironment.Error));
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
                    if (!checkpoint.IsBusy && pythonEnvironment.Status != Status && !string.IsNullOrEmpty(pythonEnvironment.Status))
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
            RecordTile(epoch.Tile);

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

            previousRun = TrainingHistory.Latest();
            report = TrainingReport.Create(request, TrainingHistory.NextRunNumber(), instances.Count, reportSettings, previousRun, DateTime.Now);
            RunStarted?.Invoke(report);
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
                stopCompleted = true;
                BeginStop($"Reached {request.MaxIterations} epochs.", false);
                return;
            }

            string error;
            if (!epoch.Begin(request, instanceCount, request.NewTileEachEpoch, out error))
            {
                BeginStop(error, true);
                return;
            }

            RecordTile(epoch.Tile);
            foreach (TrainingInstance instance in instances)
                instance.RestartEpisode();

            Debug.Log($"[Training] Epoch {epoch.Number} started on '{epoch.Tile.name}'.", this);
            SetStatus(RunningStatus());
        }

        private void OnEpisodeEnded(TrainingInstance instance, EpisodeOutcome outcome)
        {
            stats.RecordEpisode(outcome, instance.Timer.Elapsed);
            RecordAttempt(instance, outcome);

            if (logEpisodes)
            {
                Debug.Log($"[Training] Rover {instance.Index}, episode {stats.Episodes}: {outcome} after {instance.Timer.Elapsed:0.0} s, " +
                          $"reward {instance.Rover.GetCumulativeReward():0.00}. Recent success rate {stats.RecentSuccessRate:P0}.", this);
            }

            if (State == TrainingState.Running)
                SetStatus(RunningStatus());
        }

        private void RecordAttempt(TrainingInstance instance, EpisodeOutcome outcome)
        {
            if (report == null)
                return;

            TrainingAttempt attempt = TrainingAttempt.From(outcome, instance.Timer.Elapsed, instance.Rover.GetCumulativeReward(),
                instance.EpisodeStartDistance, instance.EpisodeFinalDistance, instance.EpisodeHitRock, instance.EpisodeWheelsOnRock,
                instance.Layout.Spawn.position, instance.Layout.Goal);
            attempt.Episode = report.AttemptLog.Count;
            attempt.Epoch = epoch != null ? epoch.Number : 0;
            attempt.Tile = epoch != null && epoch.Tile != null ? epoch.Tile.name : "";
            attempt.Rover = instance.Index;
            attempt.EndedAt = Time.time - runStartTime;

            report.Record(attempt, previousRun);
            EpisodeFinished?.Invoke(attempt, report);

            int autosave = report.Settings.AutosaveEveryEpisodes;
            if (autosave > 0 && report.Episodes % autosave == 0)
            {
                report.StopReason = "In progress";
                report.SimulatedSeconds = attempt.EndedAt;
                report.Epochs = epoch != null ? epoch.Number : 0;
                TrainingHistory.Save(report, session.RunDirectory);
            }
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
            if (checkpoint.LoadFailed(session.Output, out reason))
            {
                // The trainer is only training a fresh network; don't hand that out as the resumed model.
                discardModel = true;
                BeginStop(reason, true);
            }
            else if (session.QuitReceived)
                StopCompleted($"The trainer reached its {request.MaxTrainerSteps:N0}-step budget.");
            else if (session.ProcessExited)
                BeginStop($"The Python trainer exited unexpectedly with code {session.ExitCode}. Last output:\n{session.RecentOutput()}", true);
            else if (request.IterationUnit == IterationUnit.Episodes && stats.Episodes >= request.MaxIterations)
                StopCompleted($"Reached {request.MaxIterations} episodes.");
            else if (request.EarlyStopping && EarlyStopping.ShouldStop(stats, activeEarlyStopping, out reason))
                StopCompleted(reason);
        }

        private void StopCompleted(string reason)
        {
            stopCompleted = true;
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
            runEndTime = Time.time;

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
            string model = stopWasConnected && !discardModel && session.RunId == request.RunId
                ? ModelExporter.FindModel(session.RunDirectory, behaviorName)
                : null;

            string savedModel = null;
            if (model != null)
            {
                string copy = ModelExporter.CopyTo(model, request.ModelOutputPath);
                savedModel = copy ?? model;
                message += $" Model saved to {copy ?? model}";
                // Also in the run folder, so a later run resuming from this run's checkpoints can find its tiles.
                SaveRunInfo(model);
                if (copy != null && copy != model)
                    SaveRunInfo(copy);
            }
            else if (discardModel)
            {
                message += " The model was not copied to the output file.";
            }
            else if (stopWasConnected && !stopIsError)
            {
                message += " No model was exported. Check the [Trainer] lines above for an error from Python.";
            }

            if (stopIsError)
                Debug.LogError($"[Training] {message}", this);
            else
                Debug.Log($"[Training] {message}", this);

            FinishReport(savedModel);
            SetState(TrainingState.Idle, message);
        }

        // Final numbers, saved to TrainingHistory.Folder and the run's results folder, then Finished.
        private void FinishReport(string modelPath)
        {
            if (report == null || report.AttemptLog.Count == 0)
                return;

            report.Finish(stopCompleted && !stopIsError, stopIsError, stopReason, DateTime.Now, runEndTime - runStartTime,
                epoch != null ? epoch.Number : 0, modelPath, previousRun);
            LastReportPath = TrainingHistory.Save(report, session.RunDirectory);
            LastReport = report;

            string saved = LastReportPath != null ? $"Saved to {LastReportPath}" : "The training report could not be saved; see the warnings above.";
            Debug.Log($"[Training] {report.Summary()}\n\nLearning curve:\n{report.ProgressTable()}\n{saved}", this);
            Finished?.Invoke(report, LastReportPath);
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

            if (!CheckpointResume.ValidatePath(request.ResumeFrom, out error))
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

        private void RecordTile(GameObject tile)
        {
            if (tile != null && !trainedTiles.Contains(tile.name))
                trainedTiles.Add(tile.name);
        }

        // Lets evaluation's "Held-out Tiles" skip the tiles this model trained on.
        private void SaveRunInfo(string modelPath)
        {
            var info = new TrainingRunInfo
            {
                Model = request.RunName,
                RunId = request.RunId,
                Map = request.MapName,
                Tiles = new List<string>(trainedTiles),
                FinishedAt = DateTime.Now.ToString("s", CultureInfo.InvariantCulture),
                Seed = request.Seed
            };

            checkpoint.AddLineage(info, request);
            info.TrySave(modelPath);
        }

        private void CancelPythonSetup()
        {
            if (setupRoutine != null)
            {
                StopCoroutine(setupRoutine);
                setupRoutine = null;
            }

            pythonEnvironment.Cancel();
            checkpoint.Cancel();
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
