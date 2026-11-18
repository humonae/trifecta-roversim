using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Training
{
    // One button on a PopupDialog. The popup closes before OnClick runs, unless KeepOpen is set.
    public struct PopupButton
    {
        public string Label;
        public Action OnClick;
        public bool Primary;
        public bool KeepOpen;

        public PopupButton(string label, Action onClick = null, bool primary = false, bool keepOpen = false)
        {
            Label = label;
            OnClick = onClick;
            Primary = primary;
            KeepOpen = keepOpen;
        }
    }

    // A modal popup in the middle of the screen: title, message, optional progress bar with a
    // "Please wait." line, and buttons. It builds its own canvas, so it works without any scene setup;
    // FindOrCreate adds one when the scene has none. Add the component to an object yourself to style it.
    public class PopupDialog : MonoBehaviour
    {
        [Header("Style (all optional)")]
        [Tooltip("Body and button text. Empty uses the TextMesh Pro default font.")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Title text. Empty uses Font, in bold.")]
        [SerializeField] private TMP_FontAsset titleFont;
        [Tooltip("9-sliced background for the window. Empty uses a generated rounded rectangle.")]
        [SerializeField] private Sprite panelSprite;
        [Tooltip("9-sliced background for buttons and the progress bar. Empty uses a generated rounded rectangle.")]
        [SerializeField] private Sprite buttonSprite;
        [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.6f);
        [SerializeField] private Color panelColor = new Color(0.09f, 0.09f, 0.09f, 1f);
        [SerializeField] private Color borderColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color secondaryTextColor = new Color(0.694f, 0.694f, 0.694f, 1f);
        [SerializeField] private Color accentColor = new Color(0.31f, 0.765f, 0.878f, 1f);
        [SerializeField] private Color accentTextColor = new Color(0.09f, 0.09f, 0.09f, 1f);
        [SerializeField] private Color buttonColor = new Color(0.165f, 0.165f, 0.165f, 1f);
        [SerializeField] private Color barTrackColor = new Color(0.2f, 0.2f, 0.2f, 1f);

        [Header("Layout")]
        [SerializeField, Min(300f)] private float width = 560f;
        [Tooltip("Seconds between dots in \"Please wait...\".")]
        [SerializeField, Min(0.1f)] private float dotInterval = 0.5f;

        private const string WaitText = "Please wait";

        private static readonly Dictionary<int, Sprite> roundedSprites = new Dictionary<int, Sprite>();

        private GameObject canvasObject;
        private RectTransform window;
        private TextMeshProUGUI title;
        private TextMeshProUGUI message;
        private GameObject progressGroup;
        private RectTransform progressFill;
        private TextMeshProUGUI waitLabel;
        private RectTransform buttonRow;
        private readonly List<GameObject> buttons = new List<GameObject>();

        private Func<string> progressDetail;
        private Func<float> progressValue;
        private float progressStarted;

        public bool IsOpen => canvasObject != null && canvasObject.activeSelf;
        public bool ShowsProgress => IsOpen && progressValue != null;

        public static PopupDialog FindOrCreate()
        {
            PopupDialog existing = FindAnyObjectByType<PopupDialog>(FindObjectsInactive.Include);
            if (existing != null)
                return existing;

            return new GameObject("Popup Dialog").AddComponent<PopupDialog>();
        }

        public void Show(string titleText, string messageText, params PopupButton[] buttonSpecs)
        {
            Build();
            progressDetail = null;
            progressValue = null;
            progressGroup.SetActive(false);
            SetTexts(titleText, messageText);
            SetButtons(buttonSpecs);
            Open();
        }

        // A popup with a progress bar and an animated "Please wait." line. detail and progress are read
        // every frame; progress goes from 0 to 1.
        public void ShowProgress(string titleText, Func<string> detail, Func<float> progress, params PopupButton[] buttonSpecs)
        {
            Build();
            progressDetail = detail;
            progressValue = progress;
            progressStarted = Time.unscaledTime;
            progressGroup.SetActive(true);
            SetTexts(titleText, detail != null ? detail() : "");
            SetFill(progress != null ? progress() : 0f);
            waitLabel.text = WaitText + ".";
            SetButtons(buttonSpecs);
            Open();
        }

        public void Hide()
        {
            progressDetail = null;
            progressValue = null;
            if (canvasObject != null)
                canvasObject.SetActive(false);
        }

        private void Update()
        {
            if (!ShowsProgress)
                return;

            if (progressDetail != null)
            {
                string detail = progressDetail();
                if (detail != message.text)
                    SetMessage(detail);
            }

            SetFill(progressValue());

            // "Please wait." -> ".." -> "..." -> "." again, so it's obvious the app hasn't frozen.
            int dots = 1 + (int)((Time.unscaledTime - progressStarted) / dotInterval) % 3;
            string wait = WaitText + new string('.', dots);
            if (waitLabel.text != wait)
                waitLabel.text = wait;
        }

        private void Open()
        {
            EnsureEventSystem();
            canvasObject.SetActive(true);
            canvasObject.transform.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(window);
        }

        private void SetTexts(string titleText, string messageText)
        {
            title.text = titleText ?? "";
            SetMessage(messageText);
        }

        private void SetMessage(string messageText)
        {
            message.text = messageText ?? "";
            message.gameObject.SetActive(!string.IsNullOrEmpty(messageText));
        }

        private void SetFill(float value)
        {
            progressFill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        }

        private void SetButtons(PopupButton[] specs)
        {
            foreach (GameObject old in buttons)
                Destroy(old);
            buttons.Clear();

            if (specs != null)
            {
                foreach (PopupButton spec in specs)
                    buttons.Add(CreateButton(spec));
            }

            buttonRow.gameObject.SetActive(buttons.Count > 0);
        }

        private GameObject CreateButton(PopupButton spec)
        {
            var go = new GameObject(spec.Label + " Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(buttonRow, false);

            var image = go.GetComponent<Image>();
            image.sprite = buttonSprite != null ? buttonSprite : Rounded(8);
            image.type = Image.Type.Sliced;
            image.color = spec.Primary ? accentColor : buttonColor;

            var button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            button.colors = colors;

            TextMeshProUGUI label = CreateText("Label", go.transform, 20f, spec.Primary ? accentTextColor : textColor, font, FontStyles.Normal);
            label.text = spec.Label;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(label.rectTransform);

            var layout = go.GetComponent<LayoutElement>();
            layout.minWidth = 110f;
            layout.preferredWidth = Mathf.Max(110f, label.GetPreferredValues(spec.Label).x + 40f);
            layout.preferredHeight = 40f;
            layout.minHeight = 40f;

            PopupButton captured = spec;
            button.onClick.AddListener(() =>
            {
                if (!captured.KeepOpen)
                    Hide();
                captured.OnClick?.Invoke();
            });
            return go;
        }

        private void Build()
        {
            if (canvasObject != null)
                return;

            canvasObject = new GameObject("Popup Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            // Covers the screen so nothing behind the popup can be clicked.
            Image dim = CreateImage("Dim", canvasObject.transform, null, dimColor);
            Stretch(dim.rectTransform);

            Image border = CreateImage("Window", canvasObject.transform, panelSprite != null ? panelSprite : Rounded(14), borderColor);
            window = border.rectTransform;
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
            window.sizeDelta = new Vector2(width, 0f);
            var borderLayout = border.gameObject.AddComponent<VerticalLayoutGroup>();
            borderLayout.padding = new RectOffset(1, 1, 1, 1);
            borderLayout.childControlWidth = borderLayout.childControlHeight = true;
            borderLayout.childForceExpandWidth = true;
            borderLayout.childForceExpandHeight = false;
            var fitter = border.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image panel = CreateImage("Panel", window, panelSprite != null ? panelSprite : Rounded(14), panelColor);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 24, 24);
            layout.spacing = 16f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            title = CreateText("Title", panel.transform, 28f, textColor, titleFont != null ? titleFont : font,
                titleFont != null ? FontStyles.Normal : FontStyles.Bold);

            message = CreateText("Message", panel.transform, 20f, secondaryTextColor, font, FontStyles.Normal);
            // Messages can contain things like "<=2.8.0" from pip, so don't treat them as tags.
            message.richText = false;

            progressGroup = new GameObject("Progress", typeof(RectTransform), typeof(VerticalLayoutGroup));
            progressGroup.transform.SetParent(panel.transform, false);
            var progressLayout = progressGroup.GetComponent<VerticalLayoutGroup>();
            progressLayout.spacing = 10f;
            progressLayout.childControlWidth = progressLayout.childControlHeight = true;
            progressLayout.childForceExpandWidth = true;
            progressLayout.childForceExpandHeight = false;

            Image track = CreateImage("Bar", progressGroup.transform, buttonSprite != null ? buttonSprite : Rounded(5), barTrackColor);
            var trackLayout = track.gameObject.AddComponent<LayoutElement>();
            trackLayout.minHeight = trackLayout.preferredHeight = 10f;
            Image fill = CreateImage("Fill", track.transform, buttonSprite != null ? buttonSprite : Rounded(5), accentColor);
            progressFill = fill.rectTransform;
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = new Vector2(0f, 1f);
            progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;

            waitLabel = CreateText("Please Wait", progressGroup.transform, 20f, textColor, font, FontStyles.Normal);
            waitLabel.text = WaitText + ".";

            var row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(panel.transform, false);
            buttonRow = (RectTransform)row.transform;
            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f;
            rowLayout.childAlignment = TextAnchor.MiddleRight;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
            rowLayout.padding = new RectOffset(0, 0, 6, 0);

            canvasObject.SetActive(false);
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float size, Color color, TMP_FontAsset fontAsset, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            // Set it explicitly: the popup is built while hidden, before TextMesh Pro would load its default
            // font, and button widths are measured right away.
            TMP_FontAsset chosen = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
            if (chosen != null)
                text.font = chosen;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // Buttons need an EventSystem. The RoverSim scene has one; this covers scenes that don't.
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        // A white rounded rectangle, 9-sliced so it stretches to any size without blurring the corners.
        private static Sprite Rounded(int radius)
        {
            Sprite sprite;
            if (roundedSprites.TryGetValue(radius, out sprite) && sprite != null)
                return sprite;

            int size = radius * 2 + 2;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = $"Popup Rounded {radius}"
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance past the nearest corner's centre; zero along the straight edges and inside.
                    float cx = Mathf.Max(0f, Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (size - radius)));
                    float cy = Mathf.Max(0f, Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (size - radius)));
                    float alpha = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(cx * cx + cy * cy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            float border = radius + 1;
            sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            roundedSprites[radius] = sprite;
            return sprite;
        }
    }
}
