using System.IO;
using TMPro;
using UnityEngine;

namespace Evaluation
{
    // Shows the report when an evaluation ends. With a TMP text (and optionally a panel to switch on) it
    // fills those in; otherwise it draws its own window. Add it to any object for a quick look at results
    // until the Evaluation panel shows them.
    public class EvaluationReportPanel : MonoBehaviour
    {
        [SerializeField] private EvaluationOrganizer organizer;
        [Tooltip("Optional. Receives the summary and per-map table. Leave empty to use the built-in window.")]
        [SerializeField] private TMP_Text reportText;
        [Tooltip("Optional. Switched on when a report arrives.")]
        [SerializeField] private GameObject panel;

        [Header("Built-in window")]
        [SerializeField, Min(200f)] private float windowWidth = 520f;

        private EvaluationReport report;
        private string reportPath;
        private bool showWindow;
        private Vector2 scroll;
        private GUIStyle monoStyle;
        // Any fixed number works; IMGUI only needs window IDs to be unique among open windows.
        private const int WindowId = 0x45564152;

        private void Awake()
        {
            if (organizer == null)
            {
                organizer = GetComponent<EvaluationOrganizer>();
                if (organizer == null)
                    organizer = FindAnyObjectByType<EvaluationOrganizer>();
            }
        }

        private void OnEnable()
        {
            if (organizer != null)
                organizer.Finished += Show;
        }

        private void OnDisable()
        {
            if (organizer != null)
                organizer.Finished -= Show;
        }

        public void Hide()
        {
            showWindow = false;
            if (panel != null)
                panel.SetActive(false);
        }

        private void Show(EvaluationReport finished, string path)
        {
            report = finished;
            reportPath = path;
            string text = Describe();

            if (reportText != null)
            {
                reportText.text = text;
                if (panel != null)
                    panel.SetActive(true);
            }
            else
            {
                showWindow = true;
                scroll = Vector2.zero;
            }
        }

        private string Describe()
        {
            string text = report.Summary() + "\n\n" + report.TileTable();
            if (reportPath != null)
                text += $"\n\nSaved to {reportPath}\n(and a .csv with every attempt)";
            return text;
        }

        private void OnGUI()
        {
            if (!showWindow || report == null)
                return;

            if (monoStyle == null)
            {
                monoStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 13, richText = false };
                Font mono = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Menlo", "DejaVu Sans Mono", "Courier New" }, 13);
                if (mono != null)
                    monoStyle.font = mono;
            }

            float width = Mathf.Min(windowWidth, Screen.width - 40f);
            float height = Mathf.Min(420f, Screen.height - 80f);
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.ModalWindow(WindowId, rect, DrawWindow, "Evaluation report");
        }

        private void DrawWindow(int id)
        {
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label(Describe(), monoStyle);
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (reportPath != null && GUILayout.Button("Open folder"))
                Application.OpenURL(new System.Uri(Path.GetDirectoryName(reportPath)).AbsoluteUri);
            if (GUILayout.Button("Close"))
                Hide();
            GUILayout.EndHorizontal();
        }
    }
}
