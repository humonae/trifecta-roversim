// using System;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.EventSystems;
// using UnityEngine.UI;
// using Training;

// namespace Evaluation
// {
//     // Named BarOutcome (not EpisodeOutcome) because Training.EpisodeOutcome already exists.
//     // The order is also the stacking order in each column, bottom to top, and matches the legend.
//     public enum BarOutcome : byte { Success = 0, TimedOut = 1, Flipped = 2, OutOfBounds = 3 }

//     /// <summary>Info about one column (bin) of the bar. Episode numbers are 1-based and inclusive.</summary>
//     public readonly struct EpisodeBin
//     {
//         public readonly int Index;
//         public readonly int FirstEpisode;
//         public readonly int LastEpisode;
//         public readonly int Success;
//         public readonly int TimedOut;
//         public readonly int Flipped;
//         public readonly int OutOfBounds;
//         public readonly bool InProgress;

//         public int Total => Success + TimedOut + Flipped + OutOfBounds;

//         public EpisodeBin(int index, int first, int last, int success, int timedOut, int flipped, int outOfBounds, bool inProgress)
//         {
//             Index = index; FirstEpisode = first; LastEpisode = last;
//             Success = success; TimedOut = timedOut; Flipped = flipped; OutOfBounds = outOfBounds;
//             InProgress = inProgress;
//         }

//         public override string ToString() =>
//             $"Episodes {FirstEpisode:0000}-{LastEpisode:0000}: {Success} success, {TimedOut} timed out, " +
//             $"{Flipped} flipped, {OutOfBounds} out of bounds" +
//             (InProgress ? " (in progress)" : "");
//     }

//     /// <summary>
//     /// uGUI episode tracking bar. Draws every episode outcome as one mesh (no child objects).
//     /// Bin size is chosen automatically so the bar never has more than Max Columns columns:
//     /// 1 episode per column up to Max Columns, then 2, 3, ... per column as the run grows.
//     /// Each column is a stacked bar: success at the bottom, then timed out, flipped, out of bounds.
//     ///
//     /// Add it like any UI element: empty GameObject with a RectTransform, then Add Component.
//     /// Keep Raycast Target on if you want hover info.
//     /// </summary>
//     [AddComponentMenu("UI/Episode Tracking Bar")]
//     public class EpisodeTrackingBar : MaskableGraphic, IPointerMoveHandler, IPointerExitHandler
//     {
//         private const int OutcomeCount = 4;

//         [Header("Layout")]
//         [SerializeField, Min(1)] private int maxColumns = 50;
//         [SerializeField, Min(0f)] private float gap = 3f;
//         [SerializeField, Min(0f)] private float cornerRadius = 3f;
//         [SerializeField, Range(2, 16)] private int cornerSegments = 6;

//         [Header("Colors (match your legend)")]
//         [SerializeField] private Color successColor = new Color(0.72f, 0.96f, 0.55f);
//         [SerializeField] private Color timedOutColor = new Color(0.87f, 0.33f, 0.20f);
//         [SerializeField] private Color flippedColor = new Color(0.80f, 0.53f, 0.28f);
//         [SerializeField] private Color outOfBoundsColor = new Color(0.95f, 0.77f, 0.37f);
//         [Tooltip("The last column is drawn this opaque while it is still filling up.")]
//         [SerializeField, Range(0f, 1f)] private float inProgressAlpha = 0.55f;

//         /// <summary>Raised when the hovered column changes. Null when the pointer leaves the bar.</summary>
//         public event Action<EpisodeBin?> BinHovered;

//         private readonly List<BarOutcome> episodes = new List<BarOutcome>();
//         private readonly int[] counts = new int[OutcomeCount];
//         private int hoverBin = -1;

//         public int EpisodeCount => episodes.Count;
//         public int BinSize => Mathf.Max(1, Mathf.CeilToInt(episodes.Count / (float)maxColumns));
//         public int BinCount => episodes.Count == 0 ? 0 : Mathf.CeilToInt(episodes.Count / (float)BinSize);

//         // --- Feeding data ---------------------------------------------------

//         /// <summary>
//         /// Maps one attempt to a bar category, using the attempt's outcome.
//         /// Hitting a rock is not an outcome, so it doesn't change the color here
//         /// (the Collisions tile still counts it).
//         /// A failed attempt with an outcome other than Flipped or OutOfBounds is drawn as Timed Out.
//         /// </summary>
//         public static BarOutcome Classify(AttemptResult attempt)
//         {
//             if (attempt.Success) return BarOutcome.Success;
//             if (attempt.Outcome == EpisodeOutcome.Flipped.ToString()) return BarOutcome.Flipped;
//             if (attempt.Outcome == EpisodeOutcome.OutOfBounds.ToString()) return BarOutcome.OutOfBounds;
//             return BarOutcome.TimedOut;
//         }

//         /// <summary>Rebuilds the whole bar from a report. Skipped attempts are ignored, like in the report's own stats.</summary>
//         public void SetFromReport(EvaluationReport report)
//         {
//             episodes.Clear();
//             if (report != null)
//             {
//                 foreach (AttemptResult attempt in report.AttemptLog)
//                 {
//                     if (attempt.Outcome == EvaluationReport.SkippedOutcome) continue;
//                     episodes.Add(Classify(attempt));
//                 }
//             }

//             if (hoverBin >= BinCount) hoverBin = -1;
//             SetVerticesDirty();
//         }

//         /// <summary>Appends one attempt. Skipped attempts are ignored.</summary>
//         public void AddAttempt(AttemptResult attempt)
//         {
//             if (attempt.Outcome == EvaluationReport.SkippedOutcome) return;
//             episodes.Add(Classify(attempt));
//             SetVerticesDirty();
//         }

//         public void ClearEpisodes()
//         {
//             episodes.Clear();
//             hoverBin = -1;
//             SetVerticesDirty();
//         }

//         /// <summary>Fraction (0-1) of successes in the last <paramref name="window"/> episodes.</summary>
//         public float RecentSuccessRate(int window = 50)
//         {
//             int n = episodes.Count;
//             if (n == 0) return 0f;
//             int from = Mathf.Max(0, n - window);
//             int success = 0;
//             for (int i = from; i < n; i++)
//                 if (episodes[i] == BarOutcome.Success) success++;
//             return success / (float)(n - from);
//         }

//         public EpisodeBin GetBin(int index)
//         {
//             int binSize = BinSize;
//             int first = index * binSize;
//             int end = Mathf.Min(first + binSize, episodes.Count);
//             int s = 0, t = 0, f = 0, o = 0;
//             for (int i = first; i < end; i++)
//             {
//                 switch (episodes[i])
//                 {
//                     case BarOutcome.Success: s++; break;
//                     case BarOutcome.TimedOut: t++; break;
//                     case BarOutcome.Flipped: f++; break;
//                     default: o++; break;
//                 }
//             }
//             return new EpisodeBin(index, first + 1, end, s, t, f, o, end - first < binSize);
//         }

//         // --- Hover ----------------------------------------------------------

//         public void OnPointerMove(PointerEventData eventData)
//         {
//             int binCount = BinCount;
//             Vector2 local;
//             if (binCount == 0 ||
//                 !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.enterEventCamera, out local))
//             {
//                 SetHoverBin(-1);
//                 return;
//             }

//             Rect r = GetPixelAdjustedRect();
//             float colW = ColumnWidth(r.width, binCount);
//             if (colW <= 0f)
//             {
//                 SetHoverBin(-1);
//                 return;
//             }

//             int index = Mathf.FloorToInt((local.x - r.xMin) / (colW + gap));
//             SetHoverBin(index >= 0 && index < binCount ? index : -1);
//         }

//         public void OnPointerExit(PointerEventData eventData)
//         {
//             SetHoverBin(-1);
//         }

//         private void SetHoverBin(int index)
//         {
//             if (index == hoverBin) return;
//             hoverBin = index;
//             SetVerticesDirty();
//             BinHovered?.Invoke(index >= 0 ? GetBin(index) : (EpisodeBin?)null);
//         }

//         // --- Mesh -----------------------------------------------------------

//         private float ColumnWidth(float totalWidth, int binCount) =>
//             (totalWidth - gap * (binCount - 1)) / binCount;

//         protected override void OnPopulateMesh(VertexHelper vh)
//         {
//             vh.Clear();

//             Rect r = GetPixelAdjustedRect();
//             int binCount = BinCount;
//             if (binCount == 0 || r.width <= 0f || r.height <= 0f) return;

//             float colW = ColumnWidth(r.width, binCount);
//             if (colW <= 0f) return;

//             int binSize = BinSize;
//             float radius = Mathf.Min(cornerRadius, colW * 0.5f);

//             for (int b = 0; b < binCount; b++)
//             {
//                 int first = b * binSize;
//                 int end = Mathf.Min(first + binSize, episodes.Count);
//                 int total = end - first;
//                 bool inProgress = total < binSize;

//                 for (int o = 0; o < OutcomeCount; o++) counts[o] = 0;
//                 for (int i = first; i < end; i++) counts[(int)episodes[i]]++;

//                 int bottomIdx = -1, topIdx = -1;
//                 for (int o = 0; o < OutcomeCount; o++)
//                 {
//                     if (counts[o] == 0) continue;
//                     if (bottomIdx < 0) bottomIdx = o;
//                     topIdx = o;
//                 }

//                 float x = r.xMin + b * (colW + gap);
//                 float y = r.yMin; // uGUI is y-up: stack from the bottom edge upward

//                 for (int o = 0; o < OutcomeCount; o++)
//                 {
//                     if (counts[o] == 0) continue;

//                     float h = counts[o] / (float)total * r.height;
//                     bool isBottom = o == bottomIdx;
//                     bool isTop = o == topIdx;

//                     Color c = ColorFor(o);
//                     if (inProgress) c.a *= inProgressAlpha;
//                     if (b == hoverBin)
//                     {
//                         float a = c.a;
//                         c = Color.Lerp(c, Color.white, 0.3f);
//                         c.a = a;
//                     }
//                     c *= color; // Graphic's own tint

//                     float bottomR = isBottom ? radius : 0f;
//                     float topR = isTop ? radius : 0f;
//                     AddRoundedRect(vh, x, y, colW, h, bottomR, bottomR, topR, topR, c);
//                     y += h;
//                 }
//             }
//         }

//         private Color ColorFor(int outcome)
//         {
//             switch ((BarOutcome)outcome)
//             {
//                 case BarOutcome.Success: return successColor;
//                 case BarOutcome.TimedOut: return timedOutColor;
//                 case BarOutcome.Flipped: return flippedColor;
//                 default: return outOfBoundsColor;
//             }
//         }

//         /// <summary>Adds a rounded rect (triangle fan) with an independent radius per corner.</summary>
//         private void AddRoundedRect(VertexHelper vh, float x, float y, float w, float h,
//                                     float bl, float br, float tr, float tl, Color32 c)
//         {
//             float max = Mathf.Min(w, h) * 0.5f;
//             bl = Mathf.Min(bl, max); br = Mathf.Min(br, max);
//             tr = Mathf.Min(tr, max); tl = Mathf.Min(tl, max);

//             int centre = vh.currentVertCount;
//             vh.AddVert(new Vector3(x + w * 0.5f, y + h * 0.5f), c, Vector2.zero);

//             int first = vh.currentVertCount;
//             AddCorner(vh, x + bl, y + bl, bl, 180f, c, x, y);
//             AddCorner(vh, x + w - br, y + br, br, 270f, c, x + w, y);
//             AddCorner(vh, x + w - tr, y + h - tr, tr, 0f, c, x + w, y + h);
//             AddCorner(vh, x + tl, y + h - tl, tl, 90f, c, x, y + h);

//             int count = vh.currentVertCount - first;
//             for (int i = 0; i < count; i++)
//                 vh.AddTriangle(centre, first + i, first + (i + 1) % count);
//         }

//         private void AddCorner(VertexHelper vh, float cx, float cy, float radius, float startDegrees,
//                                Color32 c, float cornerX, float cornerY)
//         {
//             if (radius <= 0f)
//             {
//                 vh.AddVert(new Vector3(cornerX, cornerY), c, Vector2.zero);
//                 return;
//             }

//             for (int i = 0; i <= cornerSegments; i++)
//             {
//                 float angle = (startDegrees + 90f * i / cornerSegments) * Mathf.Deg2Rad;
//                 vh.AddVert(new Vector3(cx + Mathf.Cos(angle) * radius, cy + Mathf.Sin(angle) * radius), c, Vector2.zero);
//             }
//         }

// #if UNITY_EDITOR
//         protected override void OnValidate()
//         {
//             base.OnValidate();
//             SetVerticesDirty();
//         }
// #endif
//     }
// }

// using System;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.EventSystems;
// using UnityEngine.UI;
// using Training;

// namespace Evaluation
// {
//     // Named BarOutcome (not EpisodeOutcome) because Training.EpisodeOutcome already exists.
//     // The order is also the stacking order in each column, bottom to top, and matches the legend.
//     public enum BarOutcome : byte { Success = 0, TimedOut = 1, Flipped = 2, OutOfBounds = 3 }

//     /// <summary>Info about one column (bin) of the bar. Episode numbers are 1-based and inclusive.</summary>
//     public readonly struct EpisodeBin
//     {
//         public readonly int Index;
//         public readonly int FirstEpisode;
//         public readonly int LastEpisode;
//         public readonly int Success;
//         public readonly int TimedOut;
//         public readonly int Flipped;
//         public readonly int OutOfBounds;
//         public readonly bool InProgress;

//         public int Total => Success + TimedOut + Flipped + OutOfBounds;

//         public EpisodeBin(int index, int first, int last, int success, int timedOut, int flipped, int outOfBounds, bool inProgress)
//         {
//             Index = index; FirstEpisode = first; LastEpisode = last;
//             Success = success; TimedOut = timedOut; Flipped = flipped; OutOfBounds = outOfBounds;
//             InProgress = inProgress;
//         }

//         public override string ToString() =>
//             $"Episodes {FirstEpisode:0000}-{LastEpisode:0000}: {Success} success, {TimedOut} timed out, " +
//             $"{Flipped} flipped, {OutOfBounds} out of bounds" +
//             (InProgress ? " (in progress)" : "");
//     }

//     /// <summary>
//     /// uGUI episode tracking bar. Draws every episode outcome as one mesh (no child objects).
//     /// Bin size is chosen automatically so the bar never has more than Max Columns columns:
//     /// 1 episode per column up to Max Columns, then 2, 3, ... per column as the run grows.
//     /// Each column is a stacked bar: success at the bottom, then timed out, flipped, out of bounds.
//     ///
//     /// Add it like any UI element: empty GameObject with a RectTransform, then Add Component.
//     /// Keep Raycast Target on if you want hover info.
//     /// </summary>
//     [AddComponentMenu("UI/Episode Tracking Bar")]
//     public class EpisodeTrackingBar : MaskableGraphic, IPointerMoveHandler, IPointerExitHandler
//     {
//         private const int OutcomeCount = 4;

//         [Header("Layout")]
//         [SerializeField, Min(1)] private int maxColumns = 50;
//         [SerializeField, Min(0f)] private float gap = 3f;
//         [SerializeField, Min(0f)] private float cornerRadius = 3f;
//         [SerializeField, Range(2, 16)] private int cornerSegments = 6;
//         [Tooltip("While the run has fewer episodes than Max Columns, keep every column at its final width so the bar fills " +
//                  "from the left, instead of one episode stretching across the whole bar.")]
//         [SerializeField] private bool reserveSlots = true;
//         [Tooltip("Color of the empty slots still to be filled. Set its alpha to 0 to hide them.")]
//         [SerializeField] private Color emptySlotColor = new Color(1f, 1f, 1f, 0.08f);

//         [Header("Colors (match your legend)")]
//         [SerializeField] private Color successColor = new Color(0.72f, 0.96f, 0.55f);
//         [SerializeField] private Color timedOutColor = new Color(0.87f, 0.33f, 0.20f);
//         [SerializeField] private Color flippedColor = new Color(0.80f, 0.53f, 0.28f);
//         [SerializeField] private Color outOfBoundsColor = new Color(0.95f, 0.77f, 0.37f);
//         [Tooltip("The last column is drawn this opaque while it is still filling up.")]
//         [SerializeField, Range(0f, 1f)] private float inProgressAlpha = 0.55f;

//         /// <summary>Raised when the hovered column changes. Null when the pointer leaves the bar.</summary>
//         public event Action<EpisodeBin?> BinHovered;

//         private readonly List<BarOutcome> episodes = new List<BarOutcome>();
//         private readonly int[] counts = new int[OutcomeCount];
//         private int hoverBin = -1;

//         public int EpisodeCount => episodes.Count;
//         public int BinSize => Mathf.Max(1, Mathf.CeilToInt(episodes.Count / (float)maxColumns));
//         public int BinCount => episodes.Count == 0 ? 0 : Mathf.CeilToInt(episodes.Count / (float)BinSize);

//         // How many column positions the width is divided into. While every episode still gets its own
//         // column, that is Max Columns, so columns keep their final width as the run grows.
//         private int SlotCount => reserveSlots && BinSize == 1 ? maxColumns : BinCount;

//         // --- Feeding data ---------------------------------------------------

//         /// <summary>
//         /// Maps one attempt to a bar category, using the attempt's outcome.
//         /// Hitting a rock is not an outcome, so it doesn't change the color here
//         /// (the Collisions tile still counts it).
//         /// A failed attempt with an outcome other than Flipped or OutOfBounds is drawn as Timed Out.
//         /// </summary>
//         public static BarOutcome Classify(AttemptResult attempt)
//         {
//             if (attempt.Success) return BarOutcome.Success;
//             if (attempt.Outcome == EpisodeOutcome.Flipped.ToString()) return BarOutcome.Flipped;
//             if (attempt.Outcome == EpisodeOutcome.OutOfBounds.ToString()) return BarOutcome.OutOfBounds;
//             return BarOutcome.TimedOut;
//         }

//         /// <summary>Rebuilds the whole bar from a report. Skipped attempts are ignored, like in the report's own stats.</summary>
//         public void SetFromReport(EvaluationReport report)
//         {
//             episodes.Clear();
//             if (report != null)
//             {
//                 foreach (AttemptResult attempt in report.AttemptLog)
//                 {
//                     if (attempt.Outcome == EvaluationReport.SkippedOutcome) continue;
//                     episodes.Add(Classify(attempt));
//                 }
//             }

//             if (hoverBin >= BinCount) hoverBin = -1;
//             SetVerticesDirty();
//         }

//         /// <summary>Appends one attempt. Skipped attempts are ignored.</summary>
//         public void AddAttempt(AttemptResult attempt)
//         {
//             if (attempt.Outcome == EvaluationReport.SkippedOutcome) return;
//             episodes.Add(Classify(attempt));
//             SetVerticesDirty();
//         }

//         public void ClearEpisodes()
//         {
//             episodes.Clear();
//             hoverBin = -1;
//             SetVerticesDirty();
//         }

//         /// <summary>Fraction (0-1) of successes in the last <paramref name="window"/> episodes.</summary>
//         public float RecentSuccessRate(int window = 50)
//         {
//             int n = episodes.Count;
//             if (n == 0) return 0f;
//             int from = Mathf.Max(0, n - window);
//             int success = 0;
//             for (int i = from; i < n; i++)
//                 if (episodes[i] == BarOutcome.Success) success++;
//             return success / (float)(n - from);
//         }

//         public EpisodeBin GetBin(int index)
//         {
//             int binSize = BinSize;
//             int first = index * binSize;
//             int end = Mathf.Min(first + binSize, episodes.Count);
//             int s = 0, t = 0, f = 0, o = 0;
//             for (int i = first; i < end; i++)
//             {
//                 switch (episodes[i])
//                 {
//                     case BarOutcome.Success: s++; break;
//                     case BarOutcome.TimedOut: t++; break;
//                     case BarOutcome.Flipped: f++; break;
//                     default: o++; break;
//                 }
//             }
//             return new EpisodeBin(index, first + 1, end, s, t, f, o, end - first < binSize);
//         }

//         // --- Hover ----------------------------------------------------------

//         public void OnPointerMove(PointerEventData eventData)
//         {
//             int binCount = BinCount;
//             Vector2 local;
//             if (binCount == 0 ||
//                 !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.enterEventCamera, out local))
//             {
//                 SetHoverBin(-1);
//                 return;
//             }

//             Rect r = GetPixelAdjustedRect();
//             float colW = ColumnWidth(r.width, SlotCount);
//             if (colW <= 0f)
//             {
//                 SetHoverBin(-1);
//                 return;
//             }

//             int index = Mathf.FloorToInt((local.x - r.xMin) / (colW + gap));
//             SetHoverBin(index >= 0 && index < binCount ? index : -1);
//         }

//         public void OnPointerExit(PointerEventData eventData)
//         {
//             SetHoverBin(-1);
//         }

//         private void SetHoverBin(int index)
//         {
//             if (index == hoverBin) return;
//             hoverBin = index;
//             SetVerticesDirty();
//             BinHovered?.Invoke(index >= 0 ? GetBin(index) : (EpisodeBin?)null);
//         }

//         // --- Mesh -----------------------------------------------------------

//         private float ColumnWidth(float totalWidth, int binCount) =>
//             (totalWidth - gap * (binCount - 1)) / binCount;

//         protected override void OnPopulateMesh(VertexHelper vh)
//         {
//             vh.Clear();

//             Rect r = GetPixelAdjustedRect();
//             int binCount = BinCount;
//             int slotCount = SlotCount;
//             if (slotCount == 0 || r.width <= 0f || r.height <= 0f) return;

//             float colW = ColumnWidth(r.width, slotCount);
//             if (colW <= 0f) return;

//             int binSize = BinSize;
//             float radius = Mathf.Min(cornerRadius, colW * 0.5f);

//             for (int b = 0; b < binCount; b++)
//             {
//                 int first = b * binSize;
//                 int end = Mathf.Min(first + binSize, episodes.Count);
//                 int total = end - first;
//                 bool inProgress = total < binSize;

//                 for (int o = 0; o < OutcomeCount; o++) counts[o] = 0;
//                 for (int i = first; i < end; i++) counts[(int)episodes[i]]++;

//                 int bottomIdx = -1, topIdx = -1;
//                 for (int o = 0; o < OutcomeCount; o++)
//                 {
//                     if (counts[o] == 0) continue;
//                     if (bottomIdx < 0) bottomIdx = o;
//                     topIdx = o;
//                 }

//                 float x = r.xMin + b * (colW + gap);
//                 float y = r.yMin; // uGUI is y-up: stack from the bottom edge upward

//                 for (int o = 0; o < OutcomeCount; o++)
//                 {
//                     if (counts[o] == 0) continue;

//                     float h = counts[o] / (float)total * r.height;
//                     bool isBottom = o == bottomIdx;
//                     bool isTop = o == topIdx;

//                     Color c = ColorFor(o);
//                     if (inProgress) c.a *= inProgressAlpha;
//                     if (b == hoverBin)
//                     {
//                         float a = c.a;
//                         c = Color.Lerp(c, Color.white, 0.3f);
//                         c.a = a;
//                     }
//                     c *= color; // Graphic's own tint

//                     float bottomR = isBottom ? radius : 0f;
//                     float topR = isTop ? radius : 0f;
//                     AddRoundedRect(vh, x, y, colW, h, bottomR, bottomR, topR, topR, c);
//                     y += h;
//                 }
//             }

//             // Empty slots show the bar's capacity, so a short run reads as "a few out of many".
//             if (emptySlotColor.a > 0f)
//             {
//                 Color empty = emptySlotColor * color;
//                 for (int b = binCount; b < slotCount; b++)
//                     AddRoundedRect(vh, r.xMin + b * (colW + gap), r.yMin, colW, r.height, radius, radius, radius, radius, empty);
//             }
//         }

//         private Color ColorFor(int outcome)
//         {
//             switch ((BarOutcome)outcome)
//             {
//                 case BarOutcome.Success: return successColor;
//                 case BarOutcome.TimedOut: return timedOutColor;
//                 case BarOutcome.Flipped: return flippedColor;
//                 default: return outOfBoundsColor;
//             }
//         }

//         /// <summary>Adds a rounded rect (triangle fan) with an independent radius per corner.</summary>
//         private void AddRoundedRect(VertexHelper vh, float x, float y, float w, float h,
//                                     float bl, float br, float tr, float tl, Color32 c)
//         {
//             float max = Mathf.Min(w, h) * 0.5f;
//             bl = Mathf.Min(bl, max); br = Mathf.Min(br, max);
//             tr = Mathf.Min(tr, max); tl = Mathf.Min(tl, max);

//             int centre = vh.currentVertCount;
//             vh.AddVert(new Vector3(x + w * 0.5f, y + h * 0.5f), c, Vector2.zero);

//             int first = vh.currentVertCount;
//             AddCorner(vh, x + bl, y + bl, bl, 180f, c, x, y);
//             AddCorner(vh, x + w - br, y + br, br, 270f, c, x + w, y);
//             AddCorner(vh, x + w - tr, y + h - tr, tr, 0f, c, x + w, y + h);
//             AddCorner(vh, x + tl, y + h - tl, tl, 90f, c, x, y + h);

//             int count = vh.currentVertCount - first;
//             for (int i = 0; i < count; i++)
//                 vh.AddTriangle(centre, first + i, first + (i + 1) % count);
//         }

//         private void AddCorner(VertexHelper vh, float cx, float cy, float radius, float startDegrees,
//                                Color32 c, float cornerX, float cornerY)
//         {
//             if (radius <= 0f)
//             {
//                 vh.AddVert(new Vector3(cornerX, cornerY), c, Vector2.zero);
//                 return;
//             }

//             for (int i = 0; i <= cornerSegments; i++)
//             {
//                 float angle = (startDegrees + 90f * i / cornerSegments) * Mathf.Deg2Rad;
//                 vh.AddVert(new Vector3(cx + Mathf.Cos(angle) * radius, cy + Mathf.Sin(angle) * radius), c, Vector2.zero);
//             }
//         }

// #if UNITY_EDITOR
//         protected override void OnValidate()
//         {
//             base.OnValidate();
//             SetVerticesDirty();
//         }
// #endif
//     }
// }

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Training;

namespace Evaluation
{
    // Named BarOutcome (not EpisodeOutcome) because Training.EpisodeOutcome already exists.
    // The order is also the stacking order in each column, bottom to top, and matches the legend.
    public enum BarOutcome : byte { Success = 0, TimedOut = 1, Flipped = 2, OutOfBounds = 3 }

    /// <summary>Info about one column (bin) of the bar. Episode numbers are 1-based and inclusive.</summary>
    public readonly struct EpisodeBin
    {
        public readonly int Index;
        public readonly int FirstEpisode;
        public readonly int LastEpisode;
        public readonly int Success;
        public readonly int TimedOut;
        public readonly int Flipped;
        public readonly int OutOfBounds;
        public readonly bool InProgress;

        public int Total => Success + TimedOut + Flipped + OutOfBounds;

        public EpisodeBin(int index, int first, int last, int success, int timedOut, int flipped, int outOfBounds, bool inProgress)
        {
            Index = index; FirstEpisode = first; LastEpisode = last;
            Success = success; TimedOut = timedOut; Flipped = flipped; OutOfBounds = outOfBounds;
            InProgress = inProgress;
        }

        public override string ToString() =>
            $"Episodes {FirstEpisode:0000}-{LastEpisode:0000}: {Success} success, {TimedOut} timed out, " +
            $"{Flipped} flipped, {OutOfBounds} out of bounds" +
            (InProgress ? " (in progress)" : "");
    }

    /// <summary>
    /// uGUI episode tracking bar. Draws every episode outcome as one mesh (no child objects).
    /// Bin size is chosen automatically so the bar never has more than Max Columns columns:
    /// 1 episode per column up to Max Columns, then 2, 3, ... per column as the run grows.
    /// Each column is a stacked bar: success at the bottom, then timed out, flipped, out of bounds.
    ///
    /// Add it like any UI element: empty GameObject with a RectTransform, then Add Component.
    /// Keep Raycast Target on if you want hover info.
    /// </summary>
    [AddComponentMenu("UI/Episode Tracking Bar")]
    public class EpisodeTrackingBar : MaskableGraphic, IPointerMoveHandler, IPointerExitHandler
    {
        private const int OutcomeCount = 4;

        [Header("Layout")]
        [SerializeField, Min(1)] private int maxColumns = 50;
        [SerializeField, Min(0f)] private float gap = 3f;
        [SerializeField, Min(0f)] private float cornerRadius = 3f;
        [SerializeField, Range(2, 16)] private int cornerSegments = 6;
        [Tooltip("While the run has fewer episodes than Max Columns, keep every column at its final width so the bar fills " +
                 "from the left, instead of one episode stretching across the whole bar.")]
        [SerializeField] private bool reserveSlots = true;
        [Tooltip("Color of the empty slots still to be filled. Set its alpha to 0 to hide them.")]
        [SerializeField] private Color emptySlotColor = new Color(1f, 1f, 1f, 0.08f);

        [Header("Colors (match your legend)")]
        [SerializeField] private Color successColor = new Color(0.72f, 0.96f, 0.55f);
        [SerializeField] private Color timedOutColor = new Color(0.87f, 0.33f, 0.20f);
        [SerializeField] private Color flippedColor = new Color(0.80f, 0.53f, 0.28f);
        [SerializeField] private Color outOfBoundsColor = new Color(0.95f, 0.77f, 0.37f);
        [Tooltip("The last column is drawn this opaque while it is still filling up.")]
        [SerializeField, Range(0f, 1f)] private float inProgressAlpha = 0.55f;

        /// <summary>Raised when the hovered column changes. Null when the pointer leaves the bar.</summary>
        public event Action<EpisodeBin?> BinHovered;

        private readonly List<BarOutcome> episodes = new List<BarOutcome>();
        private readonly int[] counts = new int[OutcomeCount];
        private int hoverBin = -1;

        public int EpisodeCount => episodes.Count;
        public int BinSize => Mathf.Max(1, Mathf.CeilToInt(episodes.Count / (float)maxColumns));
        public int BinCount => episodes.Count == 0 ? 0 : Mathf.CeilToInt(episodes.Count / (float)BinSize);

        // How many column positions the width is divided into. While every episode still gets its own
        // column, that is Max Columns, so columns keep their final width as the run grows.
        private int SlotCount => reserveSlots && BinSize == 1 ? maxColumns : BinCount;

        // --- Feeding data ---------------------------------------------------

        /// <summary>
        /// Maps one attempt to a bar category, using the attempt's outcome.
        /// Hitting a rock is not an outcome, so it doesn't change the color here
        /// (the Collisions tile still counts it).
        /// A failed attempt with an outcome other than Flipped or OutOfBounds is drawn as Timed Out.
        /// </summary>
        public static BarOutcome Classify(AttemptResult attempt)
        {
            if (attempt.Success) return BarOutcome.Success;
            if (attempt.Outcome == EpisodeOutcome.Flipped.ToString()) return BarOutcome.Flipped;
            if (attempt.Outcome == EpisodeOutcome.OutOfBounds.ToString()) return BarOutcome.OutOfBounds;
            return BarOutcome.TimedOut;
        }

        /// <summary>Rebuilds the whole bar from a report. Skipped attempts are ignored, like in the report's own stats.</summary>
        public void SetFromReport(EvaluationReport report)
        {
            episodes.Clear();
            if (report != null)
            {
                foreach (AttemptResult attempt in report.AttemptLog)
                {
                    if (attempt.Outcome == EvaluationReport.SkippedOutcome) continue;
                    episodes.Add(Classify(attempt));
                }
            }

            if (hoverBin >= BinCount) hoverBin = -1;
            SetVerticesDirty();
        }

        /// <summary>Appends one attempt. Skipped attempts are ignored.</summary>
        public void AddAttempt(AttemptResult attempt)
        {
            if (attempt.Outcome == EvaluationReport.SkippedOutcome) return;
            episodes.Add(Classify(attempt));
            SetVerticesDirty();
        }

        public void ClearEpisodes()
        {
            episodes.Clear();
            hoverBin = -1;
            SetVerticesDirty();
        }

        /// <summary>Appends one outcome directly. Used by EpisodeTrackingBarTester; real runs use SetFromReport.</summary>
        public void AddOutcome(BarOutcome outcome)
        {
            episodes.Add(outcome);
            SetVerticesDirty();
        }

        /// <summary>Replaces everything with these outcomes. Used by EpisodeTrackingBarTester; real runs use SetFromReport.</summary>
        public void SetOutcomes(IEnumerable<BarOutcome> outcomes)
        {
            episodes.Clear();
            episodes.AddRange(outcomes);
            if (hoverBin >= BinCount) hoverBin = -1;
            SetVerticesDirty();
        }

        /// <summary>Fraction (0-1) of successes in the last <paramref name="window"/> episodes.</summary>
        public float RecentSuccessRate(int window = 50)
        {
            int n = episodes.Count;
            if (n == 0) return 0f;
            int from = Mathf.Max(0, n - window);
            int success = 0;
            for (int i = from; i < n; i++)
                if (episodes[i] == BarOutcome.Success) success++;
            return success / (float)(n - from);
        }

        public EpisodeBin GetBin(int index)
        {
            int binSize = BinSize;
            int first = index * binSize;
            int end = Mathf.Min(first + binSize, episodes.Count);
            int s = 0, t = 0, f = 0, o = 0;
            for (int i = first; i < end; i++)
            {
                switch (episodes[i])
                {
                    case BarOutcome.Success: s++; break;
                    case BarOutcome.TimedOut: t++; break;
                    case BarOutcome.Flipped: f++; break;
                    default: o++; break;
                }
            }
            return new EpisodeBin(index, first + 1, end, s, t, f, o, end - first < binSize);
        }

        // --- Hover ----------------------------------------------------------

        public void OnPointerMove(PointerEventData eventData)
        {
            int binCount = BinCount;
            Vector2 local;
            if (binCount == 0 ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.enterEventCamera, out local))
            {
                SetHoverBin(-1);
                return;
            }

            Rect r = GetPixelAdjustedRect();
            float colW = ColumnWidth(r.width, SlotCount);
            if (colW <= 0f)
            {
                SetHoverBin(-1);
                return;
            }

            int index = Mathf.FloorToInt((local.x - r.xMin) / (colW + gap));
            SetHoverBin(index >= 0 && index < binCount ? index : -1);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHoverBin(-1);
        }

        private void SetHoverBin(int index)
        {
            if (index == hoverBin) return;
            hoverBin = index;
            SetVerticesDirty();
            BinHovered?.Invoke(index >= 0 ? GetBin(index) : (EpisodeBin?)null);
        }

        // --- Mesh -----------------------------------------------------------

        private float ColumnWidth(float totalWidth, int binCount) =>
            (totalWidth - gap * (binCount - 1)) / binCount;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect r = GetPixelAdjustedRect();
            int binCount = BinCount;
            int slotCount = SlotCount;
            if (slotCount == 0 || r.width <= 0f || r.height <= 0f) return;

            float colW = ColumnWidth(r.width, slotCount);
            if (colW <= 0f) return;

            int binSize = BinSize;
            float radius = Mathf.Min(cornerRadius, colW * 0.5f);

            for (int b = 0; b < binCount; b++)
            {
                int first = b * binSize;
                int end = Mathf.Min(first + binSize, episodes.Count);
                int total = end - first;
                bool inProgress = total < binSize;

                for (int o = 0; o < OutcomeCount; o++) counts[o] = 0;
                for (int i = first; i < end; i++) counts[(int)episodes[i]]++;

                int bottomIdx = -1, topIdx = -1;
                for (int o = 0; o < OutcomeCount; o++)
                {
                    if (counts[o] == 0) continue;
                    if (bottomIdx < 0) bottomIdx = o;
                    topIdx = o;
                }

                float x = r.xMin + b * (colW + gap);
                float y = r.yMin; // uGUI is y-up: stack from the bottom edge upward

                for (int o = 0; o < OutcomeCount; o++)
                {
                    if (counts[o] == 0) continue;

                    float h = counts[o] / (float)total * r.height;
                    bool isBottom = o == bottomIdx;
                    bool isTop = o == topIdx;

                    Color c = ColorFor(o);
                    if (inProgress) c.a *= inProgressAlpha;
                    if (b == hoverBin)
                    {
                        float a = c.a;
                        c = Color.Lerp(c, Color.white, 0.3f);
                        c.a = a;
                    }
                    c *= color; // Graphic's own tint

                    float bottomR = isBottom ? radius : 0f;
                    float topR = isTop ? radius : 0f;
                    AddRoundedRect(vh, x, y, colW, h, bottomR, bottomR, topR, topR, c);
                    y += h;
                }
            }

            // Empty slots show the bar's capacity, so a short run reads as "a few out of many".
            if (emptySlotColor.a > 0f)
            {
                Color empty = emptySlotColor * color;
                for (int b = binCount; b < slotCount; b++)
                    AddRoundedRect(vh, r.xMin + b * (colW + gap), r.yMin, colW, r.height, radius, radius, radius, radius, empty);
            }
        }

        private Color ColorFor(int outcome)
        {
            switch ((BarOutcome)outcome)
            {
                case BarOutcome.Success: return successColor;
                case BarOutcome.TimedOut: return timedOutColor;
                case BarOutcome.Flipped: return flippedColor;
                default: return outOfBoundsColor;
            }
        }

        /// <summary>Adds a rounded rect (triangle fan) with an independent radius per corner.</summary>
        private void AddRoundedRect(VertexHelper vh, float x, float y, float w, float h,
                                    float bl, float br, float tr, float tl, Color32 c)
        {
            float max = Mathf.Min(w, h) * 0.5f;
            bl = Mathf.Min(bl, max); br = Mathf.Min(br, max);
            tr = Mathf.Min(tr, max); tl = Mathf.Min(tl, max);

            int centre = vh.currentVertCount;
            vh.AddVert(new Vector3(x + w * 0.5f, y + h * 0.5f), c, Vector2.zero);

            int first = vh.currentVertCount;
            AddCorner(vh, x + bl, y + bl, bl, 180f, c, x, y);
            AddCorner(vh, x + w - br, y + br, br, 270f, c, x + w, y);
            AddCorner(vh, x + w - tr, y + h - tr, tr, 0f, c, x + w, y + h);
            AddCorner(vh, x + tl, y + h - tl, tl, 90f, c, x, y + h);

            int count = vh.currentVertCount - first;
            for (int i = 0; i < count; i++)
                vh.AddTriangle(centre, first + i, first + (i + 1) % count);
        }

        private void AddCorner(VertexHelper vh, float cx, float cy, float radius, float startDegrees,
                               Color32 c, float cornerX, float cornerY)
        {
            if (radius <= 0f)
            {
                vh.AddVert(new Vector3(cornerX, cornerY), c, Vector2.zero);
                return;
            }

            for (int i = 0; i <= cornerSegments; i++)
            {
                float angle = (startDegrees + 90f * i / cornerSegments) * Mathf.Deg2Rad;
                vh.AddVert(new Vector3(cx + Mathf.Cos(angle) * radius, cy + Mathf.Sin(angle) * radius), c, Vector2.zero);
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