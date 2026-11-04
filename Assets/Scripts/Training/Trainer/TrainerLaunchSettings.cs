using System;
using UnityEngine;

namespace Training
{
    [Serializable]
    public class TrainerLaunchSettings
    {
        [Tooltip("Simulation speed training starts at. It can be changed while training with TrainingOrganizer.SetTimeScale or a TrainingSpeedControl.")]
        [Min(1f)] public float TimeScale = 1f;
        [Tooltip("Seconds to wait for Python to start listening before giving up.")]
        [Min(5f)] public float StartupTimeout = 180f;
        [Tooltip("Seconds to wait for Python to save the model and exit after training stops.")]
        [Min(1f)] public float ShutdownTimeout = 60f;
        [Tooltip("How long Python waits for Unity between steps. Kept long so training can stay paused.")]
        [Min(60)] public int ConnectionTimeout = 7 * 24 * 60 * 60;
        [Tooltip("Appended to the mlagents-learn arguments, for example --torch-device cuda.")]
        public string ExtraArguments = "";
    }
}
