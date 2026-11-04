using System;
using UnityEngine;

namespace Training
{
    [Serializable]
    public class TrainingDefaults
    {
        [Header("Epochs")]
        [Tooltip("Simulated minutes spent on one tile and rock layout before moving on.")]
        [Min(0.1f)] public float EpochMinutes = 10f;
        [Tooltip("Simulated seconds one attempt may take before it is cut off.")]
        [Min(1f)] public float EpisodeSeconds = 60f;
        [Tooltip("Load a new random tile from the chosen map each epoch. Off keeps the starting tile and only re-rolls rocks.")]
        public bool NewTileEachEpoch = true;

        [Header("Iterations")]
        [Tooltip("What the Number of Iterations field counts. For episodes and epochs the trainer's learning-rate schedule is sized from the time limits.")]
        public IterationUnit IterationUnit = IterationUnit.Episodes;
        [Tooltip("Used when the Number of Iterations field is empty.")]
        [Min(1)] public int DefaultIterations = 1000;

        [Header("Obstacles")]
        [Tooltip("The obstacle density slider's maximum value.")]
        [Min(1f)] public float ObstacleSliderMax = 10f;
        [Tooltip("Rocks per square meter when the obstacle density slider is at its maximum.")]
        [Min(0f)] public float MaxRockDensity = 0.1f;

        [Header("Output")]
        [Tooltip("Used when no checkpoint output folder is picked. Relative paths start at the project folder.")]
        public string DefaultResultsDirectory = "results";

        [Header("Randomness")]
        public bool UseFixedSeed;
        public int Seed;
    }
}
