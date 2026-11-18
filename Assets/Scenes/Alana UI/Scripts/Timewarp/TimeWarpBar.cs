using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Training
{
    // The speed arrows, speed label and T-/T+ clock as normal UI inside this RectTransform.
    //
    // Put it on any RectTransform, for example an empty child of the rover window's top bar next to
    // the play / pause / stop buttons. Unlike TimeWarpPlacement (drawn with OnGUI on top of everything),
    // it moves, scales, hides and sorts with the UI it's in, and works inside layout groups.
    //
    // Size:
    //   Fit Content - sized from the rect's height; the rect's width is set to the content's (no empty
    //                 space), and layout groups get that as its preferred width.
    //   Fill Rect   - as large as fits the rect's width and height, in one or two rows, whichever is bigger.
    // Place In Parent pins the rect to the left, centre or right of its parent (the parent's layout
    // group then skips it), so it can sit at either end of a bar without extra components.
    // The UI is built when Play starts; in edit mode you only see the rect.
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class TimeWarpBar : UIBehaviour, ILayoutElement, ILayoutIgnorer
    {
        public enum RowMode { Auto, OneRow, TwoRows }
        // Same values as the first version (FitContent = 0, UseRectWidth = 1), so saved scenes keep their choice.
        public enum SizeMode { FitContent = 0, FillRect = 1 }
        public enum HorizontalAlign { Left, Center, Right }
        public enum ParentPlacement { LayoutGroup, Left, Center, Right }

        private const int ArrowTextureSize = 32;
        private const float MeasureFontSize = 100f;
        private const float TooSmallArrow = 8f;

        // Proportions, relative to the arrow size.
        private const float ArrowGap = 0.15f;
        private const float SpeedGap = 0.35f;
        private const float SpeedFont = 0.8f;
        private const float ClockGap = 0.7f;
        private const float OneRowClockFont = 0.62f;
        private const float TwoRowGap = 0.1f;
        private const float TwoRowClockHeight = 0.67f;
        private const float TwoRowClockFont = 0.57f;

        [SerializeField] private TrainingOrganizer organizer;

        [Header("Speeds")]
        [Tooltip("One arrow per step. Arrow N sets the speed to N times real time.")]
        [SerializeField, Range(2, 20)] private int arrowCount = 10;
        [Tooltip("Turn off if another speed widget (TimeWarpPlacement, TimeWarpArrows) also handles the keys.")]
        [SerializeField] private bool keyboardShortcuts = true;
        [SerializeField] private Key speedUpKey = Key.Period;
        [SerializeField] private Key slowDownKey = Key.Comma;

        [Header("Size and position")]
        [Tooltip("Fit Content: sized from this rect's height, and the rect's width is set to the content's (no empty space).\n" +
                 "Fill Rect: as large as fits this rect's width and height. Size the rect to the area you want; Alignment places the bar in any space left over.")]
        [FormerlySerializedAs("width")]
        [SerializeField] private SizeMode size = SizeMode.FitContent;
        [Tooltip("Where the bar sits inside this rect when there's room to spare.")]
        [SerializeField] private HorizontalAlign alignment = HorizontalAlign.Left;
        [Tooltip("Layout Group: the parent's layout group places this rect (in the top bar: after the stop button).\n" +
                 "Left / Center / Right: pinned to that side of the parent and vertically centred; the parent's layout group skips it.")]
        [SerializeField] private ParentPlacement placeInParent = ParentPlacement.LayoutGroup;
        [Tooltip("Gap to the parent's edge when pinned Left or Right.")]
        [SerializeField] private float edgeMargin = 10f;
        [Tooltip("Auto: Fit Content uses two rows from Two Row Min Height up; Fill Rect uses whichever layout comes out bigger.")]
        [SerializeField] private RowMode rows = RowMode.Auto;
        [SerializeField, Min(0f)] private float twoRowMinHeight = 44f;
        [SerializeField, Min(0f)] private float padding = 3f;
        [SerializeField] private bool showClock = true;

        [Header("Look")]
        [Tooltip("Optional. Empty uses TextMesh Pro's default font.")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Color litColor = new Color(0.36f, 0.92f, 0.36f);
        [SerializeField] private Color unlitColor = new Color(0.32f, 0.34f, 0.32f);
        [SerializeField] private Color laggingColor = new Color(1f, 0.72f, 0.25f);
        [SerializeField] private Color speedColor = Color.white;
        [SerializeField] private Color clockColor = new Color(0.36f, 0.92f, 0.36f);
        [Tooltip("Behind the content only. Alpha 0 for none, e.g. when the bar it's in already has a background.")]
        [SerializeField] private Color backgroundColor = new Color(0.06f, 0.06f, 0.07f, 0.8f);

        // Only one TimeWarpBar reacts to a key press per frame.
        private static int keyFrame = -1;

        private readonly SpeedMeter meter = new SpeedMeter(0.5f);
        private readonly List<Image> arrows = new List<Image>();
        private ISimulationRun run;
        private RectTransform content;
        private Image background;
        private TextMeshProUGUI speedLabel;
        private TextMeshProUGUI clockLabel;
        private Texture2D arrowTexture;
        private Sprite arrowSprite;
        private bool built;
        private bool layoutDirty = true;
        private bool rebuildNeeded;
        private float contentWidth = -1f;
        private float speedWidthPerFont;
        private float clockWidthPerFont;
        private bool warnedTooSmall;

        private RectTransform Area => (RectTransform)transform;

        protected override void Start()
        {
            base.Start();
            if (!Application.isPlaying)
                return;

            if (organizer == null)
                organizer = FindAnyObjectByType<TrainingOrganizer>();
            run = SimulationRuns.Resolve(organizer);
            Build();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (arrowSprite != null)
                Destroy(arrowSprite);
            if (arrowTexture != null)
                Destroy(arrowTexture);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            layoutDirty = true;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            rebuildNeeded = built;
        }
#endif

        private void Update()
        {
            if (!built || run == null)
                return;

            meter.Sample();
            HandleKeys();
            Refresh();
        }

        // After Update, so a size change from this frame's layout pass is picked up.
        private void LateUpdate()
        {
            if (rebuildNeeded)
            {
                rebuildNeeded = false;
                Build();
                if (transform.parent is RectTransform parentRect)
                    LayoutRebuilder.MarkLayoutForRebuild(parentRect);
            }

            if (built && layoutDirty)
            {
                layoutDirty = false;
                LayOut();
            }
        }

        // ---- Building ----

        private void Build()
        {
            if (content != null)
                Destroy(content.gameObject);
            arrows.Clear();

            // Check both: either can be missing on its own (e.g. carried over from an earlier Play session).
            if (arrowTexture == null || arrowSprite == null)
            {
                if (arrowSprite != null)
                    Destroy(arrowSprite);
                if (arrowTexture != null)
                    Destroy(arrowTexture);
                arrowTexture = ArrowTexture.Create(ArrowTextureSize);
                arrowSprite = Sprite.Create(arrowTexture, new Rect(0, 0, ArrowTextureSize, ArrowTextureSize), new Vector2(0.5f, 0.5f), 100f);
            }

            content = NewRect("Time Warp Content", Area);
            content.anchorMin = content.anchorMax = new Vector2(0f, 0.5f);
            content.pivot = new Vector2(0f, 0.5f);

            background = NewRect("Background", content).gameObject.AddComponent<Image>();
            Stretch(background.rectTransform);
            background.raycastTarget = false;

            for (int i = 0; i < arrowCount; i++)
            {
                RectTransform arrowRect = NewRect($"Arrow {i + 1}", content);
                var image = arrowRect.gameObject.AddComponent<Image>();
                image.sprite = arrowSprite;
                image.preserveAspect = true;

                var button = arrowRect.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                int level = i + 1;
                button.onClick.AddListener(() => SetLevel(level));

                arrows.Add(image);
            }

            speedLabel = NewLabel("Speed", TextAlignmentOptions.MidlineLeft);
            speedLabel.fontStyle = FontStyles.Bold;
            clockLabel = NewLabel("Clock", TextAlignmentOptions.MidlineLeft);
            clockLabel.gameObject.SetActive(showClock);

            // Text width grows in step with font size, so measure once and scale.
            speedWidthPerFont = WidthPerFontUnit(speedLabel, $"{arrowCount}x", $"{arrowCount - 1}.9x");
            clockWidthPerFont = WidthPerFontUnit(clockLabel, "T- 00:00:00   T+ 00:00:00");
            warnedTooSmall = false;

            built = true;
            layoutDirty = true;
            if (run != null)
                Refresh();
        }

        private TextMeshProUGUI NewLabel(string objectName, TextAlignmentOptions align)
        {
            var label = NewRect(objectName, content).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                label.font = font;
            label.alignment = align;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.enableAutoSizing = false;
            label.raycastTarget = false;
            label.margin = Vector4.zero;
            return label;
        }

        private static RectTransform NewRect(string objectName, Transform parent)
        {
            var child = new GameObject(objectName, typeof(RectTransform));
            child.layer = parent.gameObject.layer;
            var childRect = (RectTransform)child.transform;
            childRect.SetParent(parent, false);
            childRect.anchorMin = childRect.anchorMax = new Vector2(0f, 1f);
            childRect.pivot = new Vector2(0f, 1f);
            return childRect;
        }

        private static void Stretch(RectTransform target)
        {
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.offsetMin = target.offsetMax = Vector2.zero;
        }

        // Top-left based placement inside the content.
        private static void Place(RectTransform target, float x, float y, float w, float h)
        {
            target.anchoredPosition = new Vector2(x, -y);
            target.sizeDelta = new Vector2(w, h);
        }

        // ---- Layout ----

        private struct Plan
        {
            public bool TwoRows;
            public float Unit;   // arrow size
            public float Width;
            public float Height;
        }

        private void LayOut()
        {
            float height = Area.rect.height;
            if (height <= 1f)
                return;

            ApplyPlacement();

            Plan plan = MakePlan(Area.rect.width, height);
            float wantedWidth = plan.Width;   // what Fit Content asks a layout group for, even if it gets less
            if (size == SizeMode.FitContent && !ParentControlsWidth() && Mathf.Abs(Area.rect.width - plan.Width) > 0.5f)
                Area.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, plan.Width);

            // A layout group that sets a narrower width than the content needs: fit inside it instead.
            if (size == SizeMode.FitContent && Area.rect.width + 0.5f < plan.Width)
                plan = FillPlan(plan.TwoRows, Area.rect.width, height);

            if (size == SizeMode.FillRect && plan.Unit < TooSmallArrow && !warnedTooSmall)
            {
                warnedTooSmall = true;
                Debug.LogWarning($"[TimeWarpBar] '{name}' is {Area.rect.width:0} x {height:0}, so the arrows are only {plan.Unit:0.#} units. " +
                                 "Make the rect bigger, or set Size to Fit Content.", this);
            }

            float unit = plan.Unit;
            float arrowGap = unit * ArrowGap;
            float arrowsWidth = arrowCount * unit + (arrowCount - 1) * arrowGap;

            for (int i = 0; i < arrows.Count; i++)
                Place(arrows[i].rectTransform, padding + i * (unit + arrowGap), padding, unit, unit);

            speedLabel.fontSize = unit * SpeedFont;
            float speedWidth = speedWidthPerFont * speedLabel.fontSize;
            float speedX = padding + arrowsWidth + unit * SpeedGap;
            Place(speedLabel.rectTransform, speedX, padding, speedWidth, unit);

            if (showClock)
            {
                if (plan.TwoRows)
                {
                    clockLabel.fontSize = unit * TwoRowClockFont;
                    Place(clockLabel.rectTransform, padding, padding + unit * (1f + TwoRowGap), plan.Width - padding * 2f, unit * TwoRowClockHeight);
                }
                else
                {
                    clockLabel.fontSize = unit * OneRowClockFont;
                    Place(clockLabel.rectTransform, speedX + speedWidth + unit * ClockGap, padding, clockWidthPerFont * clockLabel.fontSize, unit);
                }
            }

            content.sizeDelta = new Vector2(plan.Width, plan.Height);
            content.localScale = Vector3.one;
            background.color = backgroundColor;
            background.enabled = backgroundColor.a > 0f;

            float spare = Mathf.Max(0f, Area.rect.width - plan.Width);
            float x = alignment == HorizontalAlign.Left ? 0f : alignment == HorizontalAlign.Center ? spare * 0.5f : spare;
            content.anchoredPosition = new Vector2(x, 0f);

            float reported = size == SizeMode.FitContent ? wantedWidth : -1f;
            if (Mathf.Abs(reported - contentWidth) > 0.5f)
            {
                contentWidth = reported;
                LayoutRebuilder.MarkLayoutForRebuild(Area);
            }
        }

        private Plan MakePlan(float rectWidth, float rectHeight)
        {
            if (size == SizeMode.FitContent)
            {
                bool twoRows = showClock && (rows == RowMode.TwoRows || (rows == RowMode.Auto && rectHeight >= twoRowMinHeight));
                float perUnitWidth, perUnitHeight;
                PerUnit(twoRows, out perUnitWidth, out perUnitHeight);
                return MakePlan(twoRows, Mathf.Max(0f, rectHeight - padding * 2f) / perUnitHeight, perUnitWidth, perUnitHeight);
            }

            Plan oneRow = FillPlan(false, rectWidth, rectHeight);
            if (!showClock || rows == RowMode.OneRow)
                return oneRow;

            Plan twoRowPlan = FillPlan(true, rectWidth, rectHeight);
            if (rows == RowMode.TwoRows)
                return twoRowPlan;

            return twoRowPlan.Unit > oneRow.Unit ? twoRowPlan : oneRow;
        }

        // The biggest layout of this shape that fits inside the rect.
        private Plan FillPlan(bool twoRows, float rectWidth, float rectHeight)
        {
            float perUnitWidth, perUnitHeight;
            PerUnit(twoRows, out perUnitWidth, out perUnitHeight);
            float byWidth = Mathf.Max(0f, rectWidth - padding * 2f) / perUnitWidth;
            float byHeight = Mathf.Max(0f, rectHeight - padding * 2f) / perUnitHeight;
            return MakePlan(twoRows, Mathf.Min(byWidth, byHeight), perUnitWidth, perUnitHeight);
        }

        private Plan MakePlan(bool twoRows, float unit, float perUnitWidth, float perUnitHeight)
        {
            return new Plan
            {
                TwoRows = twoRows,
                Unit = unit,
                Width = padding * 2f + perUnitWidth * unit,
                Height = padding * 2f + perUnitHeight * unit
            };
        }

        // Content width and height for an arrow size of 1.
        private void PerUnit(bool twoRows, out float perUnitWidth, out float perUnitHeight)
        {
            float row1 = arrowCount + (arrowCount - 1) * ArrowGap + SpeedGap + speedWidthPerFont * SpeedFont;
            if (!showClock)
            {
                perUnitWidth = row1;
                perUnitHeight = 1f;
            }
            else if (twoRows)
            {
                perUnitWidth = Mathf.Max(row1, clockWidthPerFont * TwoRowClockFont);
                perUnitHeight = 1f + TwoRowGap + TwoRowClockHeight;
            }
            else
            {
                perUnitWidth = row1 + ClockGap + clockWidthPerFont * OneRowClockFont;
                perUnitHeight = 1f;
            }
        }

        private static float WidthPerFontUnit(TextMeshProUGUI label, params string[] samples)
        {
            label.fontSize = MeasureFontSize;
            float widest = 0f;
            foreach (string sample in samples)
                widest = Mathf.Max(widest, label.GetPreferredValues(sample).x);
            return widest * 1.03f / MeasureFontSize;   // 3% slack against rounding
        }

        // Place In Parent: pin to the parent's left, centre or right edge, keeping the rect's size.
        private void ApplyPlacement()
        {
            if (placeInParent == ParentPlacement.LayoutGroup)
                return;

            float side = placeInParent == ParentPlacement.Left ? 0f : placeInParent == ParentPlacement.Center ? 0.5f : 1f;
            var point = new Vector2(side, 0.5f);
            float offset = placeInParent == ParentPlacement.Left ? edgeMargin : placeInParent == ParentPlacement.Right ? -edgeMargin : 0f;

            if (Area.anchorMin != point || Area.anchorMax != point || Area.pivot != point)
            {
                Vector2 keep = Area.rect.size;
                Area.anchorMin = Area.anchorMax = point;
                Area.pivot = point;
                Area.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, keep.x);
                Area.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, keep.y);
            }

            var position = new Vector2(offset, 0f);
            if (Area.anchoredPosition != position)
                Area.anchoredPosition = position;
        }

        // A layout group that sizes its children sets this rect's width from preferredWidth instead.
        private bool ParentControlsWidth()
        {
            Transform parent = transform.parent;
            if (parent == null)
                return false;

            var group = parent.GetComponent<LayoutGroup>();
            if (group == null || !group.isActiveAndEnabled)
                return false;

            var linear = group as HorizontalOrVerticalLayoutGroup;
            return linear == null || linear.childControlWidth;
        }

        // ---- Per frame ----

        private void Refresh()
        {
            int level = CurrentLevel();
            Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1f, -1f);
            Camera eventCamera = EventCamera();

            for (int i = 0; i < arrows.Count; i++)
            {
                Color color = i + 1 <= level ? litColor : unlitColor;
                if (RectTransformUtility.RectangleContainsScreenPoint(arrows[i].rectTransform, mouse, eventCamera))
                    color = Color.Lerp(color, Color.white, 0.35f);
                if (arrows[i].color != color)
                    arrows[i].color = color;
            }

            // Lagging: the simulation can't keep up, so show the speed it actually reaches.
            bool lagging = run.State == TrainingState.Running && meter.IsBehind(run.Speed);
            bool paused = run.State == TrainingState.Paused;
            SetText(speedLabel, lagging ? $"{meter.Actual:0.0}x" : $"{level}x");
            Color speedTint = lagging ? laggingColor : paused ? Color.Lerp(speedColor, unlitColor, 0.6f) : speedColor;
            if (speedLabel.color != speedTint)
                speedLabel.color = speedTint;

            if (showClock)
            {
                SetText(clockLabel, RunClock.Text(run));
                if (clockLabel.color != clockColor)
                    clockLabel.color = clockColor;
            }
        }

        private static void SetText(TextMeshProUGUI label, string text)
        {
            if (label.text != text)
                label.text = text;
        }

        private Camera EventCamera()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            canvas = canvas != null ? canvas.rootCanvas : null;
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (!keyboardShortcuts || keyboard == null || keyFrame == Time.frameCount || IsTyping())
                return;

            if (keyboard[speedUpKey].wasPressedThisFrame)
            {
                keyFrame = Time.frameCount;
                SetLevel(CurrentLevel() + 1);
            }
            else if (keyboard[slowDownKey].wasPressedThisFrame)
            {
                keyFrame = Time.frameCount;
                SetLevel(CurrentLevel() - 1);
            }
        }

        private int CurrentLevel()
        {
            return Mathf.Clamp(Mathf.RoundToInt(run.Speed), 1, arrowCount);
        }

        private void SetLevel(int level)
        {
            if (run != null)
                run.SetTimeScale(Mathf.Clamp(level, 1, arrowCount));
        }

        private static bool IsTyping()
        {
            if (GUIUtility.keyboardControl != 0)
                return true;

            EventSystem events = EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;
            return selected != null && selected.GetComponent<TMP_InputField>() != null;
        }

        // Settings that have no effect with the current choices; the Inspector hides them.
        internal static List<string> UnusedFields(SizeMode? sizeMode, ParentPlacement? placement, RowMode? rowMode)
        {
            var unused = new List<string>();
            if (sizeMode == SizeMode.FitContent)
                unused.Add(nameof(alignment));
            if (placement == ParentPlacement.LayoutGroup || placement == ParentPlacement.Center)
                unused.Add(nameof(edgeMargin));
            if (sizeMode == SizeMode.FillRect || (rowMode.HasValue && rowMode != RowMode.Auto))
                unused.Add(nameof(twoRowMinHeight));
            return unused;
        }

        // ---- ILayoutElement: layout groups size this rect to the content's width ----

        public void CalculateLayoutInputHorizontal() { }
        public void CalculateLayoutInputVertical() { }
        public float minWidth => -1f;
        public float preferredWidth => size == SizeMode.FitContent && contentWidth > 0f ? contentWidth : -1f;
        public float flexibleWidth => -1f;
        public float minHeight => -1f;
        public float preferredHeight => -1f;
        public float flexibleHeight => -1f;
        // Added to ILayoutElement in uGUI 2.x (Unity 6). -1 = no maximum.
        public float maxWidth => -1f;
        public float maxHeight => -1f;
        public int layoutPriority => 1;

        // ILayoutIgnorer: when pinned to a side, the parent's layout group leaves this rect alone.
        public bool ignoreLayout => placeInParent != ParentPlacement.LayoutGroup;
    }

#if UNITY_EDITOR
    // Draws TimeWarpBar's usual Inspector, minus the settings that don't apply to the current choices.
    [UnityEditor.CustomEditor(typeof(TimeWarpBar))]
    [UnityEditor.CanEditMultipleObjects]
    internal class TimeWarpBarEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            List<string> hidden = TimeWarpBar.UnusedFields(
                EnumValue<TimeWarpBar.SizeMode>("size"),
                EnumValue<TimeWarpBar.ParentPlacement>("placeInParent"),
                EnumValue<TimeWarpBar.RowMode>("rows"));
            DrawPropertiesExcluding(serializedObject, hidden.ToArray());
            serializedObject.ApplyModifiedProperties();
        }

        // Null when several selected objects have different values (then nothing is hidden for it).
        private T? EnumValue<T>(string field) where T : struct
        {
            UnityEditor.SerializedProperty property = serializedObject.FindProperty(field);
            if (property == null || property.hasMultipleDifferentValues)
                return null;
            return (T)System.Enum.ToObject(typeof(T), property.intValue);
        }
    }
#endif
}
