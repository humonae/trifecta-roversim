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
        [Tooltip("Cost per meter driven backwards. At the same value as Progress Reward Per Meter, reversing toward the goal earns nothing. 0 lets the rover reverse freely.")]
        [Min(0f)] public float ReversePenaltyPerMeter = 0.05f;
        [Tooltip("Cost per second while a wheel or the suspension touches a rock.")]
        [Min(0f)] public float WheelRockPenaltyPerSecond = 0.02f;
        [Tooltip("Cost per second while the chassis touches a rock.")]
        [Min(0f)] public float ChassisRockPenaltyPerSecond = 0.2f;

        [Header("Stall")]
        [Tooltip("End an episode when the rover goes this long without getting Stall Progress Meters closer to the goal than its best so far. It's reported as a timeout. In training it's also trained as a failure (Stall Penalty), so parking is no safer than crashing. Evaluation uses it too unless End Stalled Episodes is off on the EvaluationOrganizer. 0 turns it off everywhere.")]
        [Min(0f)] public float StallSeconds = 20f;
        [Tooltip("How much closer to the goal than its best so far the rover has to get to reset the stall clock, in meters.")]
        [Min(0.1f)] public float StallProgressMeters = 1f;
        [Tooltip("Training only: final reward for a stalled episode. The same as Failure Penalty by default.")]
        public float StallPenalty = -1f;
    }
}
