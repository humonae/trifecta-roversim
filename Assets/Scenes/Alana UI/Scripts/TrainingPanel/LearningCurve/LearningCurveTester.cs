using System.Collections;
using System.Collections.Generic;
using Evaluation;
using TMPro;
using UnityEngine;

namespace Training
{
    // Fills a LearningCurve with fake progress blocks so you can check how it looks without
    // running a training. In Play mode: change "Metric" below to switch the view, or right-click
    // this component's header and use the commands. Delete it when you're done.
    public class LearningCurveTester : MonoBehaviour
    {
        public enum OnPlay { Nothing, Fill, Live }

        [Tooltip("Empty: uses the curve on this object, or the first one in the scene.")]
        [SerializeField] private LearningCurve curve;
        [SerializeField] private LearningCurve rewardCurve;

        [Tooltip("Optional. Shows the hovered point's details.")]
        [SerializeField] private TMP_Text hoverLabel;
        [SerializeField] private OnPlay whenGameStarts = OnPlay.Fill;

        [Header("View")]
        [Tooltip("Which metric the main line shows. Change it while playing to compare the views.")]
        [SerializeField] private CurveMetric metric = CurveMetric.SuccessRate;

        [Header("Fake data")]
        [SerializeField, Min(1)] private int blocks = 20;
        [SerializeField, Min(2)] private int blockSize = 50;
        [SerializeField, Range(0f, 1f)] private float successAtStart = 0.15f;
        [SerializeField, Range(0f, 1f)] private float successAtEnd = 0.8f;
        [Tooltip("Make the last block only half full, like a run still in progress.")]
        [SerializeField] private bool lastBlockPartial = true;
        [SerializeField] private int seed = 7;

        [Header("What the failures are (share of failed episodes)")]
        [Tooltip("Rock hits. Starts high and falls: the rover learns to avoid rocks.")]
        [SerializeField, Range(0f, 1f)] private float collisionShareStart = 0.6f;
        [SerializeField, Range(0f, 1f)] private float collisionShareEnd = 0.2f;
        [Tooltip("Timeouts. Starts lower and falls slowly: the rover still wanders sometimes.")]
        [SerializeField, Range(0f, 1f)] private float timeoutShareStart = 0.3f;
        [SerializeField, Range(0f, 1f)] private float timeoutShareEnd = 0.6f;

        [Header("Live test")]
        [SerializeField, Min(0.05f)] private float liveInterval = 0.4f;

        private Coroutine live;

        private void OnEnable()
        {
            LearningCurve target = Curve(false);
            if (target != null)
                target.PointHovered += OnHovered;
        }

        private void OnDisable()
        {
            StopLive();
            LearningCurve target = Curve(false);
            if (target != null)
                target.PointHovered -= OnHovered;
        }

        private void Start()
        {
            LearningCurve target = Curve(false);
            if (target != null)
                target.SetMetric(metric);

            if (whenGameStarts == OnPlay.Fill)
                Fill();
            else if (whenGameStarts == OnPlay.Live)
                StartLive();
        }

        // Changing the Metric dropdown in the Inspector while playing switches the view.
        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            LearningCurve target = Curve(false);
            if (target != null)
                target.SetMetric(metric);
        }

        [ContextMenu("Cycle metric")]
        public void CycleMetric()
        {
            metric = (CurveMetric)(((int)metric + 1) % 4);
            LearningCurve target = Curve(true);
            if (target != null)
                target.SetMetric(metric);
            Debug.Log($"[LearningCurveTester] Metric: {metric}", this);
        }

        [ContextMenu("Fill with test data")]
        public void Fill()
        {
            LearningCurve target = Curve(true);
            if (target == null) return;

            StopLive();
            target.SetMetric(metric);
            target.SetExpectedPoints(0);
            TrainingReport report = Build(blocks);
            target.SetFromReport(report);
            if (rewardCurve != null)
            {
                rewardCurve.SetExpectedPoints(0);
                rewardCurve.SetFromReport(report);
            }
        }

        [ContextMenu("Start live test (Play mode)")]
        public void StartLive()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[LearningCurveTester] The live test needs Play mode.", this);
                return;
            }

            LearningCurve target = Curve(true);
            if (target == null) return;

            StopLive();
            target.SetMetric(metric);
            target.SetExpectedPoints(blocks); // fixed x-axis: the curve fills in from the left
            if (rewardCurve != null)
                rewardCurve.SetExpectedPoints(blocks);
            live = StartCoroutine(LiveRoutine(target));
        }

        [ContextMenu("Stop live test")]
        public void StopLive()
        {
            if (live != null)
            {
                StopCoroutine(live);
                live = null;
            }
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            LearningCurve target = Curve(true);
            if (target == null) return;

            StopLive();
            target.Clear();
        }

        private IEnumerator LiveRoutine(LearningCurve target)
        {
            var wait = new WaitForSecondsRealtime(liveInterval);
            for (int count = 1; count <= blocks; count++)
            {
                TrainingReport report = Build(count);
                target.SetFromReport(report);
                if (rewardCurve != null)
                    rewardCurve.SetFromReport(report);
                yield return wait;
            }

            live = null;
        }

        // A report holding the first `count` blocks.
        private TrainingReport Build(int count)
        {
            var random = new System.Random(seed);
            var report = new TrainingReport();
            report.Progress = new List<TrainingReport.Stats>();

            for (int i = 0; i < count; i++)
            {
                float progress = blocks > 1 ? i / (float)(blocks - 1) : 0f;
                float chance = Mathf.Lerp(successAtStart, successAtEnd, progress);
                int n = (lastBlockPartial && i == blocks - 1 && count == blocks) ? Mathf.Max(1, blockSize / 2) : blockSize;

                int successes = 0;
                for (int e = 0; e < n; e++)
                    if (random.NextDouble() < chance) successes++;

                int failures = n - successes;
                float timeoutShare = Mathf.Lerp(timeoutShareStart, timeoutShareEnd, progress);
                float collisionShare = Mathf.Lerp(collisionShareStart, collisionShareEnd, progress);
                int timedOut = Mathf.Clamp(Mathf.RoundToInt(failures * timeoutShare), 0, failures);
                int collisions = Mathf.Clamp(Mathf.RoundToInt(failures * collisionShare), 0, failures);

                float low, high;
                EvaluationReport.Wilson(successes, n, out low, out high);

                report.Progress.Add(new TrainingReport.Stats
                {
                    FirstEpisode = i * blockSize + 1,
                    LastEpisode = i * blockSize + n,
                    Episodes = n,
                    Successes = successes,
                    SuccessRate = successes / (float)n,
                    SuccessRateLow = low,
                    SuccessRateHigh = high,
                    Collisions = collisions,
                    CollisionRate = collisions / (float)n,
                    TimedOut = timedOut,
                    TimeoutRate = timedOut / (float)n,
                    AverageSecondsToGoal = Mathf.Lerp(60f, 32f, progress),
                    AverageReward = Mathf.Lerp(-2f, 6f, chance) + (float)(random.NextDouble() - 0.5) * 0.6f,
                    Failures = failures,
                    FailedTimedOut = timedOut,
                    FailedHitRock = collisions
                });
            }

            return report;
        }

        private void OnHovered(TrainingReport.Stats s)
        {
            if (hoverLabel != null && curve != null)
                hoverLabel.text = s != null ? curve.Label(s) : "";
        }

        private LearningCurve Curve(bool warn)
        {
            if (curve == null)
                curve = GetComponent<LearningCurve>();
            if (curve == null)
                curve = FindAnyObjectByType<LearningCurve>();
            if (curve == null && warn)
                Debug.LogWarning("[LearningCurveTester] No LearningCurve found. Assign one in the Curve field.", this);
            return curve;
        }
    }
}