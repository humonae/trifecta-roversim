using System;
using System.Collections.Generic;
using System.IO;
using Unity.InferenceEngine.RuntimeImport.Onnx;
using UnityEngine;

// Lets a built app load .onnx (and .sentis) files from disk as ModelAssets, the same objects the
// editor makes when a model is dragged into the project, so ML-Agents can run them unchanged.
//
// How: Sentis's runtime assembly still declares [InternalsVisibleTo("Unity.InferenceEngine.ONNX")],
// left over from when ONNX import was runtime code. This assembly takes that name, carries a copy of
// the editor's ONNX converter (Converter/), and builds the ModelAsset the way the editor importer does
// (ModelImporterBase.OnImportAsset). Google.Protobuf_Packed.dll ships with Sentis and is already
// enabled for standalone players.
//
// After upgrading com.unity.ai.inference, re-copy Converter/ from the new package's Editor/ONNX
// (see README.md).
namespace Unity.InferenceEngine.RuntimeImport
{
    public static class RuntimeModelImporter
    {
        public static bool CanImport(string path)
        {
            string extension = Path.GetExtension(path ?? "").ToLowerInvariant();
            return extension == ".onnx" || extension == ".sentis";
        }

        // Converts an .onnx file, or loads a .sentis file, into a new ModelAsset named after the file.
        // Throws if the file can't be read or converted. Warnings from the converter are returned.
        public static ModelAsset Import(string path, out List<string> warnings)
        {
            warnings = new List<string>();
            string fullPath = Path.GetFullPath(path);
            string extension = Path.GetExtension(fullPath).ToLowerInvariant();

            Model model;
            if (extension == ".onnx")
                model = ConvertOnnx(fullPath, warnings);
            else if (extension == ".sentis")
                model = ModelLoader.Load(fullPath);
            else
                throw new NotSupportedException($"'{Path.GetFileName(path)}' is not an .onnx or .sentis file.");

            ModelAsset asset = ToModelAsset(model);
            asset.name = Path.GetFileNameWithoutExtension(fullPath);
            return asset;
        }

        public static Model ConvertOnnx(string path, List<string> warnings)
        {
            var converter = new ONNXModelConverter(path);
            Model model = converter.Convert();

            var errors = new List<string>();
            foreach (ModelConverterBase.ImporterWarning warning in converter.ImportWarnings)
            {
                if (warning.messageSeverity == ModelConverterBase.WarningType.Error)
                    errors.Add(warning.message);
                else if (warning.messageSeverity == ModelConverterBase.WarningType.Warning && warnings != null)
                    warnings.Add(warning.message);
            }

            if (errors.Count > 0 || model == null)
                throw new InvalidDataException(errors.Count > 0 ? string.Join("\n", errors) : "The ONNX converter returned no model.");

            return model;
        }

        // Same steps as the editor's ModelImporterBase.OnImportAsset, minus the AssetDatabase calls.
        public static ModelAsset ToModelAsset(Model model)
        {
            ModelWriter.SaveModel(model, out byte[] description, out byte[][] weights);

            var asset = ScriptableObject.CreateInstance<ModelAsset>();

            var data = ScriptableObject.CreateInstance<ModelAssetData>();
            data.value = description;
            data.name = "Data";
            data.hideFlags = HideFlags.HideInHierarchy;
            asset.modelAssetData = data;

            asset.modelWeightsChunks = new ModelAssetWeightsData[weights.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                var chunk = ScriptableObject.CreateInstance<ModelAssetWeightsData>();
                chunk.value = weights[i];
                chunk.name = "Data";
                chunk.hideFlags = HideFlags.HideInHierarchy;
                asset.modelWeightsChunks[i] = chunk;
            }

            return asset;
        }

        // Frees a ModelAsset made by Import or ToModelAsset. Don't call it on assets from the project.
        public static void Release(ModelAsset asset)
        {
            if (asset == null)
                return;

            if (asset.modelAssetData != null)
                UnityEngine.Object.Destroy(asset.modelAssetData);

            if (asset.modelWeightsChunks != null)
            {
                foreach (ModelAssetWeightsData chunk in asset.modelWeightsChunks)
                {
                    if (chunk != null)
                        UnityEngine.Object.Destroy(chunk);
                }
            }

            UnityEngine.Object.Destroy(asset);
        }
    }
}
