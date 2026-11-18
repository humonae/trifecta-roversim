using TMPro;
using Evaluation;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

// Holds the evaluation panel's settings.
// Evaluation.EvaluationRequestBuilder turns these into the EvaluationRequest the EvaluationOrganizer runs,
// so keep the field names in sync with it. `defaults` supplies everything the panel doesn't show
// (episodes per rock layout, rover count, inference options). Speed comes from the top-right speed control.

public class EvaluationSettings : MonoBehaviour
{  
    public EvaluationDefaults defaults;
    [Header("UI elements")]
    public TMP_Text ModelText;
    public Slider ObstacleDensitySlider; 
    public TMP_Text ObstacleDensityText;
    public TMP_InputField NumberEpisodes;
    public TMP_Text StartingSeedText;

    [Header("Variables")]
    //Default values for now 
    public string SelectedModel = "None";
    public int EpisodeCount = 5000;
    public bool RandomPosition = true;
    public float ObstacleDensity = 5;
    public bool RocksOn; //set to 0 if obstacle density is 0
    public Vector3 StartPos;
    public Vector3 GoalPos;
    public int StartingSeed;
    public string ModelName;
    public EvaluationTiles TestOn = EvaluationTiles.SelectedTile;
    private int RunID; //?

    // not sure how to handle runspeed, so it's just there
    //note, the selected map is not kept as a setting because it's handled by load map
    // though it might be a good idea to put it in here... I'm not sure because my brain is EMPTY!


    public void Start()
    {
        Refresh();
    }
    public void SetEpisodes(string text)
    {
        if (int.TryParse(text, out int n))
            EpisodeCount = n;
    } 
    // Path of the .onnx/.sentis to evaluate. ONNXFilePicker calls this when a model is picked.
    public void SetSelectedModel(string path)
    {
        SelectedModel = path;
        ModelName = string.IsNullOrEmpty(path) ? "" : System.IO.Path.GetFileNameWithoutExtension(path);
        if (ModelText != null)
            ModelText.text = string.IsNullOrEmpty(ModelName) ? "None" : ModelName;
    }

    // For the Test on buttons (ButtonGroupSelector): 0 = Selected Tile, 1 = Held-out Tiles, 2 = All tiles.
    public void SetTestOn(int index)
    {
        if (System.Enum.IsDefined(typeof(EvaluationTiles), index))
            TestOn = (EvaluationTiles)index;
    }

    // For the Start and Goal buttons: 0 = Random, 1 = Set on Preview.
    // With Set on Preview, call SetStartPosition / SetGoalPosition with the picked points.
    public void SetStartGoalMode(int index) => RandomPosition = index == 0;

    // For typing a seed instead of rerolling.
    public void SetSeed(string text)
    {
        if (int.TryParse(text, out int seed))
            StartingSeed = seed;
    }

    public void SetSeed()
    {
        //random
        StartingSeed = Random.Range(0, int.MaxValue);
        StartingSeedText.text = StartingSeed.ToString();
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
 
    // public void SetModelName(string name) => ModelName = name;

    public void Refresh()
    {
        //ideally replace with the default values set in evaluationDefaults rather than reassigning them to their current values
        SetRocks();
        ModelText.text = string.IsNullOrEmpty(ModelName) ? "None" : ModelName;
        ObstacleDensitySlider.SetValueWithoutNotify(ObstacleDensity);
        NumberEpisodes.SetTextWithoutNotify(EpisodeCount.ToString());
        SetSeed();
        // StartingSeedText.text = StartingSeed.ToString();
    }


}