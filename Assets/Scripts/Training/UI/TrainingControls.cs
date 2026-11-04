using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Training
{
    public class TrainingControls : MonoBehaviour
    {
        [SerializeField] private TrainingOrganizer organizer;
        [SerializeField] private Button playButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button stopButton;
        [Tooltip("Buttons that start a run, such as Run Training. They are disabled while a run is active.")]
        [SerializeField] private Button[] startButtons = new Button[0];

        private void Awake()
        {
            if (organizer == null)
                organizer = FindAnyObjectByType<TrainingOrganizer>();
        }

        private void OnEnable()
        {
            if (organizer == null)
            {
                Debug.LogWarning($"[TrainingControls] No {nameof(TrainingOrganizer)} found.", this);
                return;
            }

            AddListener(playButton, organizer.Play);
            AddListener(pauseButton, organizer.Pause);
            AddListener(stopButton, organizer.Stop);
            organizer.StateChanged += Refresh;
            Refresh(organizer.State);
        }

        private void OnDisable()
        {
            if (organizer == null)
                return;

            RemoveListener(playButton, organizer.Play);
            RemoveListener(pauseButton, organizer.Pause);
            RemoveListener(stopButton, organizer.Stop);
            organizer.StateChanged -= Refresh;
        }

        private void Refresh(TrainingState state)
        {
            bool idle = state == TrainingState.Idle;

            SetInteractable(playButton, state == TrainingState.Paused || (idle && organizer.CanStartFromPlay));
            SetInteractable(pauseButton, state == TrainingState.Running);
            SetInteractable(stopButton, !idle && state != TrainingState.Stopping);

            foreach (Button button in startButtons)
                SetInteractable(button, idle);
        }

        private static void AddListener(Button button, UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        private static void RemoveListener(Button button, UnityAction action)
        {
            if (button != null)
                button.onClick.RemoveListener(action);
        }

        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null)
                button.interactable = interactable;
        }
    }
}
