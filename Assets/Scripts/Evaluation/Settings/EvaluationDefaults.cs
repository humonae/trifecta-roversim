using System;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace Evaluation
{
    public enum EvaluationTiles
    {
        // The tile currently loaded (picked with Browse... in the map selection window).
        SelectedTile,
        // Every tile of the opened map that the model did not train on (read from <model>.training.json).
        HeldOutTiles,
        // Every tile of the opened map.
        AllTiles
    }

    // Evaluation values the Evaluation panel doesn't show. Lives on the EvaluationSettings component
    // as `defaults`, the same way TrainingDefaults lives on TrainingSettings.
    // Speed isn't here: evaluation runs at whatever the top-right speed control is set to.
    [Serializable]
    public class EvaluationDefaults
    {
        [Header("Run")]
        [Tooltip("Used when the Episodes field is empty or 0.")]
        [Min(1)] public int DefaultEpisodes = 1000;
        [Tooltip("Episodes run on one tile and rock layout before the rocks are re-rolled (and, when testing on several tiles, the next tile is loaded).")]
        [Min(1)] public int EpisodesPerLayout = 10;
        [Tooltip("Rovers tested side by side on the same tile, each in its own arena. The episodes of each layout are split between them.")]
        [Min(1)] public int RoverCount = 1;
        [Tooltip("Simulated seconds an episode may take before it counts as timed out. Ignored when the EvaluationOrganizer matches training.")]
        [Min(1f)] public float EpisodeSeconds = 60f;

        [Header("Inference")]
        [Tooltip("Use the policy's mean action instead of sampling from it, so results don't depend on sampling noise.")]
        public bool DeterministicInference = true;
        public InferenceDevice InferenceDevice = InferenceDevice.Default;

        [Header("Obstacles")]
        [Tooltip("The obstacle density slider's maximum value. Ignored when the EvaluationOrganizer matches training.")]
        [Min(1f)] public float ObstacleSliderMax = 10f;
        [Tooltip("Rocks per square meter at the slider's maximum. Ignored when the EvaluationOrganizer matches training.")]
        [Min(0f)] public float MaxRockDensity = 0.1f;

        [Header("Report")]
        [Tooltip("Optional extra folder for a copy of each report. Every run is always saved to the app's EvaluationRuns folder.")]
        public string ReportFolder = "";
    }
}
