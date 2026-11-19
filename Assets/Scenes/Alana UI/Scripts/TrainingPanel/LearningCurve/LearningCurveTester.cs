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
        public enum DataSource { RealReport, FixedBlocks }

        [Tooltip("Empty: uses the curve on this object, or the first one in the scene.")]
        [SerializeField] private LearningCurve curve;
        [SerializeField] private LearningCurve rewardCurve;

        [Tooltip("Optional. Shows the hovered point's details.")]
        [SerializeField] private TMP_Text hoverLabel;
        [SerializeField] private OnPlay whenGameStarts = OnPlay.Fill;

        [Header("View")]
        [Tooltip("Which metric the main line shows. Change it while playing to compare the views.")]
        [SerializeField] private CurveMetric metric = CurveMetric.SuccessRate;

        [Header("Data source")]
        [Tooltip("Real Report: fake episodes go through TrainingReport exactly like a training run, so the curve gets one point " +
                 "per episode and merges points past Max Progress Points, with the newest point at the right edge. " +
                 "Fixed Blocks: the original fake blocks below.")]
        [SerializeField] private DataSource source = DataSource.RealReport;

        [Header("Real Report")]
        [Tooltip("How many fake episodes Fill creates (the live test adds them a few at a time up to this). " +
                 "Try 2, 150, 200, 201, 450 and 5000 to see one point per episode and the pairs merging.")]
        [SerializeField, Min(1)] private int episodes = 450;
        [Tooltip("Same as TrainingOrganizer's report setting: the most points the curve holds before they merge in pairs.")]
        [SerializeField, Min(2)] private int maxProgressPoints = 200;
        [Tooltip("Same as TrainingOrganizer's report setting: each point shows the last this-many episodes up to it. 0 = off.")]
        [SerializeField, Min(0)] private int progressWindow = 20;
        [Tooltip("Live test: episodes added per tick.")]
        [SerializeField, Min(1)] private int episodesPerTick = 3;

        [Header("Fixed Blocks")]
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

        [Header("Real Report: how the model learns")]
        [Tooltip("How far through the run the model improves fastest (0 = start, 1 = end). Before it the curve is a slow start, after it a plateau.")]
        [SerializeField, Range(0f, 1f)] private float learnAt = 0.4f;
        [Tooltip("How sudden the improvement is: low = gradual, high = a sharp jump.")]
        [SerializeField, Range(1f, 20f)] private float learnSharpness = 8f;
        [Tooltip("Slow ups and downs in skill over the run, like real training: plateaus and small setbacks. 0 = a perfectly smooth trend.")]
        [SerializeField, Range(0f, 0.2f)] private float skillWander = 0.05f;
        [Tooltip("Episode length in seconds; timeouts last this long.")]
        [SerializeField, Min(1f)] private float episodeSeconds = 120f;

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

            if (source == DataSource.RealReport)
            {
                Show(target, BuildReal(episodes));
                return;
            }

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

            if (source == DataSource.RealReport)
            {
                target.SetExpectedPoints(0);
                if (rewardCurve != null) rewardCurve.SetExpectedPoints(0);
                live = StartCoroutine(LiveRealRoutine(target));
                return;
            }

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

        // --- Real Report ------------------------------------------------------

        // Feeds both curves the way TrainingMonitorView does during a run.
        private void Show(LearningCurve target, TrainingReport report)
        {
            target.SetFromReport(report);
            if (rewardCurve != null && rewardCurve != target)
                rewardCurve.SetFromReport(report);
        }

        private TrainingReport NewReport()
        {
            return new TrainingReport
            {
                IterationUnit = "Episodes",
                IterationsPlanned = episodes,
                Settings = new TrainingReportSettings { MaxProgressPoints = maxProgressPoints, ProgressWindow = progressWindow }
            };
        }

        // Fake episodes 1..count, recalculated once at the end.
        private TrainingReport BuildReal(int count)
        {
            var run = new FakeRun(this);
            TrainingReport report = NewReport();
            for (int i = 0; i < count; i++)
                report.AttemptLog.Add(run.Next(i));
            report.Recalculate(null);
            return report;
        }

        // Adds episodes a few at a time and recalculates after each one, like a real run.
        private IEnumerator LiveRealRoutine(LearningCurve target)
        {
            var wait = new WaitForSecondsRealtime(liveInterval);
            var run = new FakeRun(this);
            TrainingReport report = NewReport();

            for (int i = 0; i < episodes; i++)
            {
                report.AttemptLog.Add(run.Next(i));
                report.Recalculate(null);
                if ((i + 1) % episodesPerTick == 0 || i == episodes - 1)
                {
                    Show(target, report);
                    yield return wait;
                }
            }

            live = null;
        }

        // Generates a believable run, one episode at a time.
        //  - Skill (the chance of reaching the goal) follows an S-curve: a slow start, a learning phase around
        //    "Learn At", then a plateau. On top of that it wanders slowly (plateaus, small setbacks), the way
        //    real training does. It never jumps from one episode to the next.
        //  - Each episode then succeeds or fails at random with that skill, like a real rover. So with one
        //    episode per point the success line still goes 0% / 100%: that part is real, not the tester.
        //  - Reward is built the way the rover's reward is: progress toward the goal, minus a time penalty,
        //    plus +1 for the goal or -1 for flipping / leaving the map (timeouts get no end penalty).
        //    Failed episodes still earn progress, and more of it as skill rises, so reward climbs smoothly.
        private class FakeRun
        {
            private readonly LearningCurveTester t;
            private readonly System.Random random;
            private float wander;

            public FakeRun(LearningCurveTester tester)
            {
                t = tester;
                random = new System.Random(tester.seed);
            }

            public TrainingAttempt Next(int index)
            {
                float progress = t.episodes > 1 ? Mathf.Clamp01(index / (float)(t.episodes - 1)) : 0f;

                // Slow random drift, about +/- skillWander, changing over roughly 50 episodes.
                wander = wander * 0.98f + Gaussian() * t.skillWander * 0.2f;
                float skill = Mathf.Clamp(Mathf.Lerp(t.successAtStart, t.successAtEnd, SCurve(progress)) + wander, 0.02f, 0.98f);

                bool success = random.NextDouble() < skill;
                EpisodeOutcome outcome = EpisodeOutcome.ReachedGoal;
                bool hitRock = false;
                float reached; // share of the distance to the goal covered
                float seconds;

                if (success)
                {
                    reached = 1f;
                    seconds = Mathf.Clamp(Mathf.Lerp(0.6f, 0.3f, skill) * t.episodeSeconds + Gaussian() * 4f, 10f, t.episodeSeconds);
                }
                else
                {
                    float timeoutShare = Mathf.Lerp(t.timeoutShareStart, t.timeoutShareEnd, progress);
                    float collisionShare = Mathf.Lerp(t.collisionShareStart, t.collisionShareEnd, progress);
                    double pick = random.NextDouble();
                    outcome = pick < timeoutShare ? EpisodeOutcome.TimedOut
                            : pick < timeoutShare + (1f - timeoutShare) * 0.5f ? EpisodeOutcome.Flipped
                            : EpisodeOutcome.OutOfBounds;
                    hitRock = random.NextDouble() < collisionShare;

                    reached = Mathf.Clamp(skill * 0.8f + Gaussian() * 0.15f, 0f, 0.95f);
                    seconds = outcome == EpisodeOutcome.TimedOut
                        ? t.episodeSeconds
                        : Mathf.Lerp(0.1f, 0.8f, (float)random.NextDouble()) * t.episodeSeconds;
                }

                float terminal = success ? 1f : outcome == EpisodeOutcome.TimedOut ? 0f : -1f;
                float reward = 1.5f * reached - 0.01f * seconds + terminal + Gaussian() * 0.05f;

                const float startDistance = 40f;
                TrainingAttempt attempt = TrainingAttempt.From(outcome, seconds, reward, startDistance, startDistance * (1f - reached),
                                                               hitRock, false, Vector3.zero, Vector3.zero);
                attempt.Episode = index + 1;
                attempt.Tile = "Test";
                return attempt;
            }

            // 0 at the start of the run, 1 at the end, steepest at Learn At.
            private float SCurve(float x)
            {
                float k = t.learnSharpness, m = t.learnAt;
                float f0 = Logistic(-k * m), f1 = Logistic(k * (1f - m));
                return (Logistic(k * (x - m)) - f0) / Mathf.Max(1e-5f, f1 - f0);
            }

            private static float Logistic(float x) => 1f / (1f + Mathf.Exp(-x));

            // Standard normal (Box-Muller).
            private float Gaussian()
            {
                double u1 = 1.0 - random.NextDouble(), u2 = random.NextDouble();
                return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2));
            }
        }

        // --- Fixed Blocks -----------------------------------------------------

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