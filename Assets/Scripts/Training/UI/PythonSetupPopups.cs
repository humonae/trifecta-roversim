using System;
using System.Collections.Generic;
using UnityEngine;

namespace Training
{
    // The popups TrainingOrganizer shows when Python isn't ready to train.
    public static class PythonSetupPopups
    {
        // Python 3.10 isn't installed: say so and link to the installer.
        public static void ShowPythonMissing(PopupDialog popup, PythonEnvironment environment, PythonSetupSettings settings)
        {
            string found = environment.OtherPythonVersions.Count > 0
                ? $" Python {string.Join(", ", environment.OtherPythonVersions)} is installed, but ML-Agents only works with 3.10."
                : "";
            string path = PythonPaths.IsWindows ? " and tick \"Add python.exe to PATH\" in the installer" : "";

            popup.Show(
                "Python 3.10 is required",
                $"Training needs Python 3.10, which wasn't found on this computer.{found}\n\n" +
                $"Install Python 3.10.11{path}, then press Run Training again.\n\n{settings.PythonDownloadUrl}",
                new PopupButton("Open download page", () => Application.OpenURL(settings.PythonDownloadUrl), true, true),
                new PopupButton("Close"));
        }

        // PyTorch / ML-Agents are missing (or the venv is broken): ask before downloading anything.
        public static void AskToInstall(PopupDialog popup, PythonEnvironment environment, Action<bool> answer)
        {
            string intro = environment.RepairsVenv
                ? "The Python environment in the RoverSim folder is incomplete, so the trainer (PyTorch and ML-Agents) can't run.\n\n" +
                  "Rebuild it and reinstall them?"
                : "The Python trainer (PyTorch and ML-Agents) isn't installed yet.\n\n" +
                  "Install it into a Python environment in the RoverSim folder?";

            popup.Show(
                environment.RepairsVenv ? "Repair the Python trainer?" : "Install the Python trainer?",
                $"{intro}\n{environment.VenvFolder}\n\n" +
                "This downloads a few GB (mostly PyTorch) and can take several minutes. It only happens once.",
                new PopupButton("Yes", () => answer(true), true),
                new PopupButton("No", () => answer(false)));
        }

        public static void ShowInstalling(PopupDialog popup, PythonEnvironment environment, Action cancel)
        {
            popup.ShowProgress(
                environment.RepairsVenv ? "Repairing the Python trainer" : "Installing the Python trainer",
                () => environment.Status,
                () => environment.Progress,
                new PopupButton("Cancel", cancel));
        }

        // Install failed, or Python is set up in a way the app won't change: show what went wrong.
        public static void ShowProblem(PopupDialog popup, PythonEnvironment environment, bool installFailed)
        {
            string title = installFailed ? "Install failed" : "Python trainer not ready";
            popup.Show(
                title,
                Shorten(environment.Error) + "\n\nThe full output is in the Console (the [Python setup] lines).",
                new PopupButton("Close"));
        }

        // Error text can end in pages of pip output. Keep the first line and the last few lines.
        public static string Shorten(string error, int tailLines = 6, int maxChars = 700)
        {
            if (string.IsNullOrEmpty(error))
                return "Something went wrong while setting up Python.";

            string[] lines = error.Replace("\r", "").Split('\n');
            var kept = new List<string>();
            if (lines.Length <= tailLines + 1)
            {
                kept.AddRange(lines);
            }
            else
            {
                kept.Add(lines[0]);
                kept.Add("...");
                for (int i = lines.Length - tailLines; i < lines.Length; i++)
                    kept.Add(lines[i]);
            }

            string text = string.Join("\n", kept).Trim();
            if (text.Length > maxChars)
                text = text.Substring(0, maxChars).TrimEnd() + "...";
            return text;
        }
    }
}
