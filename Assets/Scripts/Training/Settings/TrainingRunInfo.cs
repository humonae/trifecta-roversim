using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Training
{
    // Saved next to each exported model as <model>.training.json, so evaluation can test on
    // tiles the model never trained on ("Held-out Tiles").
    [Serializable]
    public class TrainingRunInfo
    {
        public string Model = "";
        public string RunId = "";
        public string Map = "";
        public List<string> Tiles = new List<string>();
        public string FinishedAt = "";
        public int Seed;

        public static string PathFor(string modelPath)
        {
            return Path.ChangeExtension(modelPath, ".training.json");
        }

        public bool TrySave(string modelPath)
        {
            try
            {
                File.WriteAllText(PathFor(modelPath), JsonUtility.ToJson(this, true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Training] Could not save the training record next to '{modelPath}': {e.Message}");
                return false;
            }
        }

        public static bool TryLoad(string modelPath, out TrainingRunInfo info)
        {
            info = null;
            if (string.IsNullOrEmpty(modelPath))
                return false;

            string path = PathFor(modelPath);
            if (!File.Exists(path))
                return false;

            try
            {
                info = JsonUtility.FromJson<TrainingRunInfo>(File.ReadAllText(path));
                return info != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Evaluation] Could not read '{path}': {e.Message}");
                return false;
            }
        }
    }
}
