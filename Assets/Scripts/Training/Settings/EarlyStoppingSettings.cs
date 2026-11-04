using System;
using UnityEngine;

namespace Training
{
    [Serializable]
    public class EarlyStoppingSettings
    {
        [Tooltip("Number of most recent attempts the checks look at.")]
        [Min(1)] public int Window = 100;
        [Tooltip("Stop once at least this fraction of the recent attempts reached the goal.")]
        [Range(0f, 1f)] public float MinSuccessRate = 0.9f;
        [Tooltip("Also require the average time to reach the goal to be under this many seconds. 0 turns this check off.")]
        [Min(0f)] public float MaxAverageGoalSeconds;
    }
}
