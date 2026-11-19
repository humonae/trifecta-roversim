using Evaluation;
using TMPro;
using Training;
using UnityEngine;
using UnityEngine.UI;

// Shows what the sim is doing (Ready / Loading / Running / Paused / Stopping) as a word and a colored dot.
// Put it on any object that is always active, and assign the label (and the dot, if you have one).
public class RoverStatusLabel : MonoBehaviour
{
    [SerializeField] private TrainingOrganizer training;
    [SerializeField] private EvaluationOrganizer evaluation;
    [SerializeField] private TMP_Text label;
    [Tooltip("Optional. The little dot beside the word.")]
    [SerializeField] private Image dot;
    [Tooltip("Also color the word itself, not just the dot.")]
    [SerializeField] private bool tintText = false;

    [Header("Words")]
    [SerializeField] private string readyWord = "Ready";
    [SerializeField] private string loadingWord = "Loading";
    [SerializeField] private string runningWord = "Running";
    [SerializeField] private string pausedWord = "Paused";
    [SerializeField] private string stoppingWord = "Stopping";

    [Header("Colors")]
    [SerializeField] private Color readyColor = new Color32(0xA0, 0xFF, 0xBC, 0xFF);
    [SerializeField] private Color loadingColor = new Color32(0x9A, 0x9A, 0x9A, 0xFF);
    [SerializeField] private Color runningColor = new Color32(0xFF, 0xE4, 0x8A, 0xFF);

    private void Awake()
    {
        if (training == null)
            training = FindAnyObjectByType<TrainingOrganizer>();
        if (evaluation == null)
            evaluation = FindAnyObjectByType<EvaluationOrganizer>();
    }

    private void OnEnable()
    {
        if (training != null) training.StateChanged += OnStateChanged;
        if (evaluation != null) evaluation.StateChanged += OnStateChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (training != null) training.StateChanged -= OnStateChanged;
        if (evaluation != null) evaluation.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged(TrainingState state) => Refresh();

    private void Refresh()
    {
        // Only one of the two organizers can be busy at a time.
        TrainingState state = TrainingState.Idle;
        if (training != null && training.State != TrainingState.Idle)
            state = training.State;
        else if (evaluation != null && evaluation.State != TrainingState.Idle)
            state = evaluation.State;

        string word;
        Color color;
        switch (state)
        {
            case TrainingState.Running:
                word = runningWord; color = runningColor; break;
            case TrainingState.Paused:
                word = pausedWord; color = runningColor; break;
            case TrainingState.SettingUpPython:
            case TrainingState.StartingTrainer:
            case TrainingState.Preparing:
                word = loadingWord; color = loadingColor; break;
            case TrainingState.Stopping:
                word = stoppingWord; color = loadingColor; break;
            default:
                word = readyWord; color = readyColor; break;
        }

        if (label != null)
        {
            label.text = word;
            if (tintText)
                label.color = color;
        }

        if (dot != null)
            dot.color = color;
    }
}