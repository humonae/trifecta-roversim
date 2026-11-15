using System;
using System.Collections.Generic;
using System.IO;
using Unity.InferenceEngine;
using Unity.InferenceEngine.RuntimeImport;
using UnityEngine;

namespace Evaluation
{
    // Turns the .onnx (or .sentis) file picked in the UI into a ModelAsset that ML-Agents can run.
    // Works the same in the editor and in built apps, through RuntimeModelImporter
    // (Assets/ThirdParty/SentisRuntimeOnnx).
    public static class EvaluationModelLoader
    {
        // imported is true when the asset was made here and should be freed with Release after the run.
        public static bool TryLoad(string path, ModelAsset fallback, out ModelAsset model, out bool imported, out string error)
        {
            model = null;
            imported = false;

            if (string.IsNullOrEmpty(path))
            {
                if (fallback != null)
                {
                    model = fallback;
                    error = null;
                    return true;
                }

                error = "No model chosen. Use Browse under Model Selection to pick a trained .onnx file.";
                return false;
            }

            if (!File.Exists(path))
            {
                error = $"The model file '{path}' does not exist.";
                return false;
            }

            if (!RuntimeModelImporter.CanImport(path))
            {
                error = $"'{Path.GetFileName(path)}' is not an .onnx or .sentis file.";
                return false;
            }

            try
            {
                List<string> warnings;
                model = RuntimeModelImporter.Import(path, out warnings);
                foreach (string warning in warnings)
                    Debug.LogWarning($"[Evaluation] Importing {Path.GetFileName(path)}: {warning}");
            }
            catch (Exception e)
            {
                error = $"Could not import '{Path.GetFileName(path)}': {e.Message}";
                return false;
            }

            imported = true;
            Debug.Log($"[Evaluation] Imported model from {path}");
            error = null;
            return true;
        }

        public static void Release(ModelAsset model)
        {
            RuntimeModelImporter.Release(model);
        }

        // Catches files that aren't ML-Agents policies before any rover is spawned.
        public static bool Validate(ModelAsset asset, out string error)
        {
            try
            {
                Model runtimeModel = ModelLoader.Load(asset);
                foreach (Model.Output output in runtimeModel.outputs)
                {
                    if (output.name != null && output.name.Contains("continuous_actions"))
                    {
                        error = null;
                        return true;
                    }
                }

                error = $"'{asset.name}' has no continuous action output, so it isn't an ML-Agents policy for this rover.";
                return false;
            }
            catch (Exception e)
            {
                error = $"'{asset.name}' could not be loaded: {e.Message}";
                return false;
            }
        }
    }
}
