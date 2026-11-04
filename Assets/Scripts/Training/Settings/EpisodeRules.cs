using System;
using UnityEngine;

namespace Training
{
    [Serializable]
    public class EpisodeRules
    {
        [Tooltip("Horizontal distance from the goal that counts as reaching it, in meters.")]
        [Min(0.1f)] public float GoalRadius = 1.5f;
        [Tooltip("The episode fails once the chassis tilts further than this from upright.")]
        [Range(10f, 180f)] public float MaxTiltAngle = 70f;

        [Header("Rewards")]
        public float GoalReward = 1f;
        public float FailurePenalty = -1f;
        public float TimeoutReward;
        [Tooltip("Reward per meter of progress toward the goal; moving away costs the same.")]
        public float ProgressRewardPerMeter = 0.05f;
        [Min(0f)] public float TimePenaltyPerSecond = 0.01f;
    }
}
