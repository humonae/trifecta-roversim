using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Training
{
    public class PythonEnvironment
    {
        private const string LogPrefix = "[Python setup]";
        private const string PipFlags = "--disable-pip-version-check --progress-bar off";

        private PythonSetupSettings settings;
        private PythonProcess current;

        public PythonCommand Python { get; private set; }
        public bool IsReady { get; private set; }
        public string Error { get; private set; }
        public string Status { get; private set; } = "";

        public IEnumerator Ensure(PythonSetupSettings setupSettings)
        {
            settings = setupSettings;
            IsReady = false;
            Error = null;
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
            bool venvExists = File.Exists(venv.Executable);
            bool venvPythonWorks = false;
            PythonCommand basePython = default;

            if (venvExists)
            {
                yield return Probe(venv, s => status = s);
                if (status.IsReady(settings, out problem))
                {
                    Succeed(venv, status);
                    yield break;
                }

                venvPythonWorks = status.HasUsablePython(settings);
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

                    if (basePython.IsEmpty && status.HasUsablePython(settings))
                        basePython = candidate;
                }
            }

            if (!settings.InstallAutomatically)
            {
                Error = "ml-agents is not set up. Turn on Install Automatically on the TrainingOrganizer, or install " +
                        $"Python {settings.MaxPythonVersion}, {settings.TorchPackage} and {settings.MlAgentsPackage} yourself.";
                yield break;
            }

            bool ok = false;
            if (!venvPythonWorks)
            {
                if (basePython.IsEmpty)
                {
                    foreach (PythonCommand candidate in PythonLocator.SystemCandidates())
                    {
                        yield return Probe(candidate, s => status = s);
                        if (status.HasUsablePython(settings))
                        {
                            basePython = candidate;
                            break;
                        }
                    }
                }

                if (basePython.IsEmpty)
                {
                    Error = $"Python {settings.MinPythonVersion} to {settings.MaxPythonVersion} was not found. Install Python 3.10 " +
                            "(3.10.11 is the last version with a Windows installer) from https://www.python.org/downloads/ " +
                            $"and add it to the PATH, or set {PythonSetupSettings.EnvironmentVariable}. Then press Run Training again.";
                    yield break;
                }

                string venvFolder = PythonPaths.Resolve(settings.VenvFolder);
                if (venvExists && !TryDeleteFolder(venvFolder))
                    yield break;

                yield return RunStep(basePython, $"-m venv {PythonProbe.Quote(venvFolder)}", "Creating the Python environment...", r => ok = r);
                if (!ok)
                    yield break;
            }

            yield return RunStep(venv, $"-m pip install --upgrade pip {PipFlags}", "Updating pip...", r => ok = r);
            if (!ok)
                yield break;

            string torchIndex = PythonPaths.IsWindows && !string.IsNullOrWhiteSpace(settings.WindowsTorchIndexUrl)
                ? " --index-url " + settings.WindowsTorchIndexUrl.Trim()
                : "";
            yield return RunStep(venv, $"-m pip install {PythonProbe.Quote(settings.TorchPackage)}{torchIndex} {PipFlags}",
                "Installing PyTorch (first run only, this can take several minutes)...", r => ok = r);
            if (!ok)
                yield break;

            yield return RunStep(venv, $"-m pip install {PythonProbe.Quote(settings.MlAgentsPackage)}{SupportPackages()} {PipFlags}", "Installing ml-agents...", r => ok = r);
            if (!ok)
                yield break;

            yield return Probe(venv, s => status = s);
            if (status.IsReady(settings, out problem))
                Succeed(venv, status);
            else
                Error = $"Setup finished but the environment still isn't usable: {problem} See the {LogPrefix} lines in the Console.";
        }

        private string SupportPackages()
        {
            string result = "";
            foreach (string package in (settings.SupportPackages ?? "").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
                result += " " + PythonProbe.Quote(package);
            return result;
        }

        public void Cancel()
        {
            if (current == null)
                return;

            current.Kill();
            current.Dispose();
            current = null;
        }

        private IEnumerator Probe(PythonCommand command, Action<PythonStatus> done)
        {
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

        private IEnumerator RunStep(PythonCommand command, string arguments, string status, Action<bool> done)
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
            while (process.IsRunning)
            {
                process.PumpOutput();
                yield return null;
            }

            process.WaitForExit(1000);
            int exitCode = process.ExitCode;
            string output = process.RecentOutput();
            process.Dispose();
            current = null;

            if (exitCode != 0)
                Error = $"{status.TrimEnd('.')} failed with exit code {exitCode}. Last output:\n{output}";

            done(exitCode == 0);
        }

        private void Succeed(PythonCommand command, PythonStatus status)
        {
            Python = command;
            IsReady = true;
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
