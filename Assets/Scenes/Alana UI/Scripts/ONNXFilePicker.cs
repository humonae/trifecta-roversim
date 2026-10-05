using UnityEngine;
using SFB; // StandaloneFileBrowser

public class ONNXFilePicker : MonoBehaviour
{
    // Optional: drag a UI Text/TMP_Text here to display the chosen path
    public TMPro.TMP_Text selectedPathLabel;

    // Call this from your button's OnClick()
    public void OpenONNXFilePicker()
    {
        var extensions = new[] { new ExtensionFilter("ONNX Model", "onnx") };
        var paths = StandaloneFileBrowser.OpenFilePanel("Select ONNX Model", "", extensions, false);

        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            string modelPath = paths[0];
            Debug.Log($"Selected ONNX file: {modelPath}");

            if (selectedPathLabel != null)
                selectedPathLabel.text = modelPath;

            OnModelSelected(modelPath);
        }
        else
        {
            Debug.Log("File selection cancelled.");
        }
    }

    // Hook point for whatever you want to do once a file is picked
    void OnModelSelected(string path)
    {
        // e.g. load into Barracuda, store the path for later, etc.
    }
}
