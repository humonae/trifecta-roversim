using UnityEngine;

namespace Training
{
    // After ResetRover (ArticulationBody.TeleportRoot), the chassis Transform still reports the
    // rover's old pose until physics has run. Anything that reads chassis.position at the start of an
    // episode would see where the rover was: the previous episode's end, or the previous tile after
    // an epoch switch. That gave a large one-off progress reward and fake OutOfBounds results.
    //
    // TrainingInstance and EvaluationInstance call Begin right after ResetRover, then Step from
    // FixedUpdate. Until Step returns Arrived, they skip rewards, the timer and the end checks.
    public class SpawnSettle
    {
        public enum Result { Waiting, Arrived, GaveUp }

        // Normally one or two physics steps. This cap stops an episode from never starting.
        public const int MaxSteps = 10;
        // Horizontal distance from the spawn that counts as there. The rover only settles
        // vertically when it is dropped, so this can stay loose.
        private const float Tolerance = 1f;

        private Vector3 spawn;
        private int steps;

        public bool Waiting { get; private set; }

        public void Begin(Vector3 spawnPosition)
        {
            spawn = spawnPosition;
            steps = 0;
            Waiting = true;
        }

        public Result Step(Transform chassis)
        {
            if (!Waiting)
                return Result.Arrived;

            steps++;
            if (EpisodeMonitor.HorizontalDistance(chassis.position, spawn) <= Tolerance)
            {
                Waiting = false;
                return Result.Arrived;
            }

            if (steps >= MaxSteps)
            {
                Waiting = false;
                return Result.GaveUp;
            }

            return Result.Waiting;
        }
    }
}
