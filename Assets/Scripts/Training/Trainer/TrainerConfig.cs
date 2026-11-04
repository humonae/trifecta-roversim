using System;
using System.Collections.Generic;
using UnityEngine;

namespace Training
{
    [Serializable]
    public class TrainerConfig
    {
        [Tooltip("Matched by index to the Network Architecture dropdown.")]
        public List<NetworkPreset> NetworkPresets = new List<NetworkPreset>
        {
            new NetworkPreset("Small", 128, 2),
            new NetworkPreset("Medium", 256, 2),
            new NetworkPreset("Large", 512, 3)
        };

        [Header("PPO")]
        [Min(1)] public int BatchSize = 1024;
        [Min(1)] public int BufferSize = 20480;
        [Min(0f)] public float LearningRate = 3e-4f;
        [Min(0f)] public float Beta = 5e-3f;
        [Min(0f)] public float Epsilon = 0.2f;
        [Range(0f, 1f)] public float Lambda = 0.95f;
        [Tooltip("Passes over the buffer per update (ML-Agents num_epoch, not a training epoch).")]
        [Min(1)] public int NumEpoch = 3;
        [Range(0f, 1f)] public float Gamma = 0.99f;
        public bool NormalizeObservations;

        [Header("Run")]
        [Min(1)] public int TimeHorizon = 128;
        [Min(1)] public int SummaryFrequency = 10000;
        [Min(1)] public int CheckpointInterval = 200000;
        [Min(1)] public int KeepCheckpoints = 5;

        public NetworkPreset PresetAt(int index)
        {
            if (NetworkPresets == null || NetworkPresets.Count == 0)
                return new NetworkPreset("Default", 256, 2);

            return NetworkPresets[Mathf.Clamp(index, 0, NetworkPresets.Count - 1)];
        }

        public NetworkPreset PresetNamed(string name)
        {
            foreach (NetworkPreset preset in NetworkPresets)
            {
                if (preset.Name == name)
                    return preset;
            }

            return PresetAt(0);
        }
    }
}
