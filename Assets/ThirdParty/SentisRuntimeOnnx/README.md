# Sentis runtime ONNX import

This lets the built app load `.onnx` and `.sentis` files from disk as `ModelAsset`s. Those are the same objects the editor makes when you drag a model into the project, so ML-Agents runs them unchanged.

```csharp
ModelAsset model = Unity.InferenceEngine.RuntimeImport.RuntimeModelImporter.Import(path, out var warnings);
behaviorParameters.Model = model;   // set before the agent initializes
...
RuntimeModelImporter.Release(model);  // when done
```

## Why this works

- **Internal access.** Since Sentis 1.5, the ONNX converter only ships in the editor assembly. But the runtime assembly still has `[assembly: InternalsVisibleTo("Unity.InferenceEngine.ONNX")]` (in `Runtime/Core/Serialization/ModelWriter.cs`). This folder's asmdef uses exactly that name, so it can use Sentis internals without modifying the package.
- **Converter source.** `Converter/` is a copy of the converter files from `Editor/ONNX/` in the package. The only change is the namespace, which is now `Unity.InferenceEngine.RuntimeImport`. None of those files use `UnityEditor`.
- **Building the ModelAsset.** `RuntimeModelImporter.ToModelAsset` follows the same steps as the editor's `ModelImporterBase.OnImportAsset`: `ModelWriter.SaveModel`, then `ModelAssetData` plus the `ModelAssetWeightsData` chunks.
- **Protobuf DLL.** `Google.Protobuf_Packed.dll` already ships in the package, and its import settings already include the Windows, Linux and macOS standalone players.
- **IL2CPP.** `link.xml` keeps IL2CPP from stripping the protobuf classes.

## Rules

- **Don't rename the asmdef.** The name `Unity.InferenceEngine.ONNX` is what grants the internal access.
- **After upgrading `com.unity.ai.inference`,** run **Tools > RoverSim > Update Runtime ONNX Converter From Sentis**, then check the Console for compile errors.
  - If a future Sentis version removes the `InternalsVisibleTo`, this stops compiling. The error appears straight away; it doesn't fail silently.
- **License.** The copied converter code is under the Unity Companion License (`LICENSE.md`), which allows this use inside a Unity project.
