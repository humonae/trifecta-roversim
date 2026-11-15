using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RoverSim.SentisRuntimeOnnx.Editor
{
    // Re-copies Sentis's ONNX converter into Assets/ThirdParty/SentisRuntimeOnnx/Converter after the
    // com.unity.ai.inference package is upgraded, applying the same namespace change as the original copy.
    public static class RuntimeOnnxConverterSync
    {
        private const string PackageName = "com.unity.ai.inference";
        private const string Target = "Assets/ThirdParty/SentisRuntimeOnnx/Converter";
        private static readonly string[] Files =
        {
            "ONNX/ONNXModelConverter.cs",
            "ONNX/ONNXConstantsLoader.cs",
            "ONNX/ONNXNodeWrapper.cs",
            "ONNX/ONNXModelUtility.cs",
            "ONNX/Onnx.cs",
            "ModelConverterBase.cs"
        };

        [MenuItem("Tools/RoverSim/Update Runtime ONNX Converter From Sentis")]
        public static void Sync()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForPackageName(PackageName);
            if (package == null)
            {
                EditorUtility.DisplayDialog("Runtime ONNX", $"{PackageName} is not installed.", "OK");
                return;
            }

            string editorFolder = Path.Combine(package.resolvedPath, "Editor");
            var missing = new StringBuilder();
            foreach (string file in Files)
            {
                if (!File.Exists(Path.Combine(editorFolder, file)))
                    missing.AppendLine(file);
            }

            if (missing.Length > 0)
            {
                EditorUtility.DisplayDialog("Runtime ONNX",
                    $"Sentis {package.version} has moved or renamed these files, so the converter can't be updated automatically:\n\n{missing}", "OK");
                return;
            }

            foreach (string file in Files)
            {
                string source = File.ReadAllText(Path.Combine(editorFolder, file));
                string header =
                    $"// Copied from {PackageName} {package.version} (Editor/{file}) under the Unity Companion License (see LICENSE.md).\n" +
                    "// Only change: namespace Unity.InferenceEngine.Editor -> Unity.InferenceEngine.RuntimeImport, so it compiles\n" +
                    "// into the runtime assembly Unity.InferenceEngine.ONNX and doesn't clash with the editor importer.\n";
                string converted = source.Replace("Unity.InferenceEngine.Editor", "Unity.InferenceEngine.RuntimeImport");
                File.WriteAllText(Path.Combine(Target, Path.GetFileName(file)), header + converted);
            }

            File.Copy(Path.Combine(package.resolvedPath, "LICENSE.md"), "Assets/ThirdParty/SentisRuntimeOnnx/LICENSE.md", true);
            AssetDatabase.Refresh();
            Debug.Log($"[Runtime ONNX] Converter updated from {PackageName} {package.version}.");
        }
    }
}
