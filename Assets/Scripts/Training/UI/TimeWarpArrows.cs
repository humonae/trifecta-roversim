using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Training
{
    public class TimeWarpArrows : MonoBehaviour
    {
        private const int ArrowTextureSize = 32;
        private const float Padding = 6f;
        private const float RowGap = 3f;

        [SerializeField] private TrainingOrganizer organizer;

        [Header("Speeds")]
        [Tooltip("One arrow per step. Arrow N sets the speed to N times real time.")]
        [SerializeField, Range(2, 20)] private int arrowCount = 10;
        [SerializeField] private Key speedUpKey = Key.Period;
        [SerializeField] private Key slowDownKey = Key.Comma;

        [Header("Placement")]
        [Tooltip("Optional. The widget sits just below this, for example the GlobalHeader bar.")]
        [SerializeField] private RectTransform placeBelow;
        [Tooltip("Distance from the top of the screen when Place Below is empty.")]
        [SerializeField, Min(0f)] private float topOffset = 72f;
        [SerializeField, Min(0f)] private float rightMargin = 12f;
        [SerializeField, Min(0f)] private float gapBelow = 6f;

        [Header("Look")]
        [SerializeField, Min(8f)] private float arrowSize = 18f;
        [SerializeField, Min(0f)] private float arrowGap = 2f;
        [SerializeField] private bool showClock = true;
        [SerializeField] private Color litColor = new Color(0.36f, 0.92f, 0.36f);
        [SerializeField] private Color unlitColor = new Color(0.32f, 0.34f, 0.32f);
        [SerializeField] private Color laggingColor = new Color(1f, 0.72f, 0.25f);
        [SerializeField, Range(0f, 1f)] private float backgroundOpacity = 0.8f;

        private readonly SpeedMeter meter = new SpeedMeter(0.5f);
        private readonly Vector3[] corners = new Vector3[4];
        private Texture2D arrow;
        private Texture2D background;
        private GUIStyle labelStyle;
        private GUIStyle clockStyle;

        private void Awake()
        {
            if (organizer == null)
                organizer = FindAnyObjectByType<TrainingOrganizer>();
        }

        private void OnDestroy()
        {
            if (arrow != null)
                Destroy(arrow);
            if (background != null)
                Destroy(background);
        }

        private void Update()
        {
            if (organizer == null)
                return;

            meter.Sample();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || IsTyping())
                return;

            if (keyboard[speedUpKey].wasPressedThisFrame)
                SetLevel(CurrentLevel() + 1);
            else if (keyboard[slowDownKey].wasPressedThisFrame)
                SetLevel(CurrentLevel() - 1);
        }

        private void OnGUI()
        {
            if (organizer == null)
                return;

            EnsureResources();
            GUI.depth = -900;

            float arrowsWidth = arrowCount * arrowSize + (arrowCount - 1) * arrowGap;
            float labelWidth = 70f;
            float width = Padding * 2f + arrowsWidth + 8f + labelWidth;
            float height = Padding * 2f + arrowSize + (showClock ? RowGap + 16f : 0f);
            var panel = new Rect(Screen.width - rightMargin - width, Top(), width, height);
            GUI.DrawTexture(panel, background);

            int level = CurrentLevel();
            float y = panel.y + Padding;
            for (int i = 1; i <= arrowCount; i++)
            {
                var rect = new Rect(panel.x + Padding + (i - 1) * (arrowSize + arrowGap), y, arrowSize, arrowSize);
                bool hovered = rect.Contains(Event.current.mousePosition);
                Color color = i <= level ? litColor : unlitColor;
                GUI.color = hovered ? Color.Lerp(color, Color.white, 0.35f) : color;
                GUI.DrawTexture(rect, arrow);
                GUI.color = Color.white;

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                    SetLevel(i);
            }

            bool lagging = organizer.State == TrainingState.Running && meter.IsBehind(organizer.Speed);
            labelStyle.normal.textColor = lagging ? laggingColor : Color.white;
            string label = lagging ? $"{level}x ({meter.Actual:0.#}x)" : organizer.State == TrainingState.Paused ? $"{level}x ||" : $"{level}x";
            GUI.Label(new Rect(panel.x + Padding + arrowsWidth + 8f, y, labelWidth, arrowSize), label, labelStyle);

            if (showClock)
                GUI.Label(new Rect(panel.x + Padding, y + arrowSize + RowGap, width - Padding * 2f, 16f), Clock(organizer.SimulatedSeconds), clockStyle);
        }

        private int CurrentLevel()
        {
            return Mathf.Clamp(Mathf.RoundToInt(organizer.Speed), 1, arrowCount);
        }

        private void SetLevel(int level)
        {
            organizer.SetTimeScale(Mathf.Clamp(level, 1, arrowCount));
        }

        private float Top()
        {
            if (placeBelow == null)
                return topOffset;

            placeBelow.GetWorldCorners(corners);
            Canvas canvas = placeBelow.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 bottom = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            return Screen.height - bottom.y + gapBelow;
        }

        private static string Clock(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return $"T+ {total / 3600:00}:{total / 60 % 60:00}:{total % 60:00}";
        }

        private static bool IsTyping()
        {
            if (GUIUtility.keyboardControl != 0)
                return true;

            EventSystem events = EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;
            return selected != null && selected.GetComponent<TMP_InputField>() != null;
        }

        private void EnsureResources()
        {
            if (arrow != null)
                return;

            arrow = ArrowTexture.Create(ArrowTextureSize);
            background = new Texture2D(1, 1);
            background.SetPixel(0, 0, new Color(0.06f, 0.06f, 0.07f, backgroundOpacity));
            background.Apply();

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            clockStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            clockStyle.normal.textColor = new Color(0.36f, 0.92f, 0.36f);
        }
    }
}
