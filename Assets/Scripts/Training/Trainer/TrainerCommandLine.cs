using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Training
{
    public static class TrainerCommandLine
    {
        public static string Build(TrainerLaunchSettings launch, TrainingRequest request, string configPath, string resultsDirectory, string runId)
        {
            var args = new List<string>
            {
                "-u",
                "-m", "mlagents.trainers.learn",
                configPath,
                "--run-id", runId,
                "--results-dir", resultsDirectory,
                "--timeout-wait", launch.ConnectionTimeout.ToString(CultureInfo.InvariantCulture),
                "--max-lifetime-restarts", "0",
                "--time-scale", launch.TimeScale.ToString(CultureInfo.InvariantCulture),
                "--width", Screen.width.ToString(CultureInfo.InvariantCulture),
                "--height", Screen.height.ToString(CultureInfo.InvariantCulture),
                "--quality-level", QualitySettings.GetQualityLevel().ToString(CultureInfo.InvariantCulture),
                "--target-frame-rate", Application.targetFrameRate.ToString(CultureInfo.InvariantCulture),
                "--capture-frame-rate", "0"
            };

            if (request.UseFixedSeed)
            {
                args.Add("--seed");
                args.Add(request.Seed.ToString(CultureInfo.InvariantCulture));
            }

            string joined = Join(args);
            if (!string.IsNullOrWhiteSpace(launch.ExtraArguments))
                joined += " " + launch.ExtraArguments.Trim();

            return joined;
        }

        public static string Join(IEnumerable<string> args)
        {
            var builder = new StringBuilder();
            foreach (string arg in args)
            {
                if (builder.Length > 0)
                    builder.Append(' ');
                AppendQuoted(builder, arg);
            }

            return builder.ToString();
        }

        private static void AppendQuoted(StringBuilder builder, string arg)
        {
            if (arg.Length > 0 && arg.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
            {
                builder.Append(arg);
                return;
            }

            builder.Append('"');
            int backslashes = 0;
            foreach (char c in arg)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (c == '"')
                    builder.Append('\\', backslashes * 2 + 1);
                else
                    builder.Append('\\', backslashes);

                backslashes = 0;
                builder.Append(c);
            }

            builder.Append('\\', backslashes * 2);
            builder.Append('"');
        }
    }
}
