using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Training
{
    public static class TrainerConfigWriter
    {
        private const string ConfigFolder = "configs";

        public static bool TryWrite(TrainingRequest request, string behaviorName, TrainerConfig config, out string path, out string error)
        {
            try
            {
                string folder = Path.Combine(PythonPaths.Resolve(request.ResultsDirectory), ConfigFolder);
                Directory.CreateDirectory(folder);

                path = Path.Combine(folder, request.RunId + ".yaml");
                File.WriteAllText(path, Build(request, behaviorName, config), new UTF8Encoding(false));
                error = null;
                return true;
            }
            catch (Exception e)
            {
                path = null;
                error = $"Could not write the trainer config: {e.Message}";
                return false;
            }
        }

        public static string Build(TrainingRequest request, string behaviorName, TrainerConfig config)
        {
            NetworkPreset network = config.PresetNamed(request.NetworkPreset);
            var yaml = new StringBuilder();

            yaml.AppendLine("behaviors:");
            yaml.AppendLine($"  {behaviorName}:");
            yaml.AppendLine("    trainer_type: ppo");
            yaml.AppendLine("    hyperparameters:");
            Line(yaml, 6, "batch_size", config.BatchSize);
            Line(yaml, 6, "buffer_size", config.BufferSize);
            Line(yaml, 6, "learning_rate", config.LearningRate);
            Line(yaml, 6, "beta", config.Beta);
            Line(yaml, 6, "epsilon", config.Epsilon);
            Line(yaml, 6, "lambd", config.Lambda);
            Line(yaml, 6, "num_epoch", config.NumEpoch);
            Line(yaml, 6, "learning_rate_schedule", "linear");
            yaml.AppendLine("    network_settings:");
            Line(yaml, 6, "normalize", config.NormalizeObservations ? "true" : "false");
            Line(yaml, 6, "hidden_units", network.HiddenUnits);
            Line(yaml, 6, "num_layers", network.Layers);
            Line(yaml, 6, "vis_encode_type", string.IsNullOrWhiteSpace(network.VisualEncoder) ? "simple" : network.VisualEncoder.Trim());
            yaml.AppendLine("    reward_signals:");
            yaml.AppendLine("      extrinsic:");
            Line(yaml, 8, "gamma", config.Gamma);
            Line(yaml, 8, "strength", 1f);
            Line(yaml, 4, "keep_checkpoints", config.KeepCheckpoints);
            Line(yaml, 4, "checkpoint_interval", config.CheckpointInterval);
            Line(yaml, 4, "max_steps", request.MaxTrainerSteps);
            Line(yaml, 4, "time_horizon", config.TimeHorizon);
            Line(yaml, 4, "summary_freq", config.SummaryFrequency);

            return yaml.ToString();
        }

        private static void Line(StringBuilder yaml, int indent, string key, object value)
        {
            string text = value is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : value.ToString();

            yaml.Append(' ', indent).Append(key).Append(": ").AppendLine(text);
        }
    }
}
