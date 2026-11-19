using TMPro;
using UnityEngine;

namespace Training
{
    // Draws axis labels for a LearningCurve: 0-100% on the left, episode numbers along the bottom.
    // Add it to the SAME object as the LearningCurve. It creates the text objects itself.
    // The labels sit just outside the curve's rectangle, so leave some room around the graph
    // (about 40 px on the left and 25 px below), or turn "Y Labels Outside" off to put them inside.
    [DisallowMultipleComponent]
    public class LearningCurveAxes : MonoBehaviour
    {
        [SerializeField] private LearningCurve curve;
        [Tooltip("Optional. Empty uses TextMeshPro's default font.")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField, Min(8f)] private float fontSize = 12f;
        [SerializeField] private Color color = new Color(0.72f, 0.71f, 0.68f);
        [Tooltip("Must match the Padding on the LearningCurve.")]
        [SerializeField, Min(0f)] private float padding = 8f;
        [Tooltip("On: the percent labels sit outside the left edge. Off: inside it.")]
        [SerializeField] private bool yLabelsOutside = true;
        [SerializeField] private bool showXLabels = true;
        [Tooltip("Caption under the episode numbers. Leave empty for none.")]
        [SerializeField] private string xTitle = "Episodes";

        private readonly TMP_Text[] yLabels = new TMP_Text[5];
        private readonly TMP_Text[] xLabels = new TMP_Text[3];
        private TMP_Text title;
        private int lastFirst = -1;
        private int lastLast = -1;

        private void Awake()
        {
            if (curve == null)
                curve = GetComponent<LearningCurve>();
            if (curve == null)
            {
                Debug.LogWarning("[LearningCurveAxes] Put this on the same object as the LearningCurve, or assign one.", this);
                enabled = false;
                return;
            }

            Transform parent = curve.transform;
            float lineHeight = fontSize * 1.5f;

            for (int k = 0; k <= 4; k++)
            {
                yLabels[k] = Make($"Y {k * 25}%", parent,
                    new Vector2(0f, k / 4f),
                    new Vector2(yLabelsOutside ? 1f : 0f, 0.5f),
                    new Vector2(yLabelsOutside ? -4f : padding + 4f, padding * (1f - k / 2f)),
                    new Vector2(44f, lineHeight),
                    yLabelsOutside ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft);
                yLabels[k].text = $"{k * 25}%";
            }

            for (int i = 0; i < 3; i++)
            {
                float t = i * 0.5f;
                xLabels[i] = Make($"X {i}", parent,
                    new Vector2(t, 0f), new Vector2(0.5f, 1f),
                    new Vector2(padding * (1f - 2f * t), -4f),
                    new Vector2(60f, lineHeight), TextAlignmentOptions.Top);
            }

            if (!string.IsNullOrEmpty(xTitle))
            {
                title = Make("X title", parent,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -4f - lineHeight),
                    new Vector2(140f, lineHeight), TextAlignmentOptions.Top);
                title.text = xTitle;
            }
        }

        private void LateUpdate()
        {
            // The percent labels only make sense for the rate metrics. The reward axis has no fixed scale.
            float min, max;
            curve.GetValueRange(out min, out max);
            bool reward = curve.Metric == CurveMetric.AverageReward;
            for (int k = 0; k <= 4; k++)
            {
                if (yLabels[k] == null) continue;
                float v = Mathf.Lerp(min, max, k / 4f);
                yLabels[k].text = reward
                    ? v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                    : $"{k * 25}%";
                yLabels[k].gameObject.SetActive(true);
            }
            int first = curve.AxisFirstEpisode;
            int last = curve.AxisLastEpisode;
            bool xOn = showXLabels && last > 0;

            if (first != lastFirst || last != lastLast)
            {
                lastFirst = first;
                lastLast = last;
                xLabels[0].text = first.ToString("N0");
                xLabels[1].text = Mathf.RoundToInt((first + last) * 0.5f).ToString("N0");
                xLabels[2].text = last.ToString("N0");
            }

            // With one point (or two adjacent episodes) the middle and right labels would just repeat the left one.
            int span = last - first;
            for (int i = 0; i < xLabels.Length; i++)
            {
                if (xLabels[i] == null) continue;
                bool show = xOn && (i == 0 || (i == 1 ? span >= 2 : span >= 1));
                xLabels[i].gameObject.SetActive(show);
            }
            if (title != null)
                title.gameObject.SetActive(xOn);
        }

        private TMP_Text Make(string objectName, Transform parent, Vector2 anchor, Vector2 pivot,
                              Vector2 position, Vector2 size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
                text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false; // so the labels never block the curve's hover
            return text;
        }
    }
}