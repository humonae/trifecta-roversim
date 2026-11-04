using UnityEngine;

namespace Training
{
    public class EpisodeMonitor
    {
        private readonly EpisodeRules rules;

        public EpisodeMonitor(EpisodeRules rules)
        {
            this.rules = rules;
        }

        public EpisodeOutcome Evaluate(Transform chassis, Vector3 goal, Rect arena, EpisodeTimer timer)
        {
            Vector3 position = chassis.position;

            if (HorizontalDistance(position, goal) <= rules.GoalRadius)
                return EpisodeOutcome.ReachedGoal;

            if (!arena.Contains(new Vector2(position.x, position.z)))
                return EpisodeOutcome.OutOfBounds;

            if (Vector3.Angle(chassis.up, Vector3.up) > rules.MaxTiltAngle)
                return EpisodeOutcome.Flipped;

            if (timer.Expired)
                return EpisodeOutcome.TimedOut;

            return EpisodeOutcome.Running;
        }

        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return new Vector2(a.x - b.x, a.z - b.z).magnitude;
        }
    }
}
