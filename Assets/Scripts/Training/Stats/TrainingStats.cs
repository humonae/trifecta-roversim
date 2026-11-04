using System.Collections.Generic;

namespace Training
{
    public class TrainingStats
    {
        private readonly struct Attempt
        {
            public readonly bool Success;
            public readonly float Seconds;

            public Attempt(bool success, float seconds)
            {
                Success = success;
                Seconds = seconds;
            }
        }

        private readonly Queue<Attempt> recent = new Queue<Attempt>();
        private readonly int window;
        private int recentSuccesses;
        private float recentGoalSeconds;

        public TrainingStats(int window)
        {
            this.window = window < 1 ? 1 : window;
        }

        public int Episodes { get; private set; }
        public int Successes { get; private set; }
        public int CompletedEpochs { get; private set; }

        public int RecentCount => recent.Count;
        public bool WindowFull => recent.Count >= window;
        public float RecentSuccessRate => recent.Count > 0 ? (float)recentSuccesses / recent.Count : 0f;
        public float RecentAverageGoalSeconds => recentSuccesses > 0 ? recentGoalSeconds / recentSuccesses : float.PositiveInfinity;

        public void RecordEpisode(EpisodeOutcome outcome, float seconds)
        {
            bool success = outcome == EpisodeOutcome.ReachedGoal;
            Episodes++;
            if (success)
                Successes++;

            recent.Enqueue(new Attempt(success, seconds));
            if (success)
            {
                recentSuccesses++;
                recentGoalSeconds += seconds;
            }

            if (recent.Count > window)
            {
                Attempt dropped = recent.Dequeue();
                if (dropped.Success)
                {
                    recentSuccesses--;
                    recentGoalSeconds -= dropped.Seconds;
                }
            }
        }

        public void RecordEpoch()
        {
            CompletedEpochs++;
        }
    }
}
