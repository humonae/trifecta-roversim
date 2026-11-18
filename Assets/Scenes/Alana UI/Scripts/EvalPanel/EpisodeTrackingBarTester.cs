using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Evaluation
{
    // Fills an EpisodeTrackingBar with fake outcomes so you can check how it looks and behaves
    // without running an evaluation. Right-click this component's header (or use its three-dots
    // menu) to run the test commands. Fill and Clear work in the editor too, with the game stopped.
    // Delete this component when you're done; nothing else depends on it.
    public class EpisodeTrackingBarTester : MonoBehaviour
    {
        [Tooltip("Empty: uses the bar on this object, or the first one in the scene.")]
        [SerializeField] private EpisodeTrackingBar bar;
        [Tooltip("Optional. Shows the hovered column's details, so you can test hover without the monitor panel.")]
        [SerializeField] private TMP_Text hoverLabel;

        [Header("Fake data")]
        [Tooltip("Try 1, 12, 50, 51, 120 and 1000: those are where the bar changes how it lays out.")]
        [SerializeField, Min(1)] private int episodes = 120;
        [SerializeField] private int seed = 7;
        [Tooltip("Chance of success on the first episode. It rises toward the last one, like a model that is learning.")]
        [SerializeField, Range(0f, 1f)] private float successAtStart = 0.2f;
        [SerializeField, Range(0f, 1f)] private float successAtEnd = 0.8f;
        [Header("Of the failures")]
        [SerializeField, Range(0f, 1f)] private float timedOutShare = 0.5f;
        [Tooltip("Timed out + flipped shouldn't add up to more than 1. Whatever is left over goes out of bounds.")]
        [SerializeField, Range(0f, 1f)] private float flippedShare = 0.3f;

        [Header("Live test")]
        [Tooltip("Seconds between episodes in the live test (Play mode only).")]
        [SerializeField, Min(0.01f)] private float liveInterval = 0.1f;

        private Coroutine live;

        private void OnEnable()
        {
            EpisodeTrackingBar target = Bar(false);
            if (target != null)
                target.BinHovered += OnBinHovered;
        }

        private void OnDisable()
        {
            StopLive();
            EpisodeTrackingBar target = Bar(false);
            if (target != null)
                target.BinHovered -= OnBinHovered;
        }

        // --- Commands (right-click the component header) -----------------------

        [ContextMenu("Fill with test data")]
        public void Fill()
        {
            EpisodeTrackingBar target = Bar(true);
            if (target == null) return;

            StopLive();
            target.SetOutcomes(Generate());
        }

        [ContextMenu("Start live test (Play mode)")]
        public void StartLive()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[EpisodeTrackingBarTester] The live test needs Play mode. Use \"Fill with test data\" in the editor.", this);
                return;
            }

            EpisodeTrackingBar target = Bar(true);
            if (target == null) return;

            StopLive();
            target.ClearEpisodes();
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
            EpisodeTrackingBar target = Bar(true);
            if (target == null) return;

            StopLive();
            target.ClearEpisodes();
        }

        // --- Internals ---------------------------------------------------------

        private IEnumerator LiveRoutine(EpisodeTrackingBar target)
        {
            var wait = new WaitForSecondsRealtime(liveInterval);
            foreach (BarOutcome outcome in Generate())
            {
                target.AddOutcome(outcome);
                yield return wait;
            }

            live = null;
        }

        private List<BarOutcome> Generate()
        {
            var random = new System.Random(seed);
            var outcomes = new List<BarOutcome>(episodes);

            for (int i = 0; i < episodes; i++)
            {
                float progress = episodes > 1 ? i / (float)(episodes - 1) : 0f;
                float successChance = Mathf.Lerp(successAtStart, successAtEnd, progress);

                if (random.NextDouble() < successChance)
                {
                    outcomes.Add(BarOutcome.Success);
                    continue;
                }

                double pick = random.NextDouble();
                if (pick < timedOutShare)
                    outcomes.Add(BarOutcome.TimedOut);
                else if (pick < timedOutShare + flippedShare)
                    outcomes.Add(BarOutcome.Flipped);
                else
                    outcomes.Add(BarOutcome.OutOfBounds);
            }

            return outcomes;
        }

        private void OnBinHovered(EpisodeBin? bin)
        {
            if (hoverLabel != null)
                hoverLabel.text = bin.HasValue ? bin.Value.ToString() : "";
        }

        private EpisodeTrackingBar Bar(bool warn)
        {
            if (bar == null)
                bar = GetComponent<EpisodeTrackingBar>();
            if (bar == null)
                bar = FindAnyObjectByType<EpisodeTrackingBar>();
            if (bar == null && warn)
                Debug.LogWarning("[EpisodeTrackingBarTester] No EpisodeTrackingBar found. Assign one in the Bar field.", this);
            return bar;
        }
    }
}
