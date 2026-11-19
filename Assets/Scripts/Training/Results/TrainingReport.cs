using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Evaluation;

namespace Training
{
    // One training run, in the same shape as Evaluation.EvaluationReport (run details, per-tile results,
    // one row per episode), with the numbers tracked three ways using the same Stats:
    //  - Overall:  every episode so far;
    //  - Recent:   the latest episodes (TrainingReportSettings: the last N, or the last N% of the run),
    //              so "recent vs overall" shows how the model is learning;
    //  - Progress: the learning curve. One point per episode until there are more than
    //              TrainingReportSettings.MaxProgressPoints; then one every 2, 4, 8 ... episodes.
    //              Each point covers the last ProgressWindow episodes up to it (a rolling window).
    // The comparison with the previous training run uses the recent success rate.
    // Rates are 0-1. Times are -1 when there is nothing to average (JSON has no NaN).
    [Serializable]
    public class TrainingReport
    {
        // The same numbers for any range of episodes. FirstEpisode/LastEpisode are 1-based and inclusive.
        [Serializable]
        public class Stats
        {
            public int FirstEpisode;
            public int LastEpisode;
            public int Episodes;

            public int Successes;
            public float SuccessRate;
            public float SuccessRateLow;
            public float SuccessRateHigh;

            public int Collisions;
            public float CollisionRate;
            public int TimedOut;
            public float TimeoutRate;

            public float AverageSecondsToGoal = -1f;
            public float MedianSecondsToGoal = -1f;
            public float AverageReward;

            // Why episodes failed. HitRock overlaps the others (a rover can hit a rock and then time out).
            public int Failures;
            public int FailedHitRock;
            public int FailedTimedOut;
            public int FailedFlipped;
            public int FailedOutOfBounds;
        }

        [Serializable]
        public class TileResult
        {
            public string Tile = "";
            public int Episodes;
            public int Successes;
            public float SuccessRate;
            public float AverageSecondsToGoal = -1f;
        }

        // Run details
        public int RunNumber;
        public string RunId = "";
        public string Model = "";
        public string ModelPath = "";
        public string MapName = "";
        public string StartTile = "";
        public string Network = "";
        public string ResumedFrom = "";
        public long ResumedFromStep;
        public string StartedAt = "";
        public string FinishedAt = "";
        public int Seed;
        public float RockDensity;
        public float EpisodeSeconds;
        public float EpochSeconds;
        public bool NewTileEachEpoch;
        public int Rovers;
        public string IterationUnit = "";
        public int IterationsPlanned;
        public long MaxTrainerSteps;
        public bool EarlyStopping;
        public float EarlyStoppingTarget;
        // Completed: it reached its iteration target, step budget or early-stopping goal.
        public bool Completed;
        public bool StoppedByError;
        public string StopReason = "";
        public float SimulatedSeconds;
        public int Epochs;

        // What "recent" and the progress blocks mean for this run.
        public TrainingReportSettings Settings = new TrainingReportSettings();
        // e.g. "last 10% of the run (at least 100)"
        public string RecentWindow = "";

        public Stats Overall = new Stats();
        public Stats Recent = new Stats();
        public List<Stats> Progress = new List<Stats>();

        // Change in the recent success rate from the previous training run, in percentage points.
        public bool HasPrevious;
        public int PreviousRunNumber;
        public float SuccessChangePoints;

        public List<TileResult> Tiles = new List<TileResult>();
        public List<TrainingAttempt> AttemptLog = new List<TrainingAttempt>();

        public int Episodes => AttemptLog.Count;

        public static TrainingReport Create(TrainingRequest request, int runNumber, int rovers, TrainingReportSettings settings,
            TrainingReport previous, DateTime started)
        {
            CheckpointNetwork checkpoint = request.Checkpoint != null && request.Checkpoint.IsSet ? request.Checkpoint : null;
            settings = settings ?? new TrainingReportSettings();
            return new TrainingReport
            {
                RunNumber = runNumber,
                RunId = request.RunId,
                Model = string.IsNullOrEmpty(request.ModelOutputPath) ? request.RunName : Path.GetFileNameWithoutExtension(request.ModelOutputPath),
                MapName = request.MapName,
                StartTile = request.StartTile,
                Network = checkpoint != null
                    ? $"{checkpoint.HiddenUnits} x {checkpoint.Layers} (from checkpoint)"
                    : request.NetworkPreset,
                ResumedFrom = request.ResumeFrom ?? "",
                ResumedFromStep = checkpoint != null ? checkpoint.Step : 0,
                StartedAt = started.ToString("s", CultureInfo.InvariantCulture),
                Seed = request.Seed,
                RockDensity = request.RocksEnabled ? request.RockDensity : 0f,
                EpisodeSeconds = request.EpisodeSeconds,
                EpochSeconds = request.EpochSeconds,
                NewTileEachEpoch = request.NewTileEachEpoch,
                Rovers = rovers,
                IterationUnit = request.IterationUnit.ToString(),
                IterationsPlanned = request.MaxIterations,
                MaxTrainerSteps = request.MaxTrainerSteps,
                EarlyStopping = request.EarlyStopping,
                EarlyStoppingTarget = request.SuccessTarget,
                Settings = Copy(settings),
                RecentWindow = settings.Describe(),
                HasPrevious = previous != null,
                PreviousRunNumber = previous != null ? previous.RunNumber : 0
            };
        }

        public DateTime StartedTime => Parse(StartedAt);

        // "model - Jezero B4 - 1,240 episodes - seed 12345"
        public string Details => $"{Model} - {MapName} {StartTile} - {Episodes:N0} episodes - seed {Seed}";

        // Runs list card
        public string CardLine => $"{Network} - {Episodes:N0} episodes, recent success {EvaluationReport.Percent(Recent.SuccessRate)}";

        public void Record(TrainingAttempt attempt, TrainingReport previous)
        {
            AttemptLog.Add(attempt);
            Recalculate(previous);
        }

        public void Finish(bool completed, bool error, string reason, DateTime finished, float simulatedSeconds, int epochs,
            string modelPath, TrainingReport previous)
        {
            Completed = completed;
            StoppedByError = error;
            StopReason = reason ?? "";
            FinishedAt = finished.ToString("s", CultureInfo.InvariantCulture);
            SimulatedSeconds = simulatedSeconds;
            Epochs = epochs;
            ModelPath = modelPath ?? "";
            Recalculate(previous);
        }

        public void Recalculate(TrainingReport previous)
        {
            int count = AttemptLog.Count;
            Overall = Compute(0, count);
            Recent = Compute(count - Settings.RecentCount(count), count);
            SuccessChangePoints = previous != null ? (Recent.SuccessRate - previous.Recent.SuccessRate) * 100f : 0f;

            // A point every `block` episodes; each one covers the `window` episodes ending at it
            // (fewer at the start of the run). Compute clamps the start to episode 1.
            int block = Settings.ProgressBlockSize(count);
            int window = Settings.ProgressWindowFor(block);
            Progress.Clear();
            for (int start = 0; start < count; start += block)
            {
                int end = Math.Min(count, start + block);
                Progress.Add(Compute(end - window, end));
            }

            Tiles = ComputeTiles();
        }

        // Episodes [start, end), 0-based. Plain loops: this runs after every episode.
        public Stats Compute(int start, int end)
        {
            start = Math.Max(0, start);
            end = Math.Min(AttemptLog.Count, end);
            var stats = new Stats { FirstEpisode = start + 1, LastEpisode = end, Episodes = Math.Max(0, end - start) };
            double reward = 0, goalSeconds = 0;
            var successSeconds = new List<float>();

            for (int i = start; i < end; i++)
            {
                TrainingAttempt a = AttemptLog[i];
                reward += a.Reward;
                if (a.HitRock)
                    stats.Collisions++;
                if (a.Outcome == TimedOutName)
                    stats.TimedOut++;

                if (a.Success)
                {
                    stats.Successes++;
                    goalSeconds += a.Seconds;
                    successSeconds.Add(a.Seconds);
                    continue;
                }

                stats.Failures++;
                if (a.HitRock)
                    stats.FailedHitRock++;
                if (a.Outcome == FlippedName)
                    stats.FailedFlipped++;
                else if (a.Outcome == OutOfBoundsName)
                    stats.FailedOutOfBounds++;
            }

            int n = stats.Episodes;
            stats.SuccessRate = Rate(stats.Successes, n);
            EvaluationReport.Wilson(stats.Successes, n, out stats.SuccessRateLow, out stats.SuccessRateHigh);
            stats.CollisionRate = Rate(stats.Collisions, n);
            stats.TimeoutRate = Rate(stats.TimedOut, n);
            stats.FailedTimedOut = stats.TimedOut;
            stats.AverageReward = n > 0 ? (float)(reward / n) : 0f;
            stats.AverageSecondsToGoal = stats.Successes > 0 ? (float)(goalSeconds / stats.Successes) : -1f;
            stats.MedianSecondsToGoal = Median(successSeconds);
            return stats;
        }

        private List<TileResult> ComputeTiles()
        {
            var tiles = new List<TileResult>();
            var index = new Dictionary<string, int>();
            var goalSeconds = new List<double>();
            foreach (TrainingAttempt a in AttemptLog)
            {
                string key = a.Tile ?? "";
                int i;
                if (!index.TryGetValue(key, out i))
                {
                    i = tiles.Count;
                    index.Add(key, i);
                    tiles.Add(new TileResult { Tile = key });
                    goalSeconds.Add(0);
                }

                tiles[i].Episodes++;
                if (a.Success)
                {
                    tiles[i].Successes++;
                    goalSeconds[i] += a.Seconds;
                }
            }

            for (int i = 0; i < tiles.Count; i++)
            {
                tiles[i].SuccessRate = Rate(tiles[i].Successes, tiles[i].Episodes);
                tiles[i].AverageSecondsToGoal = tiles[i].Successes > 0 ? (float)(goalSeconds[i] / tiles[i].Successes) : -1f;
            }

            return tiles;
        }

        public string Summary()
        {
            var text = new StringBuilder();
            text.AppendLine($"Training run {RunNumber}: {Details}{(Completed ? "" : StoppedByError ? " (stopped by an error)" : " (stopped early)")}");
            text.AppendLine($"Network: {Network}{(string.IsNullOrEmpty(ResumedFrom) ? "" : $", resumed from {Path.GetFileName(ResumedFrom)} (step {ResumedFromStep:N0})")}; " +
                            $"{Rovers} rover(s), {Epochs} epoch(s), {SimulatedSeconds / 60f:0.0} simulated minutes");
            text.AppendLine(CompareTable());
            if (HasPrevious)
                text.AppendLine($"Recent success vs training run {PreviousRunNumber}: {EvaluationReport.Points(SuccessChangePoints)}");
            if (!Completed && !string.IsNullOrEmpty(StopReason))
                text.AppendLine($"Reason: {StopReason}");
            return text.ToString().TrimEnd();
        }

        // Overall vs recent, side by side.
        public string CompareTable()
        {
            var text = new StringBuilder();
            string all = $"All {Overall.FirstEpisode}-{Overall.LastEpisode}";
            string recent = $"Recent {Recent.FirstEpisode}-{Recent.LastEpisode}";
            text.AppendLine($"{"",-14}  {all,-30}  {recent,-30}   ({RecentWindow})");
            Row(text, "Success", s => $"{EvaluationReport.Percent(s.SuccessRate)} ({EvaluationReport.Percent(s.SuccessRateLow)}-{EvaluationReport.Percent(s.SuccessRateHigh)})");
            Row(text, "Collisions", s => $"{EvaluationReport.Percent(s.CollisionRate)} ({s.Collisions})");
            Row(text, "Timeouts", s => $"{EvaluationReport.Percent(s.TimeoutRate)} ({s.TimedOut})");
            Row(text, "Time to goal", s => $"{EvaluationReport.Seconds(s.AverageSecondsToGoal)} avg, {EvaluationReport.Seconds(s.MedianSecondsToGoal)} med");
            Row(text, "Avg reward", s => s.AverageReward.ToString("0.00", CultureInfo.InvariantCulture));
            Row(text, "Why it failed", s => $"rock {s.FailedHitRock}, flip {s.FailedFlipped}, out {s.FailedOutOfBounds}");
            return text.ToString().TrimEnd();
        }

        private void Row(StringBuilder text, string label, Func<Stats, string> cell)
        {
            text.AppendLine($"{label,-14}  {cell(Overall),-30}  {cell(Recent),-30}");
        }

        // The learning curve, one row per block.
        public string ProgressTable()
        {
            var text = new StringBuilder();
            text.AppendLine("Episodes        Success  Collisions  Timeouts  Avg reward  Time to goal");
            foreach (Stats b in Progress)
            {
                string range = $"{b.FirstEpisode}-{b.LastEpisode}";
                text.AppendLine($"{range,-14}  {EvaluationReport.Percent(b.SuccessRate),7}  {EvaluationReport.Percent(b.CollisionRate),10}  " +
                                $"{EvaluationReport.Percent(b.TimeoutRate),8}  {b.AverageReward,10:0.00}  {EvaluationReport.Seconds(b.AverageSecondsToGoal),12}");
            }

            return text.ToString().TrimEnd();
        }

        private static readonly string TimedOutName = EpisodeOutcome.TimedOut.ToString();
        private static readonly string FlippedName = EpisodeOutcome.Flipped.ToString();
        private static readonly string OutOfBoundsName = EpisodeOutcome.OutOfBounds.ToString();

        private static TrainingReportSettings Copy(TrainingReportSettings s)
        {
            return new TrainingReportSettings
            {
                RecentMode = s.RecentMode,
                RecentEpisodes = s.RecentEpisodes,
                RecentPercent = s.RecentPercent,
                RecentMinimum = s.RecentMinimum,
                MaxProgressPoints = s.MaxProgressPoints,
                ProgressWindow = s.ProgressWindow,
                AutosaveEveryEpisodes = s.AutosaveEveryEpisodes
            };
        }

        private static float Rate(int count, int total)
        {
            return total > 0 ? (float)count / total : 0f;
        }

        private static float Median(List<float> values)
        {
            if (values.Count == 0)
                return -1f;

            values.Sort();
            int middle = values.Count / 2;
            return values.Count % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) * 0.5f;
        }

        private static DateTime Parse(string text)
        {
            DateTime value;
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value) ? value : DateTime.MinValue;
        }
    }
}
