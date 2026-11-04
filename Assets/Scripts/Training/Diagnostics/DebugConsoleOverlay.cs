using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Training
{
    public class DebugConsoleOverlay : MonoBehaviour
    {
        private const float RebuildInterval = 0.2f;
        private const float Margin = 6f;
        private const float RowHeight = 22f;
        private const float RowGap = 2f;

        [SerializeField] private Key toggleKey = Key.F3;
        [SerializeField] private bool startVisible;
        [Tooltip("Fraction of the screen width the console covers, from the right edge.")]
        [SerializeField, Range(0.2f, 1f)] private float screenWidth = 0.5f;
        [Tooltip("Fraction of the screen height the console covers, from the bottom edge.")]
        [SerializeField, Range(0.2f, 1f)] private float screenHeight = 0.45f;
        [SerializeField, Min(100)] private int maxMessages = 2000;
        [SerializeField, Min(8)] private int fontSize = 13;
        [SerializeField, Range(0f, 1f)] private float backgroundOpacity = 0.85f;
        [Tooltip("Start with only backend messages shown. Errors from anywhere are always shown.")]
        [SerializeField] private bool backendOnly = true;
        [Tooltip("Messages that start with [Tag] for one of these tags count as backend messages.")]
        [SerializeField] private string[] backendTags =
        {
            "Training", "Trainer", "Python setup", "MLAgentsPortPatcher",
            "RockPlacer", "RockField", "RockShape", "InstancedRenderer",
            "TrainingControls", "TrainingSpeedControl"
        };

        private readonly List<LogEntry> snapshot = new List<LogEntry>();
        private readonly List<ConsoleLine> lines = new List<ConsoleLine>();
        private readonly Dictionary<LogType, GUIStyle> styles = new Dictionary<LogType, GUIStyle>();

        private LogBuffer buffer;
        private LogFilter filter;
        private GUIStyle stackStyle;
        private Texture2D background;
        private bool visible;
        private bool showStackTraces;
        private bool follow = true;
        private bool filterChanged = true;
        private int builtVersion = -1;
        private int widestLine;
        private int infoCount;
        private int warningCount;
        private int errorCount;
        private float nextRebuild;
        private Vector2 scroll;

        public bool Visible
        {
            get => visible;
            set => visible = value;
        }

        private void Awake()
        {
            buffer = new LogBuffer(maxMessages);
            filter = new LogFilter(backendTags) { BackendOnly = backendOnly };
            visible = startVisible;
            Application.logMessageReceivedThreaded += buffer.Add;
        }

        private void OnDestroy()
        {
            if (buffer != null)
                Application.logMessageReceivedThreaded -= buffer.Add;
            if (background != null)
                Destroy(background);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
                visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible)
                return;

            EnsureStyles();
            RebuildIfNeeded();

            GUI.depth = -1000;
            float width = Screen.width * screenWidth;
            float height = Screen.height * screenHeight;
            var panel = new Rect(Screen.width - width, Screen.height - height, width, height);
            GUI.DrawTexture(panel, background);

            var filters = new Rect(panel.x + Margin, panel.y + 4f, panel.width - Margin * 2f, RowHeight);
            var actions = new Rect(filters.x, filters.yMax + RowGap, filters.width, RowHeight);
            DrawFilters(filters);
            DrawActions(actions);

            var view = new Rect(panel.x + Margin, actions.yMax + 4f, panel.width - Margin * 2f, panel.yMax - actions.yMax - Margin);
            DrawLines(view);
        }

        private void DrawFilters(Rect area)
        {
            GUILayout.BeginArea(area);
            GUILayout.BeginHorizontal();

            GUILayout.Label($"Console ({toggleKey})", GUILayout.ExpandWidth(false));
            GUILayout.Space(8f);
            filter.ShowInfo = Toggle(filter.ShowInfo, $"Info {infoCount}");
            filter.ShowWarnings = Toggle(filter.ShowWarnings, $"Warnings {warningCount}");
            filter.ShowErrors = Toggle(filter.ShowErrors, $"Errors {errorCount}");
            filter.BackendOnly = Toggle(filter.BackendOnly, "Backend only");
            showStackTraces = Toggle(showStackTraces, "Stack traces");

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawActions(Rect area)
        {
            GUILayout.BeginArea(area);
            GUILayout.BeginHorizontal();

            GUILayout.Label("Search", GUILayout.ExpandWidth(false));
            string search = GUILayout.TextField(filter.Search, GUILayout.MinWidth(120f), GUILayout.ExpandWidth(true));
            if (search != filter.Search)
            {
                filter.Search = search;
                filterChanged = true;
            }

            GUILayout.Space(8f);
            if (GUILayout.Button(follow ? "Following" : "Follow", GUILayout.Width(80f)))
                follow = true;
            if (GUILayout.Button("Copy", GUILayout.Width(60f)))
                GUIUtility.systemCopyBuffer = ConsoleLines.ToText(lines);
            if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                buffer.Clear();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private bool Toggle(bool value, string label)
        {
            bool result = GUILayout.Toggle(value, " " + label, GUILayout.ExpandWidth(false));
            if (result != value)
                filterChanged = true;

            GUILayout.Space(8f);
            return result;
        }

        private void DrawLines(Rect view)
        {
            float lineHeight = fontSize + 5f;
            float contentWidth = Mathf.Max(view.width - 20f, widestLine * fontSize * 0.6f + 20f);
            var content = new Rect(0f, 0f, contentWidth, lines.Count * lineHeight);
            float bottom = Mathf.Max(0f, content.height - VisibleHeight(view, content));

            if (follow)
                scroll.y = bottom;

            Vector2 before = scroll;
            scroll = GUI.BeginScrollView(view, scroll, content);

            int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / lineHeight));
            int last = Mathf.Min(lines.Count, first + Mathf.CeilToInt(view.height / lineHeight) + 1);
            for (int i = first; i < last; i++)
            {
                ConsoleLine line = lines[i];
                GUI.Label(new Rect(4f, i * lineHeight, contentWidth, lineHeight), line.Text, line.IsStackTrace ? stackStyle : StyleFor(line.Type));
            }

            GUI.EndScrollView();

            if (!Mathf.Approximately(scroll.y, before.y))
                follow = scroll.y >= bottom - 1f;
        }

        private static float VisibleHeight(Rect view, Rect content)
        {
            GUIStyle vertical = GUI.skin.verticalScrollbar;
            GUIStyle horizontal = GUI.skin.horizontalScrollbar;
            float verticalWidth = vertical.fixedWidth + vertical.margin.left;
            float horizontalHeight = horizontal.fixedHeight + horizontal.margin.top;

            bool hasVertical = content.height > view.height;
            bool hasHorizontal = content.width > view.width - (hasVertical ? verticalWidth : 0f);
            return hasHorizontal ? view.height - horizontalHeight : view.height;
        }

        private void RebuildIfNeeded()
        {
            if (Event.current.type != EventType.Layout)
                return;

            bool changed = buffer.Version != builtVersion;
            if (!filterChanged && (!changed || Time.unscaledTime < nextRebuild))
                return;

            builtVersion = buffer.CopyTo(snapshot);
            ConsoleLines.Count(snapshot, out infoCount, out warningCount, out errorCount);
            widestLine = ConsoleLines.Build(snapshot, filter, showStackTraces, lines);
            filterChanged = false;
            nextRebuild = Time.unscaledTime + RebuildInterval;
        }

        private GUIStyle StyleFor(LogType type)
        {
            switch (type)
            {
                case LogType.Warning:
                    return styles[LogType.Warning];
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    return styles[LogType.Error];
                default:
                    return styles[LogType.Log];
            }
        }

        private void EnsureStyles()
        {
            if (stackStyle != null)
                return;

            styles[LogType.Log] = LineStyle(new Color(0.88f, 0.88f, 0.88f));
            styles[LogType.Warning] = LineStyle(new Color(1f, 0.82f, 0.35f));
            styles[LogType.Error] = LineStyle(new Color(1f, 0.45f, 0.42f));
            stackStyle = LineStyle(new Color(0.6f, 0.6f, 0.6f));

            background = new Texture2D(1, 1);
            background.SetPixel(0, 0, new Color(0.06f, 0.06f, 0.07f, backgroundOpacity));
            background.Apply();
        }

        private GUIStyle LineStyle(Color color)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                wordWrap = false,
                richText = false,
                clipping = TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            style.normal.textColor = color;
            return style;
        }
    }
}
