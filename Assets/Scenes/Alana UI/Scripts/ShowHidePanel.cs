using UnityEngine;

public class ShowHidePanel : MonoBehaviour
{
    //toggle between training and evaluation panels
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

    public GameObject panel;
    public void ShowPanel()
    {
        panel.SetActive(true);
    }
    public void HidePanel()
    {
        panel.SetActive(false);
    }




}
