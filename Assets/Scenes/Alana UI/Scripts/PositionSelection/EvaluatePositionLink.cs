using UnityEngine;

// Connects the terrain click-picker to the Evaluation panel's Start/Goal settings.
public class EvaluationPositionLink : MonoBehaviour
{
    [SerializeField] private RoverPositionSelector selector;
    [SerializeField] private EvaluationSettings settings;

    private void Awake()
    {
        // Clicking only does anything while "Set on Preview" is chosen.
        if (selector != null) selector.enabled = !settings.RandomPosition;
    }

    private void OnEnable()
    {
        selector.SelectionComplete += OnComplete;
        selector.SelectionCleared += OnCleared;
    }

    private void OnDisable()
    {
        selector.SelectionComplete -= OnComplete;
        selector.SelectionCleared -= OnCleared;
    }

    private void OnComplete(Vector3 start, Vector3 goal)
    {
        settings.SetStartPosition(start);
        settings.SetGoalPosition(goal);
    }

    // Zero for both means "less than 1 m apart", so the organizer shows its
    // "Pick both a start and a goal" message instead of using stale points.
    private void OnCleared()
    {
        settings.SetStartPosition(Vector3.zero);
        settings.SetGoalPosition(Vector3.zero);
    }

    // Wire the Random | Set on Preview buttons' OnClick (int) to this
    // INSTEAD of EvaluationSettings.SetStartGoalMode. It calls that for you.
    public void SetMode(int index)
    {
        settings.SetStartGoalMode(index);
        bool pick = index == 1;
        selector.enabled = pick;
        if (!pick) selector.ClearSelection(); // also clears the markers
    }
}