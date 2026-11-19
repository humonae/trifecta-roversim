using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Training
{
    public enum CurveMetric { SuccessRate, CollisionRate, TimeoutRate, AverageReward }

    /// <summary>
    /// uGUI learning curve for the Training panel. One point per block of episodes
    /// (TrainingReport.Progress), joined by a line, with an optional area fill, the 95% confidence band
    /// (success rate), a faint average-reward line, and a hover guide. Drawn as one mesh.
    ///
    /// Add it like the episode tracker: empty GameObject with a RectTransform under the panel's Canvas,
    /// then Add Component > UI > Learning Curve. Keep Raycast Target on for hover.
    /// Axis labels and the legend are not drawn (a mesh can't draw text): use TMP labels beside it.
    /// </summary>
    [AddComponentMenu("UI/Learning Curve")]
    public class LearningCurve : MaskableGraphic, IPointerMoveHandler, IPointerExitHandler
    {
        [SerializeField] private CurveMetric metric = CurveMetric.SuccessRate;

        [Header("Layout")]
        [SerializeField, Min(0f)] private float padding = 8f;
        [SerializeField, Min(0.5f)] private float lineWidth = 3f;
        [SerializeField, Min(0f)] private float pointRadius = 3.5f;
        [SerializeField, Min(0f)] private float gridWidth = 1f;
        [Tooltip("Reserve room for this many points so the curve fills in from the left instead of stretching. " +
                 "0 = spread whatever points exist across the full width. Use SetExpectedPoints(planned episodes / block size).")]
        [SerializeField, Min(0)] private int expectedPoints = 0;

        [Header("Look")]
        [SerializeField] private bool showArea = true;
        [SerializeField] private bool showConfidenceBand = true;
        [SerializeField, Range(0f, 1f)] private float areaAlpha = 0.18f;
        [SerializeField, Range(0f, 1f)] private float bandAlpha = 0.22f;
        [SerializeField] private Color gridColor = new Color(1f, 1f, 1f, 0.12f);
        [Tooltip("The newest block is lighter while it is still filling up.")]
        [SerializeField, Range(0f, 1f)] private float inProgressAlpha = 0.5f;

        [Header("Reward line")]
        [Tooltip("A second, thinner line for average reward, scaled to fit the graph. Shown when the metric isn't Average Reward.")]
        [SerializeField] private bool showRewardLine = true;
        [SerializeField] private Color rewardLineColor = new Color(0.62f, 0.69f, 0.77f);
        [SerializeField, Range(0.2f, 1f)] private float rewardLineWidthScale = 0.6f;

        [Header("Failure lines")]
        [Tooltip("Also draw the rock-hit and timeout rates as thinner lines on the same 0-100% axis. Only when the metric is Success Rate.")]
        [SerializeField] private bool showFailureLines = true;
        [SerializeField, Range(0.2f, 1f)] private float failureLineWidthScale = 0.6f;

        [Header("Hover")]
        [SerializeField] private bool showHoverGuide = true;
        [SerializeField] private Color guideColor = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField] private Color columnColor = new Color(1f, 1f, 1f, 0.07f);
        [SerializeField, Min(0.5f)] private float ringThickness = 1.5f;

        [Header("Colors")]
        [SerializeField] private Color successColor = new Color(0.78f, 0.94f, 0.59f);
        [SerializeField] private Color collisionColor = new Color(0.75f, 0.52f, 0.48f);
        [SerializeField] private Color timeoutColor = new Color(0.85f, 0.77f, 0.42f);
        [SerializeField] private Color rewardColor = new Color(0.55f, 0.75f, 0.95f);

        /// <summary>Raised when the hovered point changes. Null when the pointer leaves the curve.</summary>
        public event Action<TrainingReport.Stats> PointHovered;

        private readonly List<TrainingReport.Stats> blocks = new List<TrainingReport.Stats>();
        private int hover = -1;

        public int PointCount => blocks.Count;
        /// <summary>Episode at the left end of the x-axis (the end of the first block). 0 when empty.</summary>
        public int AxisFirstEpisode => blocks.Count > 0 ? blocks[0].LastEpisode : 0;
        public void GetValueRange(out float min, out float max) => ValueRange(out min, out max);

        /// <summary>Episode at the right end of the x-axis. 0 when empty.</summary>
        public int AxisLastEpisode
        {
            get
            {
                if (blocks.Count == 0) return 0;
                if (expectedPoints > blocks.Count) return expectedPoints * blocks[0].Episodes; // fixed axis
                return blocks[blocks.Count - 1].LastEpisode;
            }
        }
        public CurveMetric Metric => metric;

        // --- Feeding data ---------------------------------------------------

        /// <summary>Rebuilds the curve from the report's progress blocks.</summary>
        public void SetFromReport(TrainingReport report)
        {
            blocks.Clear();
            if (report != null && report.Progress != null)
                blocks.AddRange(report.Progress);

            if (hover >= blocks.Count) hover = -1;
            SetVerticesDirty();
        }

        public void Clear()
        {
            blocks.Clear();
            hover = -1;
            SetVerticesDirty();
        }

        /// <summary>For a metric toggle button (UnityEvent with an int): 0 success, 1 collisions, 2 timeouts, 3 reward.</summary>
        public void SetMetric(int index)
        {
            if (Enum.IsDefined(typeof(CurveMetric), index))
                SetMetric((CurveMetric)index);
        }

        public void SetMetric(CurveMetric value)
        {
            metric = value;
            SetVerticesDirty();
        }

        public void SetExpectedPoints(int count)
        {
            expectedPoints = Mathf.Max(0, count);
            SetVerticesDirty();
        }

        /// <summary>"Episodes 1201-1240: 75% success (95% CI 60%-86%), avg reward 5.0, 35.0s to goal"</summary>
        public static string Describe(TrainingReport.Stats s)
        {
            if (s == null) return "";
            return $"Episodes {s.FirstEpisode}-{s.LastEpisode}: {Evaluation.EvaluationReport.Percent(s.SuccessRate)} success " +
                $"(95% CI {Evaluation.EvaluationReport.Percent(s.SuccessRateLow)}-{Evaluation.EvaluationReport.Percent(s.SuccessRateHigh)}), " +
                $"{Evaluation.EvaluationReport.Percent(s.CollisionRate)} rock hits, " +
                $"{Evaluation.EvaluationReport.Percent(s.TimeoutRate)} timeouts, " +
                $"avg reward {s.AverageReward.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}, " +
                $"{Evaluation.EvaluationReport.Seconds(s.AverageSecondsToGoal)} to goal";
        }

        /// <summary>Describe(s), plus " (in progress)" when that is the newest, still-filling block.</summary>
        public string Label(TrainingReport.Stats s)
        {
            if (s == null) return "";
            return Describe(s) + (IsInProgress(blocks.Count - 1) && ReferenceEquals(s, blocks[blocks.Count - 1]) ? " (in progress)" : "");
        }

        // The newest block is partial when it has fewer episodes than the first (full) one.
        private bool IsInProgress(int index)
        {
            return blocks.Count > 1 && index == blocks.Count - 1 && blocks[index].Episodes < blocks[0].Episodes;
        }

        // --- Values ---------------------------------------------------------

        private float ValueOf(TrainingReport.Stats s)
        {
            switch (metric)
            {
                case CurveMetric.CollisionRate: return s.CollisionRate;
                case CurveMetric.TimeoutRate: return s.TimeoutRate;
                case CurveMetric.AverageReward: return s.AverageReward;
                default: return s.SuccessRate;
            }
        }

        private Color LineColor()
        {
            switch (metric)
            {
                case CurveMetric.CollisionRate: return collisionColor;
                case CurveMetric.TimeoutRate: return timeoutColor;
                case CurveMetric.AverageReward: return rewardColor;
                default: return successColor;
            }
        }

        // Rates are always 0-100% so a flat low curve looks flat. Reward fits the data.
        private void ValueRange(out float min, out float max)
        {
            if (metric != CurveMetric.AverageReward)
            {
                min = 0f;
                max = 1f;
                return;
            }

            RewardRange(out min, out max);
            float pad = (max - min) * 0.08f;
            min -= pad;
            max += pad;
        }

        private void RewardRange(out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            foreach (TrainingReport.Stats s in blocks)
            {
                min = Mathf.Min(min, s.AverageReward);
                max = Mathf.Max(max, s.AverageReward);
            }

            if (min > max) { min = 0f; max = 1f; }
            if (Mathf.Approximately(min, max)) { min -= 0.5f; max += 0.5f; }
        }

        // --- Hover ----------------------------------------------------------

        public void OnPointerMove(PointerEventData eventData)
        {
            Vector2 local;
            if (blocks.Count == 0 ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.enterEventCamera, out local))
            {
                SetHover(-1);
                return;
            }

            Rect r = GetPixelAdjustedRect();
            float left = r.xMin + padding;
            float width = r.width - 2f * padding;
            int slots = Mathf.Max(expectedPoints, blocks.Count);
            if (width <= 0f)
            {
                SetHover(-1);
                return;
            }

            int index = slots <= 1 ? 0 : Mathf.RoundToInt((local.x - left) / width * (slots - 1));
            SetHover(index >= 0 && index < blocks.Count ? index : -1);
        }

        public void OnPointerExit(PointerEventData eventData) => SetHover(-1);

        private void SetHover(int index)
        {
            if (index == hover) return;
            hover = index;
            SetVerticesDirty();
            PointHovered?.Invoke(index >= 0 ? blocks[index] : null);
        }

    

        // --- Mesh -----------------------------------------------------------

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect r = GetPixelAdjustedRect();
            float left = r.xMin + padding;
            float right = r.xMax - padding;
            float bottom = r.yMin + padding;
            float top = r.yMax - padding;
            if (right <= left || top <= bottom) return;

            float min, max;
            ValueRange(out min, out max);

            // Gridlines at 0, 25, 50, 75, 100% of the range.
            if (gridWidth > 0f)
            {
                for (int k = 0; k <= 4; k++)
                {
                    float y = Mathf.Lerp(bottom, top, k / 4f);
                    AddLine(vh, new Vector2(left, y), new Vector2(right, y), gridWidth, Tint(gridColor));
                }

                if (metric == CurveMetric.AverageReward && min < 0f && max > 0f)
                {
                    float y = Mathf.Lerp(bottom, top, Mathf.InverseLerp(min, max, 0f));
                    Color zero = gridColor;
                    zero.a = Mathf.Min(1f, gridColor.a * 2.5f);
                    AddLine(vh, new Vector2(left, y), new Vector2(right, y), gridWidth, Tint(zero));
                }
            }

            int n = blocks.Count;
            if (n == 0) return;

            int slots = Mathf.Max(expectedPoints, n);
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float t = slots <= 1 ? 0.5f : i / (float)(slots - 1);
                float v = Mathf.InverseLerp(min, max, ValueOf(blocks[i]));
                pts[i] = new Vector2(Mathf.Lerp(left, right, t), Mathf.Lerp(bottom, top, v));
            }

            bool partial = IsInProgress(n - 1);
            Color line = LineColor();

            // Highlighted column behind the hovered point.
            if (showHoverGuide && hover >= 0 && hover < n)
            {
                float step = slots <= 1 ? right - left : (right - left) / (slots - 1);
                float x0 = Mathf.Max(r.xMin, pts[hover].x - step * 0.5f);
                float x1 = Mathf.Min(r.xMax, pts[hover].x + step * 0.5f);
                AddQuad(vh, new Vector2(x0, bottom), new Vector2(x1, bottom), new Vector2(x1, top), new Vector2(x0, top), Tint(columnColor));
            }

            // 95% confidence band (success rate only).
            if (showConfidenceBand && metric == CurveMetric.SuccessRate && n > 1)
            {
                Color band = line;
                band.a = bandAlpha;
                for (int i = 0; i < n - 1; i++)
                {
                    float lo0 = Mathf.Lerp(bottom, top, blocks[i].SuccessRateLow);
                    float hi0 = Mathf.Lerp(bottom, top, blocks[i].SuccessRateHigh);
                    float lo1 = Mathf.Lerp(bottom, top, blocks[i + 1].SuccessRateLow);
                    float hi1 = Mathf.Lerp(bottom, top, blocks[i + 1].SuccessRateHigh);
                    AddQuad(vh, new Vector2(pts[i].x, lo0), new Vector2(pts[i + 1].x, lo1),
                            new Vector2(pts[i + 1].x, hi1), new Vector2(pts[i].x, hi0), Tint(band));
                }
            }

            // Area under the line.
            if (showArea && n > 1)
            {
                Color area = line;
                area.a = areaAlpha;
                for (int i = 0; i < n - 1; i++)
                    AddQuad(vh, new Vector2(pts[i].x, bottom), new Vector2(pts[i + 1].x, bottom), pts[i + 1], pts[i], Tint(area));
            }

            // Faint average-reward line, scaled to fit the graph (under the main line).
            if (showRewardLine && metric != CurveMetric.AverageReward && n > 1)
            {
                float rMin, rMax;
                RewardRange(out rMin, out rMax);
                float w = lineWidth * rewardLineWidthScale;
                Vector2 prev = Vector2.zero;
                for (int i = 0; i < n; i++)
                {
                    float v = Mathf.InverseLerp(rMin, rMax, blocks[i].AverageReward);
                    var p = new Vector2(pts[i].x, Mathf.Lerp(bottom, top, v));
                    if (i > 0)
                    {
                        Color c = rewardLineColor;
                        if (partial && i == n - 1) c.a *= inProgressAlpha;
                        AddLine(vh, prev, p, w, Tint(c));
                    }

                    prev = p;
                }
            }

            // Rock-hit and timeout rates, thinner, on the same 0-100% axis.
            if (showFailureLines && metric == CurveMetric.SuccessRate && n > 1)
            {
                float fw = lineWidth * failureLineWidthScale;
                for (int i = 0; i < n - 1; i++)
                {
                    float a = (partial && i == n - 2) ? inProgressAlpha : 1f;

                    Color rock = collisionColor;
                    rock.a *= a;
                    AddLine(vh, RatePoint(pts[i].x, blocks[i].CollisionRate, bottom, top),
                                RatePoint(pts[i + 1].x, blocks[i + 1].CollisionRate, bottom, top), fw, Tint(rock));

                    Color time = timeoutColor;
                    time.a *= a;
                    AddLine(vh, RatePoint(pts[i].x, blocks[i].TimeoutRate, bottom, top),
                                RatePoint(pts[i + 1].x, blocks[i + 1].TimeoutRate, bottom, top), fw, Tint(time));
                }
            }

            // Vertical hover guide.
            if (showHoverGuide && hover >= 0 && hover < n)
                AddLine(vh, new Vector2(pts[hover].x, bottom), new Vector2(pts[hover].x, top), Mathf.Max(1f, gridWidth), Tint(guideColor));

            // Main line.
            for (int i = 0; i < n - 1; i++)
            {
                Color c = line;
                if (partial && i == n - 2) c.a *= inProgressAlpha;
                AddLine(vh, pts[i], pts[i + 1], lineWidth, Tint(c));
            }

            // Points. Their radius is at least half the line width so the joints stay round.
            float radius = Mathf.Max(pointRadius, lineWidth * 0.5f);
            for (int i = 0; i < n; i++)
            {
                Color c = line;
                if (partial && i == n - 1) c.a *= inProgressAlpha;
                AddCircle(vh, pts[i], radius, Tint(c));
            }

            // Rings: on the hovered point, and on the newest point while its block is still filling.
            if (showHoverGuide && hover >= 0 && hover < n)
                AddRing(vh, pts[hover], radius * 2.1f, ringThickness, Tint(Color.white));
            if (partial && hover != n - 1)
            {
                Color c = line;
                c.a *= inProgressAlpha;
                AddRing(vh, pts[n - 1], radius * 2.1f, ringThickness, Tint(c));
            }
        }

        private Color32 Tint(Color c) => c * color; // Graphic's own tint

        private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 col)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, col, Vector2.zero);
            vh.AddVert(b, col, Vector2.zero);
            vh.AddVert(c, col, Vector2.zero);
            vh.AddVert(d, col, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        private static void AddLine(VertexHelper vh, Vector2 from, Vector2 to, float width, Color32 col)
        {
            Vector2 dir = to - from;
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();
            Vector2 n = new Vector2(-dir.y, dir.x) * (width * 0.5f);
            AddQuad(vh, from + n, to + n, to - n, from - n, col);
        }

        private static Vector2 RatePoint(float x, float rate, float bottom, float top)
        {
            return new Vector2(x, Mathf.Lerp(bottom, top, Mathf.Clamp01(rate)));
        }
        private static void AddCircle(VertexHelper vh, Vector2 centre, float radius, Color32 col)
        {
            const int segments = 16;
            int first = vh.currentVertCount;
            vh.AddVert(centre, col, Vector2.zero);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * 2f * Mathf.PI / segments;
                vh.AddVert(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, col, Vector2.zero);
            }

            for (int i = 0; i < segments; i++)
                vh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % segments);
        }

        // A hollow circle: outer radius `outer`, `thickness` wide.
        private static void AddRing(VertexHelper vh, Vector2 centre, float outer, float thickness, Color32 col)
        {
            const int segments = 24;
            float inner = Mathf.Max(0f, outer - thickness);
            int first = vh.currentVertCount;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * 2f * Mathf.PI / segments;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vh.AddVert(centre + dir * inner, col, Vector2.zero);
                vh.AddVert(centre + dir * outer, col, Vector2.zero);
            }

            for (int i = 0; i < segments; i++)
            {
                int a = first + i * 2;
                int b = first + ((i + 1) % segments) * 2;
                vh.AddTriangle(a, a + 1, b);
                vh.AddTriangle(b, a + 1, b + 1);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}