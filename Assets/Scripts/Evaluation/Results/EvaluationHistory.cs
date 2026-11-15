using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Evaluation
{
    // Every finished evaluation is saved as run_<number>.json (+ .csv with one row per episode) in
    // <persistentDataPath>/EvaluationRuns, so the Runs list survives restarts and each run can be
    // compared with the one before it.
    public static class EvaluationHistory
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "EvaluationRuns");

        // All saved runs, oldest first.
        public static List<EvaluationReport> LoadAll()
        {
            var runs = new List<EvaluationReport>();
            if (!Directory.Exists(Folder))
                return runs;

            foreach (string file in Directory.GetFiles(Folder, "run_*.json"))
            {
                try
                {
                    EvaluationReport report = JsonUtility.FromJson<EvaluationReport>(File.ReadAllText(file));
                    if (report != null)
                        runs.Add(report);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Evaluation] Skipping unreadable run file '{file}': {e.Message}");
                }
            }

            return runs.OrderBy(r => r.RunNumber).ToList();
        }

        public static EvaluationReport Latest()
        {
            List<EvaluationReport> runs = LoadAll();
            return runs.Count > 0 ? runs[runs.Count - 1] : null;
        }

        public static int NextRunNumber()
        {
            if (!Directory.Exists(Folder))
                return 1;

            int highest = 0;
            foreach (string file in Directory.GetFiles(Folder, "run_*.json"))
            {
                int number;
                string digits = Path.GetFileNameWithoutExtension(file).Substring(4);
                if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                    highest = Math.Max(highest, number);
            }

            return highest + 1;
        }

        // Saves the run and returns the JSON path, or null. extraFolder (optional) also gets a copy.
        public static string Save(EvaluationReport report, string extraFolder)
        {
            string name = $"run_{report.RunNumber:0000}";
            string saved = null;

            foreach (string folder in new[] { Folder, extraFolder })
            {
                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                try
                {
                    Directory.CreateDirectory(folder);
                    string json = Path.Combine(folder, name + ".json");
                    File.WriteAllText(json, JsonUtility.ToJson(report, true));
                    File.WriteAllText(Path.Combine(folder, name + ".csv"), ToCsv(report));
                    if (saved == null)
                        saved = json;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Evaluation] Could not save run {report.RunNumber} to '{folder}': {e.Message}");
                }
            }

            return saved;
        }

        private static string ToCsv(EvaluationReport report)
        {
            var csv = new StringBuilder();
            csv.AppendLine("episode,layout,tile,rover,outcome,success,seconds,start_distance_m,final_distance_m,hit_rock,wheels_on_rock,spawn_x,spawn_z,goal_x,goal_z");
            foreach (AttemptResult a in report.AttemptLog)
            {
                csv.Append(a.Episode + 1).Append(',')
                   .Append(a.Layout + 1).Append(',')
                   .Append('"').Append((a.Tile ?? "").Replace("\"", "\"\"")).Append("\",")
                   .Append(a.Rover).Append(',')
                   .Append(a.Outcome).Append(',')
                   .Append(a.Success ? 1 : 0).Append(',')
                   .Append(F(a.Seconds)).Append(',')
                   .Append(F(a.StartDistance)).Append(',')
                   .Append(F(a.FinalDistance)).Append(',')
                   .Append(a.HitRock ? 1 : 0).Append(',')
                   .Append(a.WheelsOnRock ? 1 : 0).Append(',')
                   .Append(F(a.Spawn.x)).Append(',')
                   .Append(F(a.Spawn.z)).Append(',')
                   .Append(F(a.Goal.x)).Append(',')
                   .Append(F(a.Goal.z)).AppendLine();
            }

            return csv.ToString();
        }

        private static string F(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
