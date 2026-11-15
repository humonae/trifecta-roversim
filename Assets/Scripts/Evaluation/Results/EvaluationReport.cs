using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Training;

namespace Evaluation
{
    // Everything the Evaluation panel's Metrics and Runs sections show, for one run.
    // Rates are 0-1. Times are -1 when there is nothing to average (JSON has no NaN).
    [Serializable]
    public class EvaluationReport
    {
        public const string SkippedOutcome = "Skipped";

        [Serializable]
        public class TileResult
        {
            public string Tile = "";
            public int Episodes;
            public int Successes;
            public float SuccessRate;
            public float AverageSecondsToGoal = -1f;
        }

        // Run details header: "Run #", date, "checkpointName - selection - # episodes - seed #####"
        public int RunNumber;
        public string Model = "";
        public string ModelPath = "";
        public string Selection = "";
        public string MapName = "";
        public string TestOn = "";
        public string StartedAt = "";
        public string FinishedAt = "";
        public int Seed;
        public float ObstacleDensity;
        public float RockDensity;
        public float EpisodeSeconds;
        public bool DeterministicInference;
        public bool Completed;
        public string StopReason = "";

        public int EpisodesPlanned;
        // Episodes that ran to an outcome (skipped ones are not counted anywhere below).
        public int Episodes;
        public int Skipped;

        // "success" tile: rate plus its 95% confidence interval (Wilson score).
        public int Successes;
        public float SuccessRate;
        public float SuccessRateLow;
        public float SuccessRateHigh;

        // "Timeouts" and "Collisions" tiles: "N of Episodes".
        public int TimedOut;
        public float TimeoutRate;
        public int Collisions;
        public float CollisionRate;

        // "Time to Goal" tile, over successful episodes.
        public float AverageSecondsToGoal = -1f;
        public float MedianSecondsToGoal = -1f;

        // "Why it failed": failed episodes by cause. HitRock overlaps the others
        // (a rover can hit a rock and then time out).
        public int Failures;
        public int FailedHitRock;
        public int FailedTimedOut;
        public int FailedFlipped;
        public int FailedOutOfBounds;

        // Runs list: change in success rate from the previous run, in percentage points.
        public bool HasPrevious;
        public int PreviousRunNumber;
        public float SuccessChangePoints;

        public List<TileResult> Tiles = new List<TileResult>();
        public List<AttemptResult> AttemptLog = new List<AttemptResult>();

        public static EvaluationReport Create(EvaluationRequest request, int runNumber, EvaluationReport previous, DateTime started)
        {
            return new EvaluationReport
            {
                RunNumber = runNumber,
                Model = request.ModelName,
                ModelPath = request.ModelPath,
                Selection = request.Selection,
                MapName = request.MapName,
                TestOn = request.TestOn.ToString(),
                StartedAt = started.ToString("s", CultureInfo.InvariantCulture),
                Seed = request.Seed,
                ObstacleDensity = request.ObstacleDensity,
                RockDensity = request.RockDensity,
                EpisodeSeconds = request.EpisodeSeconds,
                DeterministicInference = request.DeterministicInference,
                EpisodesPlanned = request.Episodes,
                HasPrevious = previous != null,
                PreviousRunNumber = previous != null ? previous.RunNumber : 0
            };
        }

        public DateTime StartedTime => Parse(StartedAt);

        // "checkpointName - selection - # episodes - seed #####"
        public string Details => $"{Model} - {Selection} - {Episodes} episodes - seed {Seed}";

        // Runs list card, e.g. "Selected Tile: Jezero C6 - 50 episodes"
        public string CardLine => $"{Selection} - {Episodes} episodes";

        public void Record(AttemptResult attempt, EvaluationReport previous)
        {
            AttemptLog.Add(attempt);
            Recalculate(previous);
        }

        public void Finish(bool completed, string reason, DateTime finished, EvaluationReport previous)
        {
            Completed = completed;
            StopReason = reason ?? "";
            FinishedAt = finished.ToString("s", CultureInfo.InvariantCulture);
            Recalculate(previous);
        }

        public void Recalculate(EvaluationReport previous)
        {
            List<AttemptResult> counted = AttemptLog.Where(a => a.Outcome != SkippedOutcome).ToList();
            List<AttemptResult> succeeded = counted.Where(a => a.Success).ToList();
            List<AttemptResult> failed = counted.Where(a => !a.Success).ToList();

            Episodes = counted.Count;
            Skipped = AttemptLog.Count - counted.Count;
            Successes = succeeded.Count;
            SuccessRate = Rate(Successes, Episodes);
            Wilson(Successes, Episodes, out SuccessRateLow, out SuccessRateHigh);

            TimedOut = counted.Count(a => a.Outcome == EpisodeOutcome.TimedOut.ToString());
            TimeoutRate = Rate(TimedOut, Episodes);
            Collisions = counted.Count(a => a.HitRock);
            CollisionRate = Rate(Collisions, Episodes);

            AverageSecondsToGoal = Average(succeeded.Select(a => a.Seconds));
            MedianSecondsToGoal = Median(succeeded.Select(a => a.Seconds));

            Failures = failed.Count;
            FailedHitRock = failed.Count(a => a.HitRock);
            FailedTimedOut = TimedOut;
            FailedFlipped = failed.Count(a => a.Outcome == EpisodeOutcome.Flipped.ToString());
            FailedOutOfBounds = failed.Count(a => a.Outcome == EpisodeOutcome.OutOfBounds.ToString());

            SuccessChangePoints = previous != null ? (SuccessRate - previous.SuccessRate) * 100f : 0f;

            Tiles = counted
                .GroupBy(a => a.Tile)
                .Select(g => new TileResult
                {
                    Tile = g.Key,
                    Episodes = g.Count(),
                    Successes = g.Count(a => a.Success),
                    SuccessRate = Rate(g.Count(a => a.Success), g.Count()),
                    AverageSecondsToGoal = Average(g.Where(a => a.Success).Select(a => a.Seconds))
                })
                .ToList();
        }

        public string Summary()
        {
            var text = new StringBuilder();
            text.AppendLine($"Run {RunNumber}: {Details}{(Completed ? "" : " (stopped early)")}");
            text.AppendLine($"Success: {Percent(SuccessRate)} ({Successes}/{Episodes}, 95% CI {Percent(SuccessRateLow)}-{Percent(SuccessRateHigh)})" +
                            (HasPrevious ? $", {Points(SuccessChangePoints)} vs run {PreviousRunNumber}" : ""));
            text.AppendLine($"Time to goal: {Seconds(AverageSecondsToGoal)} average, {Seconds(MedianSecondsToGoal)} median");
            text.AppendLine($"Timeouts: {TimedOut} of {Episodes} ({Percent(TimeoutRate)})   Collisions: {Collisions} of {Episodes} ({Percent(CollisionRate)})");
            text.AppendLine($"Why it failed: hit a rock {FailedHitRock}, timed out {FailedTimedOut}, flipped {FailedFlipped}, out of bounds {FailedOutOfBounds}" +
                            (Skipped > 0 ? $"; {Skipped} skipped (no clear spawn/goal)" : ""));
            if (!Completed && !string.IsNullOrEmpty(StopReason))
                text.AppendLine($"Reason: {StopReason}");
            return text.ToString().TrimEnd();
        }

        public string TileTable()
        {
            var text = new StringBuilder();
            text.AppendLine("Tile                      Success      Avg time");
            foreach (TileResult tile in Tiles)
            {
                string name = tile.Tile.Length > 24 ? tile.Tile.Substring(0, 24) : tile.Tile;
                text.AppendLine($"{name,-24}  {tile.Successes,3}/{tile.Episodes,-3} {Percent(tile.SuccessRate),5}  {Seconds(tile.AverageSecondsToGoal),8}");
            }

            return text.ToString().TrimEnd();
        }

        public static string Percent(float rate)
        {
            return (rate * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        public static string Points(float points)
        {
            return (points >= 0f ? "+" : "") + points.ToString("0", CultureInfo.InvariantCulture) + " pts";
        }

        public static string Seconds(float seconds)
        {
            return seconds < 0f ? "n/a" : seconds.ToString("0.0", CultureInfo.InvariantCulture) + "s";
        }

        // 95% Wilson score interval; stays inside 0-1 and behaves at 0% and 100%, unlike p ± 1.96·SE.
        public static void Wilson(int successes, int trials, out float low, out float high)
        {
            if (trials <= 0)
            {
                low = 0f;
                high = 0f;
                return;
            }

            const double z = 1.959964;
            double n = trials;
            double p = successes / n;
            double z2 = z * z;
            double centre = (p + z2 / (2 * n)) / (1 + z2 / n);
            double half = z * Math.Sqrt(p * (1 - p) / n + z2 / (4 * n * n)) / (1 + z2 / n);
            low = (float)Math.Max(0.0, centre - half);
            high = (float)Math.Min(1.0, centre + half);
        }

        private static float Rate(int count, int total)
        {
            return total > 0 ? (float)count / total : 0f;
        }

        private static float Average(IEnumerable<float> values)
        {
            List<float> list = values.ToList();
            return list.Count > 0 ? list.Average() : -1f;
        }

        private static float Median(IEnumerable<float> values)
        {
            List<float> list = values.OrderBy(v => v).ToList();
            if (list.Count == 0)
                return -1f;

            int middle = list.Count / 2;
            return list.Count % 2 == 1 ? list[middle] : (list[middle - 1] + list[middle]) * 0.5f;
        }

        private static DateTime Parse(string text)
        {
            DateTime value;
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value) ? value : DateTime.MinValue;
        }
    }
}
