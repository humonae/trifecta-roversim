using TMPro;
using UnityEngine;

namespace Evaluation
{
    // One bar-graph row: a background (the track) with a fill image inside it.
    // The fill's WIDTH is set from a count; its height and vertical position stay as set in the editor.
    //
    // Setup: make the fill a child of the background image and stretch it to fill the background
    // (anchors at the corners). The script then changes only the fill's right edge, so it works with
    // sliced (rounded) sprites too. OutcomeBarsView calls Set() for you.
    public class OutcomeBar : MonoBehaviour
    {
        public enum Kind { Success, TimedOut, Flipped, OutOfBounds, HitRock }

        [Tooltip("Which count this bar shows. Hit a rock overlaps the other failure causes, so bars with it won't add up.")]
        [SerializeField] private Kind kind = Kind.TimedOut;

        [Header("Parts")]
        [Tooltip("The fill image's RectTransform. Must be a child of the background.")]
        [SerializeField] private RectTransform fill;
        [Tooltip("Optional. Shows the count.")]
        [SerializeField] private TMP_Text countText;
        [SerializeField] private string countFormat = "00";

        [Header("Look")]
        [Tooltip("Smallest width (0-1 of the track) shown for a count above zero, so a count of 1 is still visible. 0 = off.")]
        [SerializeField, Range(0f, 0.2f)] private float minVisibleFraction = 0.02f;
        [Tooltip("How fast the bar moves to its new size. 0 = jump straight there.")]
        [SerializeField, Min(0f)] private float smoothing = 12f;

        private float current;
        private float target;

        public Kind Outcome => kind;

        private void Awake()
        {
            Apply();
            enabled = false; // Update only runs while the bar is moving
        }

        /// <summary>Sets the bar to count / denominator of the track, and the label to the count.</summary>
        public void Set(int count, int denominator)
        {
            target = denominator > 0 ? Mathf.Clamp01(count / (float)denominator) : 0f;
            if (count > 0 && target < minVisibleFraction)
                target = minVisibleFraction;

            if (countText != null)
                countText.text = count.ToString(countFormat);

            if (smoothing <= 0f)
            {
                current = target;
                Apply();
            }
            else
            {
                enabled = true;
            }
        }

        private void Update()
        {
            // Unscaled time: evaluations can run with Time.timeScale well above 1.
            float t = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            current = Mathf.Lerp(current, target, t);

            if (Mathf.Abs(current - target) < 0.0005f)
            {
                current = target;
                enabled = false;
            }

            Apply();
        }

        private void Apply()
        {
            if (fill == null)
                return;

            Vector2 anchorMin = fill.anchorMin;
            Vector2 anchorMax = fill.anchorMax;
            Vector2 offsetMin = fill.offsetMin;
            Vector2 offsetMax = fill.offsetMax;

            anchorMin.x = 0f;
            anchorMax.x = current;
            offsetMin.x = 0f;
            offsetMax.x = 0f;

            fill.anchorMin = anchorMin;
            fill.anchorMax = anchorMax;
            fill.offsetMin = offsetMin;
            fill.offsetMax = offsetMax;
        }
    }
}