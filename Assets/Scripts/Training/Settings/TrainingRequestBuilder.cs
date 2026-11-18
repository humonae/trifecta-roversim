using System;
using System.IO;
using UnityEngine;

namespace Training
{
    // Turns the Training panel's values (TrainingSettings) into the request the organizer runs.
    // TrainingSettings.defaults supplies the values the panel doesn't show.
    public static class TrainingRequestBuilder
    {
        private const string DefaultModelName = "trained_model";

        public static TrainingRequest Build(TrainingSettings ui, TrainerConfig trainerConfig, string mapName)
        {
            TrainingDefaults defaults = ui.defaults ?? new TrainingDefaults();

            int iterations = ui.Iterations > 0 ? ui.Iterations : defaults.DefaultIterations;
            string resultsDirectory = FirstExistingFolder(ui.ModelOutputPath) ?? defaults.DefaultResultsDirectory;
            string modelName = FirstValidName(ui.ModelName) ?? DefaultModelName;

            return new TrainingRequest
            {
                MapName = mapName ?? "",
                NewTileEachEpoch = ui.NewTileEachEpoch,
                RocksEnabled = ui.RocksOn,
                RockDensity = RockDensity(ui.ObstacleDensity, defaults),
                EpochSeconds = Mathf.Max(0.1f, ui.EpochMinutes) * 60f,
                EpisodeSeconds = Mathf.Max(1f, ui.EpisodeSeconds),
                IterationUnit = defaults.IterationUnit,
                MaxIterations = iterations,
                NetworkPreset = trainerConfig.PresetNamed(ui.NetworkArchitecture).Name,
                EarlyStopping = ui.EarlyStopping,
                SuccessTarget = Mathf.Clamp01(ui.SuccessTarget),
                ResultsDirectory = resultsDirectory,
                ModelOutputPath = Path.Combine(resultsDirectory, modelName + ".onnx"),
                RunName = modelName,
                RunId = PythonPaths.MakeRunId(modelName),
                UseFixedSeed = ui.FixedSeed,
                Seed = defaults.Seed,
                ResumeFrom = string.IsNullOrWhiteSpace(ui.ResumeCheckpointPath) ? "" : PythonPaths.Resolve(ui.ResumeCheckpointPath)
            };
        }

        // The obstacle slider goes from 0 to ObstacleSliderMax; its maximum means MaxRockDensity rocks/m².
        public static float RockDensity(float sliderValue, TrainingDefaults defaults)
        {
            return Mathf.Clamp01(sliderValue / Mathf.Max(1f, defaults.ObstacleSliderMax)) * defaults.MaxRockDensity;
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
