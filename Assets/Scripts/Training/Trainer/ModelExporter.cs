using System;
using System.IO;
using UnityEngine;

namespace Training
{
    public static class ModelExporter
    {
        public static string FindModel(string runDirectory, string behaviorName)
        {
            if (string.IsNullOrEmpty(runDirectory) || string.IsNullOrEmpty(behaviorName))
                return null;

            string model = Path.Combine(runDirectory, behaviorName + ".onnx");
            return File.Exists(model) ? model : null;
        }

        public static string CopyTo(string model, string destination)
        {
            if (string.IsNullOrWhiteSpace(destination))
                return null;

            try
            {
                string target = PythonPaths.Resolve(destination);
                if (!target.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase))
                    target += ".onnx";

                if (string.Equals(Path.GetFullPath(model), target, StringComparison.OrdinalIgnoreCase))
                    return target;

                if (File.Exists(target))
                    target = Path.Combine(Path.GetDirectoryName(target), $"{Path.GetFileNameWithoutExtension(target)}_{DateTime.Now:yyyyMMdd_HHmmss}.onnx");

                string folder = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                File.Copy(model, target);
                return target;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Training] Could not copy the model to '{destination}': {e.Message}");
                return null;
            }
        }
    }
}
