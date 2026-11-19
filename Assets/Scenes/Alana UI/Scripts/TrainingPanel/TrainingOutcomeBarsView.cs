using Evaluation;
using UnityEngine;

namespace Training
{
    // The Training panel's version of OutcomeBarsView. Put it on the object that holds the rows.
    // Training finishes episodes very fast, so the bars are refreshed at most every refreshInterval.
    public class TrainingOutcomeBarsView : MonoBehaviour
    {
        public enum Window { Recent, Overall }

        public enum Scale { OfAllEpisodes, OfFailures, OfLargestOutcome }

        [SerializeField] private TrainingOrganizer organizer;
        [Tooltip("Recent: the latest episodes, so you can see whether it's improving. Overall: the whole run.")]
        [SerializeField] private Window window = Window.Recent;
        [SerializeField] private Scale scale = Scale.OfAllEpisodes;
        [SerializeField, Min(0f)] private float refreshInterval = 0.2f;
        [Tooltip("Leave empty to use every OutcomeBar under this object.")]
        [SerializeField] private OutcomeBar[] bars;

        private TrainingReport latest;
        private bool dirty;
        private float nextRefresh;

        private void Awake()
        {
            if (organizer == null)
                organizer = FindAnyObjectByType<TrainingOrganizer>();

            if (bars == null || bars.Length == 0)
                bars = GetComponentsInChildren<OutcomeBar>(true);
        }

        private void OnEnable()
        {
            if (organizer == null)
                return;

            organizer.RunStarted += OnRunStarted;
            organizer.EpisodeFinished += OnEpisodeFinished;
            organizer.Finished += OnFinished;

            if (organizer.Report != null)
                Show(organizer.Report);
        }

        private void OnDisable()
        {
            if (organizer == null)
                return;

            organizer.RunStarted -= OnRunStarted;
            organizer.EpisodeFinished -= OnEpisodeFinished;
            organizer.Finished -= OnFinished;
        }

        private void Update()
        {
            if (dirty && Time.unscaledTime >= nextRefresh)
                Show(latest);
        }

        /// <summary>For a Recent / Overall toggle button.</summary>
        public void ShowRecent() { window = Window.Recent; if (latest != null) Show(latest); }

        /// <summary>For a Recent / Overall toggle button.</summary>
        public void ShowOverall() { window = Window.Overall; if (latest != null) Show(latest); }

        private void OnRunStarted(TrainingReport report) => Show(report);

        // The report already includes this episode when the event fires.
        private void OnEpisodeFinished(TrainingAttempt attempt, TrainingReport report)
        {
            latest = report;
            dirty = true;
        }

        private void OnFinished(TrainingReport report, string savedPath) => Show(report);

        private void Show(TrainingReport r)
        {
            latest = r;
            dirty = false;
            nextRefresh = Time.unscaledTime + refreshInterval;
            Refresh(r);
        }

        private void Refresh(TrainingReport r)
        {
            if (r == null || bars == null)
                return;

            TrainingReport.Stats s = window == Window.Recent ? r.Recent : r.Overall;
            if (s == null)
                return;

            int largest = 0;
            if (scale == Scale.OfLargestOutcome)
            {
                foreach (OutcomeBar bar in bars)
                {
                    if (bar != null)
                        largest = Mathf.Max(largest, CountFor(s, bar.Outcome));
                }
            }

            foreach (OutcomeBar bar in bars)
            {
                if (bar == null)
                    continue;

                int denominator;
                switch (scale)
                {
                    case Scale.OfFailures: denominator = s.Failures; break;
                    case Scale.OfLargestOutcome: denominator = largest; break;
                    default: denominator = s.Episodes; break;
                }

                bar.Set(CountFor(s, bar.Outcome), denominator);
            }
        }

        private static int CountFor(TrainingReport.Stats s, OutcomeBar.Kind kind)
        {
            switch (kind)
            {
                case OutcomeBar.Kind.Success: return s.Successes;
                case OutcomeBar.Kind.TimedOut: return s.FailedTimedOut;
                case OutcomeBar.Kind.Flipped: return s.FailedFlipped;
                case OutcomeBar.Kind.OutOfBounds: return s.FailedOutOfBounds;
                default: return s.FailedHitRock;
            }
        }

        
    }
}