using System;
using UnityEngine;

namespace Training
{
    public enum RecentWindowMode
    {
        // The last N episodes.
        Episodes,
        // The last N% of the episodes so far (at least Recent Minimum).
        PercentOfRun
    }

    // TrainingOrganizer's settings for TrainingReport.
    [Serializable]
    public class TrainingReportSettings
    {
        [Tooltip("What \"recent\" means in the report: the last N episodes, or the last N% of the run so far.")]
        public RecentWindowMode RecentMode = RecentWindowMode.PercentOfRun;
        [Tooltip("Episodes mode: how many of the latest episodes count as recent.")]
        [Min(1)] public int RecentEpisodes = 100;
        [Tooltip("Percent Of Run mode: what share of the episodes so far counts as recent.")]
        [Range(1f, 100f)] public float RecentPercent = 10f;
        [Tooltip("Percent Of Run mode: never fewer than this many episodes (or all of them, early in a run).")]
        [Min(1)] public int RecentMinimum = 100;

        [Tooltip("Most points the learning curve (TrainingReport.Progress) holds. Every episode is its own point " +
                 "until the run has more episodes than this; then neighbouring points merge in pairs " +
                 "(2 episodes per point, then 4, 8 ...) so the curve always fits.")]
        [Min(2)] public int MaxProgressPoints = 200;
        [Tooltip("Each learning-curve point shows the last this-many episodes up to it (a rolling window), so the curve follows " +
                 "the trend instead of jumping between 0% and 100%. It's never smaller than the episodes per point. " +
                 "0 = no rolling window: each point covers only its own episodes.")]
        [Min(0)] public int ProgressWindow = 20;
        [Tooltip("Save the report every this many episodes while training (0 = only at the end).")]
        [Min(0)] public int AutosaveEveryEpisodes = 500;

        // How many of the latest episodes count as recent when the run has `total` so far.
        public int RecentCount(int total)
        {
            if (total <= 0)
                return 0;

            int wanted = RecentMode == RecentWindowMode.Episodes
                ? RecentEpisodes
                : Math.Max(RecentMinimum, (int)Math.Ceiling(total * RecentPercent / 100.0));

            return Math.Min(total, Math.Max(1, wanted));
        }

        // Episodes per learning-curve point when the run has `total` episodes: 1 while they all fit,
        // then the smallest power of two that keeps the point count at or under MaxProgressPoints.
        // Doubling (rather than total / max) means points only ever merge in pairs, so the curve
        // keeps its shape as it collapses instead of shifting every block boundary each episode.
        public int ProgressBlockSize(int total)
        {
            int max = Math.Max(2, MaxProgressPoints);
            int block = 1;
            while (total > (long)block * max)
                block *= 2;
            return block;
        }

        // How many episodes each learning-curve point covers, ending at that point, when points are `block` episodes apart.
        public int ProgressWindowFor(int block)
        {
            block = Math.Max(1, block);
            return ProgressWindow > 0 ? Math.Max(ProgressWindow, block) : block;
        }

        // e.g. "last 100 episodes" or "last 10% of the run (at least 100)"
        public string Describe()
        {
            return RecentMode == RecentWindowMode.Episodes
                ? $"last {RecentEpisodes} episodes"
                : $"last {RecentPercent:0.#}% of the run (at least {RecentMinimum})";
        }
    }
}
