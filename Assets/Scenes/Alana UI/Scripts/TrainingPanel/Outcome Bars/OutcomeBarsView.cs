using UnityEngine;
using Training;

namespace Evaluation
{
    // Feeds a group of OutcomeBar rows from the organizer's report, live: when a run starts,
    // after every episode, and when it finishes. Put it on the object that holds the rows.
    public class OutcomeBarsView : MonoBehaviour
    {
        public enum Scale
        {
            [Tooltip("A full bar means every episode ended that way. Bars add up to at most the whole track.")]
            OfAllEpisodes,
            [Tooltip("A full bar means every failure was this cause.")]
            OfFailures,
            [Tooltip("The most common outcome in this group fills the track; the others are sized against it. Easiest to read when counts are small.")]
            OfLargestOutcome
        }

        [SerializeField] private EvaluationOrganizer organizer;
        [SerializeField] private Scale scale = Scale.OfAllEpisodes;
        [Tooltip("Leave empty to use every OutcomeBar under this object.")]
        [SerializeField] private OutcomeBar[] bars;

        private void Awake()
        {
            if (organizer == null)
                organizer = FindAnyObjectByType<EvaluationOrganizer>();

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
                Refresh(organizer.Report);
        }

        private void OnDisable()
        {
            if (organizer == null)
                return;

            organizer.RunStarted -= OnRunStarted;
            organizer.EpisodeFinished -= OnEpisodeFinished;
            organizer.Finished -= OnFinished;
        }

        private void OnRunStarted(EvaluationReport report) => Refresh(report);

        // The report already includes this episode when the event fires.
        private void OnEpisodeFinished(AttemptResult attempt, EvaluationReport report) => Refresh(report);

        private void OnFinished(EvaluationReport report, string savedPath) => Refresh(report);

        private void Refresh(EvaluationReport r)
        {
            if (r == null || bars == null)
                return;

            int largest = 0;
            if (scale == Scale.OfLargestOutcome)
            {
                foreach (OutcomeBar bar in bars)
                {
                    if (bar != null)
                        largest = Mathf.Max(largest, CountFor(r, bar.Outcome));
                }
            }

            foreach (OutcomeBar bar in bars)
            {
                if (bar == null)
                    continue;

                int denominator;
                switch (scale)
                {
                    case Scale.OfFailures: denominator = r.Failures; break;
                    case Scale.OfLargestOutcome: denominator = largest; break;
                    default: denominator = r.Episodes; break;
                }

                bar.Set(CountFor(r, bar.Outcome), denominator);
            }
        }

        private static int CountFor(EvaluationReport r, OutcomeBar.Kind kind)
        {
            switch (kind)
            {
                case OutcomeBar.Kind.Success: return r.Successes;
                case OutcomeBar.Kind.TimedOut: return r.FailedTimedOut;
                case OutcomeBar.Kind.Flipped: return r.FailedFlipped;
                case OutcomeBar.Kind.OutOfBounds: return r.FailedOutOfBounds;
                default: return r.FailedHitRock;
            }
        }
    }
}