using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Training
{
    public class TrainingSpeedControl : MonoBehaviour
    {
        [SerializeField] private TrainingOrganizer organizer;
        [Tooltip("Optional. Its min and max set the speed range, for example 1 to 20.")]
        [SerializeField] private Slider slider;
        [Tooltip("Optional. Typing a number sets the speed.")]
        [SerializeField] private TMP_InputField input;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Button fasterButton;
        [SerializeField] private Button slowerButton;
        [SerializeField] private string labelFormat = "Speed {0}x";

        private ISimulationRun run;
        private bool updating;

        private void Awake()
        {
            if (organizer == null)
                organizer = FindAnyObjectByType<TrainingOrganizer>();
            run = SimulationRuns.Resolve(organizer);
        }

        private void OnEnable()
        {
            if (run == null)
            {
                Debug.LogWarning($"[TrainingSpeedControl] No {nameof(TrainingOrganizer)} found.", this);
                return;
            }

            if (slider != null)
                slider.onValueChanged.AddListener(OnSliderChanged);
            if (input != null)
                input.onEndEdit.AddListener(run.SetTimeScale);
            if (fasterButton != null)
                fasterButton.onClick.AddListener(run.SpeedUp);
            if (slowerButton != null)
                slowerButton.onClick.AddListener(run.SlowDown);

            run.SpeedChanged += Show;
            Show(run.Speed);
        }

        private void OnDisable()
        {
            if (run == null)
                return;

            if (slider != null)
                slider.onValueChanged.RemoveListener(OnSliderChanged);
            if (input != null)
                input.onEndEdit.RemoveListener(run.SetTimeScale);
            if (fasterButton != null)
                fasterButton.onClick.RemoveListener(run.SpeedUp);
            if (slowerButton != null)
                slowerButton.onClick.RemoveListener(run.SlowDown);

            run.SpeedChanged -= Show;
        }

        private void OnSliderChanged(float value)
        {
            if (!updating)
                run.SetTimeScale(value);
        }

        private void Show(float speed)
        {
            updating = true;

            if (slider != null)
                slider.SetValueWithoutNotify(speed);
            if (input != null)
                input.SetTextWithoutNotify(Format(speed));
            if (label != null)
                label.text = string.Format(labelFormat, Format(speed));

            updating = false;
        }

        private static string Format(float speed)
        {
            return speed.ToString(speed < 10f ? "0.#" : "0", CultureInfo.InvariantCulture);
        }
    }
}
