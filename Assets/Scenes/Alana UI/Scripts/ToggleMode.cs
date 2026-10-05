using UnityEngine;

public class ToggleMode : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public GameObject TrainingPanel;
    public GameObject EvaluatePanel;

    public void ToggleTraining()
    {
        TrainingPanel.SetActive(true);
        EvaluatePanel.SetActive(false);
    }

    public void ToggleEvaluate()
    {
        EvaluatePanel.SetActive(true);
        TrainingPanel.SetActive(false);
    }
}
