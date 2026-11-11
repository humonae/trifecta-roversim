using TMPro;
//using Evaluation;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

// Holds the training panel's settings.
// TrainingRequestBuilder and TrainingOrganizer read these fields by name,
// so keep the field names in sync with them.

public class EvaluationSettings : MonoBehaviour
{
    [Header("Default Training Values")]

    //public EvaluationDefaults defaults;
    

   
    public TMP_Text ModelText;
    public Slider ObstacleDensitySlider; 
    public TMP_InputField NumberEpisodes;
    public TMP_Text StartingSeedText;

    // not sure how to handle runspeed, so it's just there
    //note, the selected map is not kept as a setting because it's handled by load map
    // though it might be a good idea to put it in here... I'm not sure because my brain is EMPTY!


    public void Start()
    {
        Refresh();
    }
    private string[] architectureNames = {"Small","Medium","Large"};
    public string SelectedModel;
    public int EpisodeCount = 5000;
    public bool RandomPosition = true;
    public float ObstacleDensity = 5;
    public bool RocksOn; //set to 0 if obstacle density is 0
    public Vector3 StartPos;

    public Vector3 GoalPos;

    public int StartingSeed;

    public string ModelOutputPath;
    
    public string ModelName;
 
    // For a dropdown, which sends the selected option's index.
 
    // For an input field, which sends text.
    public void SetEpisodes(string text)
    {
        if (int.TryParse(text, out int n))
            EpisodeCount = n;
    }
 
    public void SetSeed(float a)
    {
        //random
    }
 
    public void SetObstacleDensity(float value)
    {
        ObstacleDensity = value;
        SetRocks();
    } 
 
    private void SetRocks()
    {
        RocksOn = ObstacleDensity == 0.0f? false : true;
    }
 
    // Vector3 can't be passed from an Inspector UI event, so call these from code
    // (for example when the user clicks the terrain).
    public void SetStartPosition(Vector3 position) => StartPos = position;
 
    public void SetGoalPosition(Vector3 position) => GoalPos = position;
 
    public void SetModelOutputPath(string path) => ModelOutputPath = path;
 
    // public void SetModelName(string name) => ModelName = name;

    public void Refresh()
    {
    }


}