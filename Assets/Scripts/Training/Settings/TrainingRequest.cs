using System;
using UnityEngine;

namespace Training
{
    [Serializable]
    public class TrainingRequest
    {
        public string MapName = "";
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
        [Tooltip("Fraction of recent attempts that must reach the goal for early stopping.")]
        public float SuccessTarget = 0.9f;
        public string ResultsDirectory = "";
        public string ModelOutputPath = "";
        public string RunName = "";
        public string RunId = "";
        public bool UseFixedSeed;
        public int Seed;
        [Tooltip("A .pt checkpoint to continue training from. Empty trains a new network.")]
        public string ResumeFrom = "";
        [Tooltip("Set once Python has read the checkpoint, before the trainer starts.")]
        public CheckpointNetwork Checkpoint = new CheckpointNetwork();

        public bool ResumesFromCheckpoint => !string.IsNullOrEmpty(ResumeFrom);

        public string ToLogString()
        {
            return JsonUtility.ToJson(this, true);
        }
    }
}
