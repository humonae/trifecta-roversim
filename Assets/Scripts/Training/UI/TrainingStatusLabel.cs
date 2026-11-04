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
        private float nextRefresh;

        private void Awake()
        {
            if (organizer == null)
                organizer = FindAnyObjectByType<TrainingOrganizer>();
            if (label == null)
                label = GetComponent<TMP_Text>();
        }

        private void Update()
        {
            if (organizer == null || label == null || Time.unscaledTime < nextRefresh)
                return;

            meter.Sample();
            nextRefresh = Time.unscaledTime + RefreshInterval;
            label.text = Describe();
        }

        private string Describe()
        {
            bool live = organizer.State == TrainingState.Running || organizer.State == TrainingState.Paused;
            if (!live || organizer.CurrentEpoch == null || organizer.Instances.Count == 0)
                return organizer.Status;

            string text = organizer.Status;

            if (showTimers)
            {
                EpisodeTimer epoch = organizer.CurrentEpoch.Timer;
                EpisodeTimer episode = organizer.Instances[0].Timer;
                text += $"\nEpoch {epoch.Remaining / 60f:0.0} min left  |  Episode {episode.Elapsed:0}/{episode.Limit:0} s";
            }

            if (showSpeed && organizer.State == TrainingState.Running)
            {
                text += $"\nSpeed {Format(organizer.Speed)}x";
                if (meter.IsBehind(organizer.Speed))
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
