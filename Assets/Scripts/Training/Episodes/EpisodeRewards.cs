namespace Training
{
    public class EpisodeRewards
    {
        private readonly EpisodeRules rules;
        private float lastDistance;

        public EpisodeRewards(EpisodeRules rules)
        {
            this.rules = rules;
        }

        public void Begin(float distanceToGoal)
        {
            lastDistance = distanceToGoal;
        }

        public float Step(float distanceToGoal, float deltaTime)
        {
            float progress = lastDistance - distanceToGoal;
            lastDistance = distanceToGoal;
            return progress * rules.ProgressRewardPerMeter - rules.TimePenaltyPerSecond * deltaTime;
        }

        public float Final(EpisodeOutcome outcome)
        {
            switch (outcome)
            {
                case EpisodeOutcome.ReachedGoal:
                    return rules.GoalReward;
                case EpisodeOutcome.TimedOut:
                    return rules.TimeoutReward;
                case EpisodeOutcome.Flipped:
                case EpisodeOutcome.OutOfBounds:
                    return rules.FailurePenalty;
                default:
                    return 0f;
            }
        }
    }
}
