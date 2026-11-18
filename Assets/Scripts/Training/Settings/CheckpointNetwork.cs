using System;

namespace Training
{
    // Filled in when a run resumes from a .pt checkpoint. The checkpoint's network replaces the
    // Network Architecture preset, because ML-Agents silently starts from random weights when
    // the shapes don't match.
    [Serializable]
    public class CheckpointNetwork
    {
        public bool IsSet;
        [UnityEngine.Tooltip("The copy of the checkpoint the trainer loads (ML-Agents init_path).")]
        public string InitPath = "";
        public long Step;
        public int HiddenUnits;
        public int Layers;
        public string VisualEncoder = "";
        public bool Normalize;

        public NetworkPreset ToPreset(NetworkPreset fallback)
        {
            var preset = new NetworkPreset("Checkpoint", HiddenUnits, Layers);
            preset.VisualEncoder = string.IsNullOrEmpty(VisualEncoder)
                ? (fallback != null ? fallback.VisualEncoder : "simple")
                : VisualEncoder;
            return preset;
        }
    }
}
