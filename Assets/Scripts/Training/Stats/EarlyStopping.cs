namespace Training
{
    public static class EarlyStopping
    {
        public static bool ShouldStop(TrainingStats stats, EarlyStoppingSettings settings, out string reason)
        {
            reason = null;
            if (!stats.WindowFull || stats.RecentSuccessRate < settings.MinSuccessRate)
                return false;

            if (settings.MaxAverageGoalSeconds > 0f && stats.RecentAverageGoalSeconds > settings.MaxAverageGoalSeconds)
                return false;

            reason = $"Early stopping: {stats.RecentSuccessRate:P0} of the last {stats.RecentCount} attempts reached the goal";
            if (settings.MaxAverageGoalSeconds > 0f)
                reason += $" in {stats.RecentAverageGoalSeconds:0.0} s on average";

            reason += ".";
            return true;
        }
    }
}
