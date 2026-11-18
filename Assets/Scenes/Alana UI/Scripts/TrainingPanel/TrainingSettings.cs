using TMPro;
using Training;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

// Holds the training panel's settings.
// Training.TrainingRequestBuilder turns these into the TrainingRequest the TrainingOrganizer runs,
// so keep the field names in sync with it. `defaults` supplies everything the panel doesn't show
// (iteration unit, obstacle slider scale, default results folder, seed).

public class TrainingSettings : MonoBehaviour
{
    [Header("Default Training Values")]
    // public TrainingDefaults defaults;
    /*
        return new TrainingRequest
    {
        NewTileEachEpoch = defaults.NewTileEachEpoch,
        RocksEnabled = ui.rocks,
        RockDensity = Mathf.Clamp01(ui.obstacleDensity / defaults.ObstacleSliderMax) * defaults.MaxRockDensity,
        EpochSeconds = defaults.EpochMinutes * 60f,
        EpisodeSeconds = defaults.EpisodeSeconds,
        IterationUnit = defaults.IterationUnit,
        MaxIterations = iterations,
        NetworkPreset = trainerConfig.PresetAt(ui.networkArchitecture).Name,
        EarlyStopping = ui.earlyStopping,
        ResultsDirectory = resultsDirectory,
        ModelOutputPath = Path.Combine(modelFolder, modelName + ".onnx"),
        RunName = modelName,
        RunId = PythonPaths.MakeRunId(modelName),
        UseFixedSeed = defaults.UseFixedSeed,
        Seed = defaults.Seed
    };
    */

    public TrainingDefaults defaults;

    public TMP_Dropdown NADropdown;
    public TMP_InputField IterInput;
    public Slider EarlyStopSlider;
    public Slider SuccessSlider;
    public TMP_Text SuccessText;
    public Slider FixedSeedToggle;
    public Slider EpochToggle;
    public Slider ObstacleDensitySlider;
    public TMP_Text ObstacleDensityText;
    public TMP_Text Folder;
    public TMP_Text OutputFileText;
    // The training panel's Model label (TrainingOptions/Content/ModelSelect/.../Browse/GameObject/placeholder).
    public TMP_Text PretrainedModelText;


    public void Start()
    {
        Refresh();
    }
    private string[] architectureNames = {"Small","Medium","Large"};
    public string NetworkArchitecture = "Small";
    public int Iterations = 5000;

    public bool EarlyStopping = true;
    public float SuccessTarget = 0.9f;
    
    public bool FixedSeed = true;

    public bool NewTileEachEpoch = true;
    public float EpochMinutes = 10f;
    public float EpisodeSeconds = 60f;
    public float ObstacleDensity = 5;

    public bool RocksOn; //set to 0 if obstacle density is 0

    public bool RandomPosition;

    public Vector3 StartPos;

    public Vector3 GoalPos;

    public string ModelOutputPath;
    
    public string ModelName;

    // The .pt picked with Model > Browse (ONNXFilePicker.OpenPTPretrainedPicker). Nothing is loaded until Load Model.
    public string SelectedCheckpointPath = "";
    // The loaded .pt that the next training run continues from. Empty trains a new network.
    // Set only by Load Model (ONNXFilePicker.LoadPTPretrained), and only for a valid ML-Agents checkpoint.
    public string ResumeCheckpointPath = "";
    // Fired with the reason when Load Model rejects the selected file (the selection is then cleared).
    public event System.Action<string> CheckpointRejected;
    private string checkpointRejection;

    public void SetNetworkArchitecture(string name) => NetworkArchitecture = name;
 
    // For a dropdown, which sends the selected option's index.
    public void SetNetworkArchitectureIndex(int index)
    {
        if (architectureNames != null && index >= 0 && index < architectureNames.Length)
            NetworkArchitecture = architectureNames[index];
    }
 
    // For an input field, which sends text.
    public void SetIterations(string text)
    {
        if (int.TryParse(text, out int n))
            Iterations = n;
    }

    public void SetEarlyStopping(float a)    
    {   
        EarlyStopping = a != 0.0f;  
    }
 
    // For a 0 to 1 slider.
    public void SetSuccessTarget(float value){
        SuccessTarget = value;
        SuccessText.text = (SuccessTarget * 100f).ToString("0") + "%";
    }
 
    // For a 0 to 100 slider.
    public void SetSuccessTargetPercent(float percent) => SuccessTarget = percent / 100f;// a decimal?
 
    public void SetFixedSeed(float a)
    {
        FixedSeed = a != 0.0f;  
    }
 
    public void SetNewTileEachEpoch(float a)
    {
        NewTileEachEpoch = a != 0.0f;  
    }
 
    public void SetObstacleDensity(float value)
    {       
        ObstacleDensity = value;
        ObstacleDensityText.text = (ObstacleDensity * 10f).ToString("0") + "%";
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
 
    // The folder the checkpoints and the .onnx are saved to.
    public void SetModelOutputPath(string path) => ModelOutputPath = path;
 
    // The .onnx file name, with or without the extension.
    public void SetModelName(string name) => ModelName = name;

    // For a save-file dialog that returns the full path of the .onnx.
    public void SetModelOutputFile(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return;

        ModelOutputPath = System.IO.Path.GetDirectoryName(fullPath);
        ModelName = System.IO.Path.GetFileNameWithoutExtension(fullPath);
    }

    // Model > Browse: remember the file, and unload any loaded checkpoint until Load Model is pressed.
    public void SelectCheckpoint(string path)
    {
        SelectedCheckpointPath = string.IsNullOrWhiteSpace(path) ? "" : path.Trim();
        ResumeCheckpointPath = "";
        checkpointRejection = null;
        ShowPretrainedModel();
    }

    // Load Model: load the selected file if it's a valid ML-Agents .pt checkpoint, otherwise clear it.
    public bool LoadSelectedCheckpoint()
    {
        string reason, detail;
        if (Training.CheckpointFile.Check(SelectedCheckpointPath, out reason, out detail))
        {
            ResumeCheckpointPath = SelectedCheckpointPath;
            checkpointRejection = null;
            Debug.Log($"[Training] Loaded checkpoint '{ResumeCheckpointPath}'. The next training run continues from it.");
            ShowPretrainedModel();
            return true;
        }

        SelectedCheckpointPath = "";
        ResumeCheckpointPath = "";
        checkpointRejection = reason;
        Debug.LogWarning($"[Training] Model not loaded: {detail} Training will start from scratch.");
        ShowPretrainedModel();
        CheckpointRejected?.Invoke(detail);
        return false;
    }

    // Select and load in one step, for code.
    public bool SetResumeCheckpoint(string path)
    {
        SelectCheckpoint(path);
        return LoadSelectedCheckpoint();
    }

    // Back to training a new network.
    public void ClearResumeCheckpoint()
    {
        SelectedCheckpointPath = "";
        ResumeCheckpointPath = "";
        checkpointRejection = null;
        ShowPretrainedModel();
    }

    private void ShowPretrainedModel()
    {
        if (PretrainedModelText == null)
            return;

        if (!string.IsNullOrEmpty(ResumeCheckpointPath))
            PretrainedModelText.text = DescribeCheckpoint(ResumeCheckpointPath);
        else if (!string.IsNullOrEmpty(SelectedCheckpointPath))
            PretrainedModelText.text = DescribeCheckpoint(SelectedCheckpointPath) + " (not loaded)";
        else
            PretrainedModelText.text = string.IsNullOrEmpty(checkpointRejection) ? "None" : $"None ({checkpointRejection})";
    }

    // "<run> / <file>": every run has a checkpoint.pt, so the run folder's name says which one it is.
    private static string DescribeCheckpoint(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "None";

        string behaviorFolder = System.IO.Path.GetDirectoryName(path);
        string runFolder = string.IsNullOrEmpty(behaviorFolder) ? null : System.IO.Path.GetDirectoryName(behaviorFolder);
        string run = string.IsNullOrEmpty(runFolder) ? null : System.IO.Path.GetFileName(runFolder);
        string file = System.IO.Path.GetFileName(path);
        return string.IsNullOrEmpty(run) ? file : $"{run} / {file}";
    }

    public void Refresh()
    {   
        //ideally replace with the default values set in trainingDefaults rather than reassigning them to their current values
        int i = NADropdown.options.FindIndex(o => o.text == NetworkArchitecture);
        if (i >= 0) NADropdown.SetValueWithoutNotify(i);

        IterInput.SetTextWithoutNotify(Iterations.ToString());
        EarlyStopSlider.SetValueWithoutNotify(EarlyStopping ? 1f : 0f);
        SuccessSlider.SetValueWithoutNotify(SuccessTarget);
        SuccessText.text = (SuccessTarget * 100f).ToString("0") + "%";
        FixedSeedToggle.SetValueWithoutNotify(FixedSeed?1:0);
        EpochToggle.SetValueWithoutNotify(NewTileEachEpoch?1:0);
        // FixedSeedToggle.SetIsOnWithoutNotify(FixedSeed);
        // EpochToggle.SetIsOnWithoutNotify(NewTileEachEpoch);
        ObstacleDensitySlider.SetValueWithoutNotify(ObstacleDensity);
        ObstacleDensityText.text = (ObstacleDensity * 10f).ToString("0") + "%";
        Folder.text = ModelOutputPath;
        OutputFileText.text = ModelName;
        ShowPretrainedModel();
    }


}