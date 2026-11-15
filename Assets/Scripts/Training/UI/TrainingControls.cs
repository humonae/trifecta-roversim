using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Training
{
    // Play / pause / stop for whichever run is active: training, or evaluation when the scene has an
    // EvaluationOrganizer (see SimulationRuns).
    public class TrainingControls : MonoBehaviour
    {
        [SerializeField] private TrainingOrganizer organizer;
        [SerializeField] private Button playButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button stopButton;
        [Tooltip("Buttons that start a run, such as Run Training and Run Evaluation. They are disabled while a run is active.")]
        [SerializeField] private Button[] startButtons = new Button[0];

        private ISimulationRun run;

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
                Debug.LogWarning($"[TrainingControls] No {nameof(TrainingOrganizer)} found.", this);
                return;
            }

            AddListener(playButton, run.Play);
            AddListener(pauseButton, run.Pause);
            AddListener(stopButton, run.Stop);
            run.StateChanged += Refresh;
            Refresh(run.State);
        }

        private void OnDisable()
        {
            if (run == null)
                return;

            RemoveListener(playButton, run.Play);
            RemoveListener(pauseButton, run.Pause);
            RemoveListener(stopButton, run.Stop);
            run.StateChanged -= Refresh;
        }

        private void Refresh(TrainingState ignored)
        {
            TrainingState state = run.State;
            bool idle = state == TrainingState.Idle;

            SetInteractable(playButton, state == TrainingState.Paused || (idle && run.CanStartFromPlay));
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
