using System;
using Training;
using UnityEngine;

namespace Evaluation
{
    [Serializable]
    public class EvaluationRequest
    {
        public string ModelPath = "";
        public string ModelName = "";
        public int Episodes;
        public EvaluationTiles TestOn;
        public int EpisodesPerLayout;
        public int RoverCount;
        public float EpisodeSeconds;
        public bool RocksEnabled;
        public float ObstacleDensity;
        public float RockDensity;
        public bool DeterministicInference;
        public int Seed;
        public string MapName = "";
        // Filled in when the run starts, e.g. "Selected Tile: Jezero C6".
        public string Selection = "";

        // Start and Goal: Random, or the points picked on the preview (Selected Tile only).
        public bool RandomPosition = true;
        public Vector3 StartPos;
        public Vector3 GoalPos;

        // Epoch only reads the rock settings; the epoch timer is unused during evaluation.
        public TrainingRequest ToEpochRequest()
        {
            return new TrainingRequest
            {
                RocksEnabled = RocksEnabled,
                RockDensity = RockDensity,
                EpisodeSeconds = EpisodeSeconds,
                EpochSeconds = 0f
            };
        }

        public string ToLogString()
        {
            return JsonUtility.ToJson(this, true);
        }
    }
}
