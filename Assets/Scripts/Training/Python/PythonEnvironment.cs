using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Training
{
    // What Check found missing, so the UI can ask the right question.
    public enum PythonSetupNeed
    {
        // Nothing the app can fix: either it's ready, or Error says what's wrong (e.g. a Python set by hand is broken).
        None,
        // No Python 3.10 on this computer. The user has to install it.
        Python,
        // Python 3.10 is there, but PyTorch / ML-Agents aren't installed in the venv yet (or the venv is broken).
        Packages
    }

    // Finds a Python that can run the trainer (Check), and creates or repairs the venv in the RoverSim
    // folder when asked (Install). Check never installs anything, so the user can be asked first.
    public class PythonEnvironment
    {
        private const string LogPrefix = "[Python setup]";
        private const string PipFlags = "--disable-pip-version-check --progress-bar off";

        // Share of the progress bar for each install step, and roughly how long it usually takes. Within a
        // step the bar creeps toward the step's end, since pip gives no percentage we can read.
        private const float VenvWeight = 0.04f, PipWeight = 0.04f, TorchWeight = 0.65f, MlAgentsWeight = 0.24f, VerifyWeight = 0.03f;
        private const float VenvSeconds = 15f, PipSeconds = 20f, TorchSeconds = 300f, MlAgentsSeconds = 120f, VerifySeconds = 10f;

        private PythonSetupSettings settings;
        private PythonProcess current;
        private PythonCommand basePython;
        private bool venvExists;
        private bool venvPythonWorks;
        private readonly List<string> otherPythons = new List<string>();
        private float progressDone;
        private float progressTotal = 1f;

        public PythonCommand Python { get; private set; }
        public bool IsReady { get; private set; }
        public string Error { get; private set; }
        public string Status { get; private set; } = "";

        public PythonSetupNeed Need { get; private set; }
        // True when Need is Packages because the venv exists but is broken, rather than missing.
        public bool RepairsVenv => Need == PythonSetupNeed.Packages && venvExists;
        public string VenvFolder => settings == null ? "" : PythonPaths.Resolve(settings.VenvFolder);
        // Pythons that ran but aren't 3.10, e.g. "3.12.4". Empty when none were found at all.
        public IReadOnlyList<string> OtherPythonVersions => otherPythons;
        // Why the packages need installing, e.g. "PyTorch is not installed (...)".
        public string PackageProblem { get; private set; }

        public bool IsInstalling { get; private set; }
        public float Progress { get; private set; }

        // Looks for a ready Python: the one set on the TrainingOrganizer or in ROVERSIM_PYTHON, then the venv,
        // then Python on the PATH. Installs nothing. Afterwards IsReady is true, or Need and Error say why not.
        public IEnumerator Check(PythonSetupSettings setupSettings)
        {
            settings = setupSettings;
            IsReady = false;
            Error = null;
            Need = PythonSetupNeed.None;
            PackageProblem = null;
            Progress = 0f;
            basePython = default;
            venvExists = false;
            venvPythonWorks = false;
            otherPythons.Clear();
            PythonStatus status = null;
            string problem;

            PythonCommand command;
            string source;
            if (PythonLocator.TryGetExplicit(settings, out command, out source))
            {
                yield return Probe(command, s => status = s);
                if (status.IsReady(settings, out problem))
                    Succeed(command, status);
                else
                    Error = $"{problem} Python came from {source} ({command}), which is never changed automatically. Fix that install, or clear the setting so the app manages its own venv.";
                yield break;
            }

            PythonCommand venv = PythonLocator.Venv(settings);
            venvExists = File.Exists(venv.Executable);

            if (venvExists)
            {
                yield return Probe(venv, s => status = s);
                if (status.IsReady(settings, out problem))
                {
                    Succeed(venv, status);
                    yield break;
                }

                venvPythonWorks = status.HasUsablePython(settings);
                PackageProblem = problem;
                Debug.LogWarning($"{LogPrefix} The venv at {venv.Executable} needs repair: {problem}");
            }
            else
            {
                foreach (PythonCommand candidate in PythonLocator.SystemCandidates())
                {
                    yield return Probe(candidate, s => status = s);
                    if (status.IsReady(settings, out problem))
                    {
                        Succeed(candidate, status);
                        yield break;
                    }

                    NoteVersion(status);
                    if (basePython.IsEmpty && status.HasUsablePython(settings))
                    {
                        basePython = candidate;
                        PackageProblem = problem;
                    }
                }
            }

            if (!venvPythonWorks && basePython.IsEmpty)
            {
                // A broken venv: the base Python still has to be found to rebuild it.
                if (venvExists)
                {
                    foreach (PythonCommand candidate in PythonLocator.SystemCandidates())
                    {
                        yield return Probe(candidate, s => status = s);
                        NoteVersion(status);
                        if (status.HasUsablePython(settings))
                        {
                            basePython = candidate;
                            break;
                        }
                    }
                }

                if (basePython.IsEmpty)
                {
                    Need = PythonSetupNeed.Python;
                    string found = otherPythons.Count > 0 ? $" Found Python {string.Join(", ", otherPythons)}, which ML-Agents can't use." : "";
                    Error = $"Python {settings.MinPythonVersion} to {settings.MaxPythonVersion} was not found.{found} Install Python 3.10 " +
                            $"(3.10.11 is the last version with a Windows installer) from {settings.PythonDownloadUrl} " +
                            $"and add it to the PATH, or set {PythonSetupSettings.EnvironmentVariable}. Then press Run Training again.";
                    Status = "Python 3.10 not found.";
                    yield break;
                }
            }

            Need = PythonSetupNeed.Packages;
            Error = settings.InstallAutomatically
                ? $"The Python trainer (PyTorch and ML-Agents) isn't installed: {PackageProblem}"
                : "ml-agents is not set up. Turn on Install Automatically on the TrainingOrganizer, or install " +
                  $"Python {settings.MaxPythonVersion}, {settings.TorchPackage} and {settings.MlAgentsPackage} yourself.";
            Status = "The Python trainer isn't installed.";
        }

        // Creates (or rebuilds) the venv and installs PyTorch and ML-Agents into it. Only after Check
        // returned Need == Packages. Progress goes from 0 to 1 and Status names the current step.
        public IEnumerator Install()
        {
            if (Need != PythonSetupNeed.Packages)
                yield break;

            IsInstalling = true;
            IsReady = false;
            Error = null;
            Progress = 0f;
            progressDone = 0f;

            bool createVenv = !venvPythonWorks;
            int stepCount = createVenv ? 4 : 3;
            int step = 0;
            progressTotal = (createVenv ? VenvWeight : 0f) + PipWeight + TorchWeight + MlAgentsWeight + VerifyWeight;

            PythonCommand venv = PythonLocator.Venv(settings);
            PythonStatus status = null;
            bool ok = false;

            if (createVenv)
            {
                string venvFolder = VenvFolder;
                if (venvExists && !TryDeleteFolder(venvFolder))
                {
                    IsInstalling = false;
                    yield break;
                }

                yield return RunStep(basePython, $"-m venv {PythonProbe.Quote(venvFolder)}",
                    $"Creating the Python environment (step {++step} of {stepCount})", VenvWeight, VenvSeconds, r => ok = r);
                if (!Continue(ok))
                    yield break;
            }

            yield return RunStep(venv, $"-m pip install --upgrade pip {PipFlags}",
                $"Updating pip (step {++step} of {stepCount})", PipWeight, PipSeconds, r => ok = r);
            if (!Continue(ok))
                yield break;

            string torchIndex = PythonPaths.IsWindows && !string.IsNullOrWhiteSpace(settings.WindowsTorchIndexUrl)
                ? " --index-url " + settings.WindowsTorchIndexUrl.Trim()
                : "";
            yield return RunStep(venv, $"-m pip install {PythonProbe.Quote(settings.TorchPackage)}{torchIndex} {PipFlags}",
                $"Downloading and installing PyTorch (step {++step} of {stepCount})", TorchWeight, TorchSeconds, r => ok = r);
            if (!Continue(ok))
                yield break;

            yield return RunStep(venv, $"-m pip install {PythonProbe.Quote(settings.MlAgentsPackage)}{SupportPackages()} {PipFlags}",
                $"Installing ML-Agents (step {++step} of {stepCount})", MlAgentsWeight, MlAgentsSeconds, r => ok = r);
            if (!Continue(ok))
                yield break;

            float verifyStart = Time.realtimeSinceStartup;
            Status = "Checking the install";
            yield return Probe(venv, s => status = s, () => Creep(VerifyWeight, Time.realtimeSinceStartup - verifyStart, VerifySeconds));
            IsInstalling = false;

            string problem;
            if (status.IsReady(settings, out problem))
            {
                Progress = 1f;
                Need = PythonSetupNeed.None;
                Succeed(venv, status);
            }
            else
            {
                Error = $"Setup finished but the environment still isn't usable: {problem} See the {LogPrefix} lines in the Console.";
            }
        }

        public void Cancel()
        {
            IsInstalling = false;
            if (current == null)
                return;

            current.Kill();
            current.Dispose();
            current = null;
        }

        private bool Continue(bool ok)
        {
            if (!ok)
                IsInstalling = false;
            return ok;
        }

        private void NoteVersion(PythonStatus status)
        {
            if (status == null || !status.Ran || status.Python == null || status.HasUsablePython(settings))
                return;

            string version = status.Python.ToString();
            if (!otherPythons.Contains(version))
                otherPythons.Add(version);
        }

        private string SupportPackages()
        {
            string result = "";
            foreach (string package in (settings.SupportPackages ?? "").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
                result += " " + PythonProbe.Quote(package);
            return result;
        }

        // Moves the bar part of the way through a step: fast at first, then slowing so it never quite
        // reaches the end of the step before the step really finishes.
        private void Creep(float weight, float elapsed, float expectedSeconds)
        {
            float within = 0.95f * (1f - Mathf.Exp(-elapsed / Mathf.Max(1f, expectedSeconds)));
            Progress = Mathf.Clamp01((progressDone + weight * within) / progressTotal);
        }

        private IEnumerator Probe(PythonCommand command, Action<PythonStatus> done, Action whileRunning = null)
        {
            if (!IsInstalling)
                Status = "Checking Python...";
            var process = new PythonProcess(LogPrefix, false);
            string error;
            if (!process.Start(command, PythonProbe.Arguments(), PythonPaths.BaseDirectory, out error))
            {
                done(new PythonStatus());
                yield break;
            }

            current = process;
            float deadline = Time.realtimeSinceStartup + PythonProbe.TimeoutSeconds;
            while (process.IsRunning && Time.realtimeSinceStartup < deadline)
            {
                process.PumpOutput();
                whileRunning?.Invoke();
                yield return null;
            }

            if (process.IsRunning)
                process.Kill();

            bool exited = process.WaitForExit(1000);
            process.PumpOutput();
            done(PythonStatus.Parse(process.Lines, exited));
            process.Dispose();
            current = null;
        }

        private IEnumerator RunStep(PythonCommand command, string arguments, string status, float weight, float expectedSeconds, Action<bool> done)
        {
            Status = status;
            Debug.Log($"{LogPrefix} {status} ({command} {arguments})");

            var process = new PythonProcess(LogPrefix);
            string error;
            if (!process.Start(command, arguments, PythonPaths.BaseDirectory, out error))
            {
                Error = error;
                done(false);
                yield break;
            }

            current = process;
            float started = Time.realtimeSinceStartup;
            while (process.IsRunning)
            {
                process.PumpOutput();
                Creep(weight, Time.realtimeSinceStartup - started, expectedSeconds);
                yield return null;
            }

            process.WaitForExit(1000);
            int exitCode = process.ExitCode;
            string output = process.RecentOutput();
            process.Dispose();
            current = null;

            if (exitCode != 0)
                Error = $"{StepName(status)} failed with exit code {exitCode}. Last output:\n{output}";

            progressDone += weight;
            Progress = Mathf.Clamp01(progressDone / progressTotal);
            done(exitCode == 0);
        }

        // "Updating pip (step 2 of 4)" -> "Updating pip"
        private static string StepName(string status)
        {
            int bracket = status.IndexOf(" (", StringComparison.Ordinal);
            return bracket > 0 ? status.Substring(0, bracket) : status.TrimEnd('.');
        }

        private void Succeed(PythonCommand command, PythonStatus status)
        {
            Python = command;
            IsReady = true;
            Need = PythonSetupNeed.None;
            Error = null;
            Status = "Python is ready.";
            Debug.Log($"{LogPrefix} Using {command}: Python {status.Python}, PyTorch {status.Torch}, ml-agents {status.MlAgents}.");
        }

        private bool TryDeleteFolder(string folder)
        {
            try
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
                return true;
            }
            catch (Exception e)
            {
                Error = $"The venv at {folder} is broken and could not be removed: {e.Message}. Delete it and press Run Training again.";
                return false;
            }
        }
    }
}
