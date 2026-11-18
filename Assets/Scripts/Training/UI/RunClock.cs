using Evaluation;
using UnityEngine;

namespace Training
{
    // The clock line under the speed arrows (TimeWarpPlacement, TimeWarpArrows):
    //   T- time until the current episode ends    T+ time since the run started
    // Both are simulated time, so they speed up with the arrows and stop while paused.
    // With several rovers, the episode is the first rover's, as in the status label.
    public static class RunClock
    {
        private const string NoEpisode = "--:--:--";

        public static string Text(ISimulationRun run)
        {
            if (run == null)
                return $"T- {NoEpisode}   T+ {Elapsed(0f)}";

            float? left = EpisodeSecondsLeft(run);
            string countdown = left.HasValue ? Countdown(left.Value) : NoEpisode;
            return $"T- {countdown}   T+ {Elapsed(run.SimulatedSeconds)}";
        }

        // Simulated seconds until the current episode ends, or null when no episode is running
        // or it has no time limit.
        public static float? EpisodeSecondsLeft(ISimulationRun run)
        {
            if (run is SimulationRuns hub)
                run = hub.Active;

            if (run is TrainingOrganizer training)
                return TrainingSecondsLeft(training);

            if (run is EvaluationOrganizer evaluation)
                return EvaluationSecondsLeft(evaluation);

            return null;
        }

        // A training episode also ends when the epoch does: every rover restarts on the new layout.
        private static float? TrainingSecondsLeft(TrainingOrganizer training)
        {
            if (!training.IsTraining || training.Instances.Count == 0)
                return null;

            TrainingInstance instance = training.Instances[0];
            if (instance == null || !instance.EpisodeRunning)
                return null;

            float left = instance.Timer.Remaining;
            Epoch epoch = training.CurrentEpoch;
            if (epoch != null && epoch.Timer.HasLimit)
                left = Mathf.Min(left, epoch.Timer.Remaining);

            return float.IsInfinity(left) ? (float?)null : left;
        }

        private static float? EvaluationSecondsLeft(EvaluationOrganizer evaluation)
        {
            if (!evaluation.IsEvaluating || evaluation.Instances.Count == 0)
                return null;

            EvaluationInstance instance = evaluation.Instances[0];
            if (instance == null || !instance.AttemptRunning || !instance.Timer.HasLimit)
                return null;

            return instance.Timer.Remaining;
        }

        // Rounded up, so it reaches 00:00:00 when the episode ends.
        private static string Countdown(float seconds)
        {
            return Hms(Mathf.CeilToInt(Mathf.Max(0f, seconds)));
        }

        private static string Elapsed(float seconds)
        {
            return Hms(Mathf.FloorToInt(Mathf.Max(0f, seconds)));
        }

        private static string Hms(int total)
        {
            return $"{total / 3600:00}:{total / 60 % 60:00}:{total % 60:00}";
        }
    }
}
