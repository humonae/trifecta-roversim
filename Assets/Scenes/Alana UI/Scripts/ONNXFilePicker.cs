using UnityEngine;
using SFB; // StandaloneFileBrowser
using System.IO;

public class ONNXFilePicker : MonoBehaviour
{
    public TMPro.TMP_Text[] outputPathLabel;
    public TMPro.TMP_Text[] outputPathFileName;
    public TMPro.TMP_Text[] selectedFileLabel;

    public int mode; //0 = training, 1 = evaluation 
    
    public void OpenONNXFolderPicker() //picking folder destination
    {
        // allow user to select a save location for the onnx file
        var folders = StandaloneFileBrowser.OpenFolderPanel("Select a save location", "", false);

        // empty result = user cancelled
        if (folders.Length == 0 || string.IsNullOrEmpty(folders[0]))
            return;

        string onnxOutputFolder = folders[0];

        if (outputPathLabel[mode] != null)
            outputPathLabel[mode].text = onnxOutputFolder;

        var training = FindAnyObjectByType<TrainingSettings>();
        if (training != null)
            training.SetModelOutputPath(onnxOutputFolder);
    }

    // For the training panel's ".onnx Output File" Browse: a save dialog for the trained model's name and folder.
    public void OpenONNXSaveLocation()
    {
        string path = StandaloneFileBrowser.SaveFilePanel("Save the trained model as", "", "trained_model", "onnx");
        if (string.IsNullOrEmpty(path))
            return;

        if (outputPathFileName.Length > mode && outputPathFileName[mode] != null)
            outputPathFileName[mode].text = Path.GetFileName(path);
        if (outputPathLabel.Length > mode && outputPathLabel[mode] != null)
            outputPathLabel[mode].text = Path.GetDirectoryName(path);

        var training = FindAnyObjectByType<TrainingSettings>();
        if (training != null)
            training.SetModelOutputFile(path);
    }

    public void OpenONNXFilePicker() //selecting a preexisting onnx file for training/evaluation
    {
        // provide a list of acceptable extensions
        var extensions = new[] { new ExtensionFilter("Trained Model", "onnx", "sentis") };
        var paths = StandaloneFileBrowser.OpenFilePanel("Select an ONNX Model", "", extensions, false);

        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            string modelPath = paths[0];
            Debug.Log($"Selected ONNX file: {modelPath}");

            if (selectedFileLabel[mode] != null)
                selectedFileLabel[mode].text = modelPath;

            OnModelSelected(modelPath);
        }
        else
        {
            Debug.Log("File selection cancelled.");
        }
    }

    public void OpenPTPretrainedPicker() 
    {
        // 1. Change the filter to only accept "pt"
        var extensions = new[] { new ExtensionFilter("PyTorch Model", "pt") };
        
        // 2. Update the window title text to match
        var paths = StandaloneFileBrowser.OpenFilePanel("Select a PyTorch Model", "", extensions, false);

        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            string modelPath = paths[0];
            Debug.Log($"Selected PyTorch file: {modelPath}");

            if (selectedFileLabel[mode] != null)
                selectedFileLabel[mode].text = modelPath;

            OnModelSelected(modelPath);
        }
        else
        {
            Debug.Log("File selection cancelled.");
        }
    }


    void OnModelSelected(string path)
    {
        // The Evaluation panel's model. EvaluationOrganizer imports it when the run starts.
        var evaluation = FindAnyObjectByType<EvaluationSettings>();
        if (evaluation != null)
            evaluation.SetSelectedModel(path);
        else
            Debug.LogWarning("No EvaluationSettings found; the picked model won't be used.");
    }

    public string GetOutputPath()
    {
        return outputPathLabel[mode].text;
    }
    public string GetFileName()
    {
        return outputPathFileName[mode].text;
    }

    public string GetFullPath()
    {
        return outputPathLabel[mode].text + outputPathFileName[mode].text;
    }
    
}
