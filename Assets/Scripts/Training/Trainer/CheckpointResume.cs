using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace Training
{
    // Continues training from a .pt checkpoint (ML-Agents init_path). The new run starts at step 0
    // with the checkpoint's weights, optimizer state and normalizer, so Number of Iterations counts
    // the additional training and the learning rate schedule starts over.
    //
    // ML-Agents only warns when a checkpoint doesn't fit, then trains from random weights. So:
    //   1. Before launch, Python reads the checkpoint and its network replaces the preset.
    //   2. After the rover connects, the trainer's log is watched and the run is stopped if the
    //      weights still didn't load (for example because the rover's cameras changed).
    public class CheckpointResume
    {
        private const string CopySuffix = ".init.pt";
        private const int MaxQuotedLogLength = 300;

        private readonly CheckpointInspector inspector = new CheckpointInspector();
        private TrainingRunInfo previousRun;
        private string source;
        private bool watching;
        private int scannedLines;

        public bool IsBusy => inspector.IsRunning;
        public string Error { get; private set; }

        public static bool ValidatePath(string path, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(path))
                return true;

            string reason;
            if (CheckpointFile.Check(path, out reason, out error))
                return true;

            error += " Load another model, or clear it to train from scratch.";
            return false;
        }

        // Run as part of the organizer's setup coroutine, once Python is ready. Sets request.Checkpoint,
        // or Error. Ends with neither if Cancel() was called.
        public IEnumerator Prepare(PythonCommand python, TrainingRequest request, GameObject roverPrefab, TrainerConfig config)
        {
            Error = null;
            watching = false;
            previousRun = null;
            source = request.ResumeFrom;
            request.Checkpoint = new CheckpointNetwork();

            string copy = Path.Combine(PythonPaths.Resolve(request.ResultsDirectory), "configs", request.RunId + CopySuffix);
            string error;
            if (!inspector.Begin(python, source, copy, out error))
            {
                Error = error;
                yield break;
            }

            while (inspector.IsRunning)
            {
                yield return null;
                inspector.Update();
            }

            if (inspector.Error != null)
            {
                Error = $"Can't resume from '{Path.GetFileName(source)}': {inspector.Error}";
                yield break;
            }

            CheckpointInfo info = inspector.Result;
            if (info == null)
                yield break;

            if (!CheckFits(info, roverPrefab, out error))
            {
                Error = $"Can't resume from '{Path.GetFileName(source)}': {error}";
                yield break;
            }

            request.Checkpoint = new CheckpointNetwork
            {
                IsSet = true,
                InitPath = copy,
                Step = info.step,
                HiddenUnits = info.hidden_units,
                Layers = info.num_layers,
                VisualEncoder = info.vis_encode_type ?? "",
                Normalize = info.normalize
            };

            Debug.Log($"[Training] Resuming from '{source}', trained for {info.step:N0} steps: {info.Describe()}. " +
                      $"The checkpoint's network is used instead of the '{request.NetworkPreset}' preset. " +
                      $"This run counts its own steps from 0.");

            previousRun = FindRunInfo(source);
            if (previousRun == null)
                Debug.LogWarning($"[Training] No training record was found for '{source}', so the new model's record will only list " +
                                 "the tiles from this run. Held-out evaluation may then include tiles the checkpoint trained on.");

            scannedLines = 0;
            watching = true;
        }

        public void Cancel()
        {
            inspector.Cancel();
            watching = false;
        }

        // Call while the run is active. True when ML-Agents reported that the checkpoint's weights
        // didn't load, in which case the run would only be training a fresh network.
        public bool LoadFailed(IReadOnlyList<string> trainerOutput, out string reason)
        {
            reason = null;
            if (!watching || trainerOutput == null)
                return false;

            for (; scannedLines < trainerOutput.Count; scannedLines++)
            {
                string line = trainerOutput[scannedLines];

                // Messages from mlagents 1.1.0 TorchModelSaver._load_model.
                if (line.Contains("Failed to load for module Policy") || line.Contains("Did not find these keys"))
                {
                    watching = false;
                    reason = $"The checkpoint '{Path.GetFileName(source)}' doesn't fit this rover, so ML-Agents could not load its weights " +
                             "and would have trained from scratch. This usually means the rover's cameras, observations or actions " +
                             $"changed since it was saved. Trainer said: {Shorten(line)}";
                    return true;
                }

                if (line.Contains("Failed to load for module Optimizer"))
                    Debug.LogWarning($"[Training] {Shorten(line.Trim())} The policy loaded, so training continues; the value estimate starts over.");

                if (line.Contains("Starting training from step 0"))
                {
                    watching = false;
                    Debug.Log($"[Training] Loaded the weights from '{source}'.");
                    return false;
                }
            }

            return false;
        }

        // Records where the model came from and carries over the tiles the checkpoint trained on.
        public void AddLineage(TrainingRunInfo info, TrainingRequest request)
        {
            if (request.Checkpoint == null || !request.Checkpoint.IsSet)
                return;

            info.InitializedFrom = request.ResumeFrom;
            info.InitializedFromStep = request.Checkpoint.Step;

            if (previousRun == null || previousRun.Tiles == null)
                return;

            foreach (string tile in previousRun.Tiles)
            {
                if (!info.Tiles.Contains(tile))
                    info.Tiles.Add(tile);
            }
        }

        private static bool CheckFits(CheckpointInfo info, GameObject roverPrefab, out string error)
        {
            error = null;

            if (info.hidden_units <= 0 || info.num_layers <= 0)
                error = "its network size could not be read.";
            else if (info.memory_size > 0)
                error = $"it uses memory (an LSTM of size {info.memory_size}), which RoverSim's trainer config doesn't set up.";

            if (error != null || roverPrefab == null)
                return error == null;

            // Actuator components add actions that BrainParameters doesn't list; leave those to the log check.
            if (roverPrefab.GetComponentsInChildren<ActuatorComponent>(true).Length > 0)
                return true;

            var behavior = roverPrefab.GetComponentInChildren<BehaviorParameters>(true);
            if (behavior == null)
                return true;

            ActionSpec actions = behavior.BrainParameters.ActionSpec;
            int[] branches = actions.BranchSizes ?? new int[0];
            int[] checkpointBranches = info.discrete_branches ?? new int[0];

            if (actions.NumContinuousActions != info.continuous_actions || !SameBranches(branches, checkpointBranches))
            {
                error = $"it was trained with {info.continuous_actions} continuous action(s) and {checkpointBranches.Length} discrete branch(es), " +
                        $"but '{roverPrefab.name}' has {actions.NumContinuousActions} and {branches.Length}. It belongs to a different rover setup.";
                return false;
            }

            return true;
        }

        private static bool SameBranches(int[] a, int[] b)
        {
            if (a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        // A checkpoint lives in <results>/<runId>/<behavior>/; the organizer saves that run's record
        // as <results>/<runId>/<behavior>.training.json, next to the run's own exported .onnx.
        private static TrainingRunInfo FindRunInfo(string checkpointPath)
        {
            try
            {
                string behaviorFolder = Path.GetDirectoryName(checkpointPath);
                string runFolder = Path.GetDirectoryName(behaviorFolder);
                if (string.IsNullOrEmpty(runFolder))
                    return null;

                TrainingRunInfo info;
                string runModel = Path.Combine(runFolder, Path.GetFileName(behaviorFolder) + ".onnx");
                return TrainingRunInfo.TryLoad(runModel, out info) ? info : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Shorten(string line)
        {
            string trimmed = line.Trim();
            return trimmed.Length <= MaxQuotedLogLength ? trimmed : trimmed.Substring(0, MaxQuotedLogLength) + "...";
        }
    }
}
