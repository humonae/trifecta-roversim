using System.Globalization;
using TMPro;
using UnityEngine;

namespace Training
{
    public class TrainingStatusLabel : MonoBehaviour
    {
        private const float RefreshInterval = 0.5f;

        [SerializeField] private TrainingOrganizer organizer;
        [SerializeField] private TMP_Text label;
        [SerializeField] private bool showTimers = true;
        [SerializeField] private bool showSpeed = true;

        private readonly SpeedMeter meter = new SpeedMeter(RefreshInterval);
        private ISimulationRun run;
        private float nextRefresh;

        private void Awake()
        {
            if (organizer == null)
                organizer = FindAnyObjectByType<TrainingOrganizer>();
            run = SimulationRuns.Resolve(organizer);
            if (label == null)
                label = GetComponent<TMP_Text>();
        }

        private void Update()
        {
            if (run == null || label == null || Time.unscaledTime < nextRefresh)
                return;

            meter.Sample();
            nextRefresh = Time.unscaledTime + RefreshInterval;
            label.text = Describe();
        }

        private string Describe()
        {
            bool live = run.State == TrainingState.Running || run.State == TrainingState.Paused;
            string text = run.Status;
            if (!live)
                return text;

            string detail = run.ProgressDetail;
            if (showTimers && !string.IsNullOrEmpty(detail))
                text += "\n" + detail;

            if (showSpeed && run.State == TrainingState.Running)
            {
                text += $"\nSpeed {Format(run.Speed)}x";
                if (meter.IsBehind(run.Speed))
                    text += $" (running at {Format(meter.Actual)}x, the computer can't keep up)";
            }

            return text;
        }

        private static string Format(float speed)
        {
            return speed.ToString(speed < 10f ? "0.#" : "0", CultureInfo.InvariantCulture);
        }
    }
}
