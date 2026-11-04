using System;
using System.Collections.Generic;

namespace Training
{
    public static class PythonLocator
    {
        public static bool TryGetExplicit(PythonSetupSettings settings, out PythonCommand command, out string source)
        {
            if (!string.IsNullOrWhiteSpace(settings.PythonExecutable))
            {
                string configured = settings.PythonExecutable.Trim();
                command = new PythonCommand(PythonPaths.LooksLikePath(configured) ? PythonPaths.Resolve(configured) : configured);
                source = "the Python set on the TrainingOrganizer";
                return true;
            }

            string fromEnvironment = Environment.GetEnvironmentVariable(PythonSetupSettings.EnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                command = new PythonCommand(fromEnvironment.Trim());
                source = PythonSetupSettings.EnvironmentVariable;
                return true;
            }

            command = default;
            source = null;
            return false;
        }

        public static PythonCommand Venv(PythonSetupSettings settings)
        {
            return new PythonCommand(PythonPaths.VenvPython(settings.VenvFolder));
        }

        public static List<PythonCommand> SystemCandidates()
        {
            var candidates = new List<PythonCommand>();
            if (PythonPaths.IsWindows)
            {
                candidates.Add(new PythonCommand("py", "-3.10"));
                candidates.Add(new PythonCommand("python3.10"));
                candidates.Add(new PythonCommand("python"));
                candidates.Add(new PythonCommand("python3"));
            }
            else
            {
                candidates.Add(new PythonCommand("python3.10"));
                candidates.Add(new PythonCommand("python3"));
                candidates.Add(new PythonCommand("python"));
            }

            return candidates;
        }
    }
}
