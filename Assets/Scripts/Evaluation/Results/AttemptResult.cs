using System;
using Training;
using UnityEngine;

namespace Evaluation
{
    [Serializable]
    public class AttemptResult
    {
        // Index of the tile + rock layout the episode ran on (0-based).
        public int Layout;
        public string Tile = "";
        public int Rover;
        // Episode number within the whole run (0-based).
        public int Episode;
        public string Outcome = "";
        public bool Success;
        [Tooltip("Simulated seconds from spawn until the goal was reached or the episode ended.")]
        public float Seconds;
        [Tooltip("Straight-line horizontal distance from spawn to goal, in meters.")]
        public float StartDistance;
        [Tooltip("Horizontal distance to the goal when the episode ended, in meters.")]
        public float FinalDistance;
        [Tooltip("The chassis touched a rock at some point in the episode.")]
        public bool HitRock;
        [Tooltip("A wheel or the suspension touched a rock at some point in the episode.")]
        public bool WheelsOnRock;
        public Vector3 Spawn;
        public Vector3 Goal;

        public static AttemptResult From(EpisodeOutcome outcome, float seconds, float startDistance, float finalDistance,
            bool hitRock, bool wheelsOnRock, Vector3 spawn, Vector3 goal)
        {
            return new AttemptResult
            {
                Outcome = outcome.ToString(),
                Success = outcome == EpisodeOutcome.ReachedGoal,
                Seconds = seconds,
                StartDistance = startDistance,
                FinalDistance = finalDistance,
                HitRock = hitRock,
                WheelsOnRock = wheelsOnRock,
                Spawn = spawn,
                Goal = goal
            };
        }
    }
}
