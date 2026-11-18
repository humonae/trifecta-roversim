using System;
using UnityEngine;

namespace Training
{
    // One training episode, as recorded in TrainingReport (the training counterpart of Evaluation.AttemptResult).
    // Episodes cut short by a new epoch aren't recorded, the same as in TrainingStats.
    [Serializable]
    public class TrainingAttempt
    {
        // Episode number within the whole run (0-based).
        public int Episode;
        public int Epoch;
        public string Tile = "";
        public int Rover;
        public string Outcome = "";
        public bool Success;
        [Tooltip("Simulated seconds from spawn until the goal was reached or the episode ended.")]
        public float Seconds;
        [Tooltip("The episode's total reward, including the final goal/failure reward.")]
        public float Reward;
        [Tooltip("Straight-line horizontal distance from spawn to goal, in meters.")]
        public float StartDistance;
        [Tooltip("Horizontal distance to the goal when the episode ended, in meters.")]
        public float FinalDistance;
        [Tooltip("The chassis touched a rock at some point in the episode.")]
        public bool HitRock;
        [Tooltip("A wheel or the suspension touched a rock at some point in the episode.")]
        public bool WheelsOnRock;
        [Tooltip("Simulated seconds since the run started, when the episode ended.")]
        public float EndedAt;
        public Vector3 Spawn;
        public Vector3 Goal;

        public static TrainingAttempt From(EpisodeOutcome outcome, float seconds, float reward, float startDistance, float finalDistance,
            bool hitRock, bool wheelsOnRock, Vector3 spawn, Vector3 goal)
        {
            return new TrainingAttempt
            {
                Outcome = outcome.ToString(),
                Success = outcome == EpisodeOutcome.ReachedGoal,
                Seconds = seconds,
                Reward = reward,
                StartDistance = startDistance,
                FinalDistance = finalDistance,
                HitRock = hitRock,
                WheelsOnRock = wheelsOnRock,
                Spawn = spawn,
                Goal = goal
            };
        }

                Tile = Tile,
                Rover = Rover,
                Episode = Episode,
                Outcome = Outcome,
                Success = Success,
                Seconds = Seconds,
                StartDistance = StartDistance,
                FinalDistance = FinalDistance,
                HitRock = HitRock,
                WheelsOnRock = WheelsOnRock,
                Spawn = Spawn,
                Goal = Goal
            };
        }
    }
}
