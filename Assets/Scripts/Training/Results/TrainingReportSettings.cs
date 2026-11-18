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

        [Tooltip("Episodes per block of the learning curve (TrainingReport.Progress).")]
        [Min(1)] public int ProgressBlockSize = 100;
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

        // e.g. "last 100 episodes" or "last 10% of the run (at least 100)"
        public string Describe()
        {
            return RecentMode == RecentWindowMode.Episodes
                ? $"last {RecentEpisodes} episodes"
                : $"last {RecentPercent:0.#}% of the run (at least {RecentMinimum})";
        }
    }
}
