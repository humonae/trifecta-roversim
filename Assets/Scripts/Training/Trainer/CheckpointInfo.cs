using System;

namespace Training
{
    // What CheckpointInspector's Python script reports about a .pt checkpoint.
    // Field names match the script's JSON keys, so JsonUtility can fill them.
    [Serializable]
    public class CheckpointInfo
    {
        public bool ok;
        public string error = "";
        public long step;
        public int hidden_units;
        public int num_layers;
        public string vis_encode_type = "";
        public bool normalize;
        public int memory_size;
        public int continuous_actions;
        public int[] discrete_branches = new int[0];
        public int visual_inputs;
        public int vector_size;
        public bool has_critic;
        public string torch_version = "";

        public string Describe()
        {
            string encoder = string.IsNullOrEmpty(vis_encode_type) ? "no visual encoder" : $"{vis_encode_type} visual encoder";
            return $"{hidden_units} units x {num_layers} layers, {encoder}, normalize {(normalize ? "on" : "off")}; " +
                   $"{visual_inputs} camera input(s), {vector_size} vector value(s), " +
                   $"{continuous_actions} continuous action(s){DescribeBranches()}";
        }

        private string DescribeBranches()
        {
            return discrete_branches == null || discrete_branches.Length == 0
                ? ""
                : $", discrete branches [{string.Join(", ", discrete_branches)}]";
        }
    }
}
