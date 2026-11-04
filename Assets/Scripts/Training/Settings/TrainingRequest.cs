using System;
using UnityEngine;

namespace Training
{
    [Serializable]
    public class TrainingRequest
    {
        public string StartTile = "";
        public bool NewTileEachEpoch;
        public bool RocksEnabled;
        public float RockDensity;
        public float EpochSeconds;
        public float EpisodeSeconds;
        public IterationUnit IterationUnit;
        public int MaxIterations;
        public long MaxTrainerSteps;
        public string NetworkPreset = "";
        public bool EarlyStopping;
        public string ResultsDirectory = "";
        public string ModelOutputPath = "";
        public string RunName = "";
        public string RunId = "";
        public bool UseFixedSeed;
        public int Seed;

        public string ToLogString()
        {
            return JsonUtility.ToJson(this, true);
        }
    }
}
