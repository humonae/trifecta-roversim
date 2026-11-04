using System;
using UnityEngine;

namespace Training
{
    [Serializable]
    public class ArenaSettings
    {
        [Tooltip("Side length of the square area each rover trains in, in meters. Rocks are only placed inside arenas.")]
        [Min(10f)] public float Size = 100f;
        [Tooltip("Spawn and goal stay at least this far from the arena edge.")]
        [Min(0f)] public float EdgeMargin = 5f;
        [Min(1f)] public float MinGoalDistance = 10f;
        [Min(1f)] public float MaxGoalDistance = 40f;
        [Tooltip("Spawn and goal are only placed on ground flatter than this.")]
        [Range(0f, 60f)] public float MaxSlope = 20f;
        [Tooltip("Gap between the bottom of the wheels and the ground when the rover spawns, in meters.")]
        [Min(0f)] public float SpawnGap = 0.1f;
        [Tooltip("No rock may be within this distance of the spawn point.")]
        [Min(0f)] public float SpawnClearRadius = 2.5f;
        [Tooltip("No rock may be within this distance of the goal.")]
        [Min(0f)] public float GoalClearRadius = 1.5f;
        [Min(1)] public int PlacementAttempts = 50;
    }
}
