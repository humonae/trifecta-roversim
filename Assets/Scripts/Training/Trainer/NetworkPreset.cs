using System;
using UnityEngine;

namespace Training
{
    [Serializable]
    public class NetworkPreset
    {
        public string Name;
        [Min(16)] public int HiddenUnits;
        [Min(1)] public int Layers;
        [Tooltip("simple, nature_cnn, resnet, match3 or fully_connected.")]
        public string VisualEncoder = "simple";

        public NetworkPreset()
        {
        }

        public NetworkPreset(string name, int hiddenUnits, int layers)
        {
            Name = name;
            HiddenUnits = hiddenUnits;
            Layers = layers;
        }
    }
}
