using System;
using System.IO;
using Training;
using UnityEngine;

namespace Evaluation
{
    // Turns the Evaluation panel's values (EvaluationSettings) into the request the organizer runs.
    public static class EvaluationRequestBuilder
    {
        // training is optional. When given, the episode time limit and obstacle scale come from the
        // Training panel's settings, so the model is tested under the conditions it trained in.
        public static EvaluationRequest Build(EvaluationSettings ui, TrainingSettings training, string mapName)
        {
            EvaluationDefaults defaults = ui.defaults ?? new EvaluationDefaults();
            TrainingDefaults trainingDefaults = training != null ? training.defaults : null;

            float episodeSeconds = training != null ? training.EpisodeSeconds : defaults.EpisodeSeconds;
            float sliderMax = trainingDefaults != null ? trainingDefaults.ObstacleSliderMax : defaults.ObstacleSliderMax;
            float maxDensity = trainingDefaults != null ? trainingDefaults.MaxRockDensity : defaults.MaxRockDensity;
            string modelPath = ChosenModelPath(ui.SelectedModel);

            return new EvaluationRequest
            {
                ModelPath = modelPath ?? "",
                ModelName = string.IsNullOrEmpty(modelPath) ? "" : Path.GetFileNameWithoutExtension(modelPath),
                Episodes = ui.EpisodeCount > 0 ? ui.EpisodeCount : defaults.DefaultEpisodes,
                TestOn = ui.TestOn,
                EpisodesPerLayout = Mathf.Max(1, defaults.EpisodesPerLayout),
                RoverCount = Mathf.Max(1, defaults.RoverCount),
                EpisodeSeconds = Mathf.Max(1f, episodeSeconds),
                RocksEnabled = ui.RocksOn,
                ObstacleDensity = ui.ObstacleDensity,
                RockDensity = Mathf.Clamp01(ui.ObstacleDensity / Mathf.Max(1f, sliderMax)) * maxDensity,
                DeterministicInference = defaults.DeterministicInference,
                Seed = ui.StartingSeed,
                MapName = mapName ?? "",
                RandomPosition = ui.RandomPosition,
                StartPos = ui.StartPos,
                GoalPos = ui.GoalPos
            };
        }

        // The picked model file, or null if none was picked ("None", empty or not a model file).
        public static string ChosenModelPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            path = path.Trim();
            return path.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".sentis", StringComparison.OrdinalIgnoreCase)
                ? path
                : null;
        }
    }
}
