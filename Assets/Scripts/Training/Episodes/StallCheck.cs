namespace Training
{
    // Ends a training episode when the rover stops getting closer to the goal, so parking costs as much as
    // crashing instead of being the safe choice.
    //
    // The rover has to beat its closest distance to the goal by Meters at least once every Seconds.
    // Turning around (which first takes it further away) or going around a rock is fine as long as it
    // ends up closer within that time.
    public class StallCheck
    {
        private float mark;
        private float sinceProgress;

        public float SecondsWithoutProgress => sinceProgress;

        public void Begin(float distanceToGoal)
        {
            mark = distanceToGoal;
            sinceProgress = 0f;
        }

        // True once the rover has gone `seconds` without getting `meters` closer than its mark.
        // seconds <= 0 turns the check off.
        public bool Step(float distanceToGoal, float deltaTime, float seconds, float meters)
        {
            if (seconds <= 0f)
                return false;

            if (distanceToGoal <= mark - meters)
            {
                mark = distanceToGoal;
                sinceProgress = 0f;
                return false;
            }

            sinceProgress += deltaTime;
            return sinceProgress >= seconds;
        }
    }
}
