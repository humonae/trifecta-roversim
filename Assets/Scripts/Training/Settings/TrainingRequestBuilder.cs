using System;
using System.IO;
using UnityEngine;

namespace Training
{
    public static class TrainingRequestBuilder
    {
        private const string DefaultModelName = "trained_model";

        public static TrainingRequest Build(SimulationSettings ui, ONNXFilePicker outputPicker, TrainingDefaults defaults, TrainerConfig trainerConfig)
        {
            int iterations = ui.maxIterations > 0 ? ui.maxIterations : defaults.DefaultIterations;
            string resultsDirectory = FirstExistingFolder(ReadPicker(outputPicker, p => p.GetOutputPath()), ui.checkpointOutputFolder)
                                      ?? defaults.DefaultResultsDirectory;
            string modelName = FirstValidName(ReadPicker(outputPicker, p => p.GetFileName()), ui.onnxFileName) ?? DefaultModelName;
            string modelFolder = FirstExistingFolder(ui.onnxOutputFolder) ?? resultsDirectory;

            return new TrainingRequest
            {
                NewTileEachEpoch = defaults.NewTileEachEpoch,
                RocksEnabled = ui.rocks,
                RockDensity = Mathf.Clamp01(ui.obstacleDensity / defaults.ObstacleSliderMax) * defaults.MaxRockDensity,
                EpochSeconds = defaults.EpochMinutes * 60f,
                EpisodeSeconds = defaults.EpisodeSeconds,
                IterationUnit = defaults.IterationUnit,
                MaxIterations = iterations,
                NetworkPreset = trainerConfig.PresetAt(ui.networkArchitecture).Name,
                EarlyStopping = ui.earlyStopping,
                ResultsDirectory = resultsDirectory,
                ModelOutputPath = Path.Combine(modelFolder, modelName + ".onnx"),
                RunName = modelName,
                RunId = PythonPaths.MakeRunId(modelName),
                UseFixedSeed = defaults.UseFixedSeed,
                Seed = defaults.Seed
            };
        }

        private static string ReadPicker(ONNXFilePicker picker, Func<ONNXFilePicker, string> read)
        {
            if (picker == null)
                return null;

            try
            {
                return read(picker);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string FirstExistingFolder(params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && Path.IsPathRooted(candidate.Trim()) && Directory.Exists(candidate.Trim()))
                    return candidate.Trim();
            }

            return null;
        }

        private static string FirstValidName(params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                string name = candidate.Trim();
                if (name.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(0, name.Length - 5).Trim();

                if (name.Length > 0 && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && name.IndexOfAny(new[] { '?', '*', ' ' }) < 0)
                    return name;
            }

            return null;
        }
    }
}
