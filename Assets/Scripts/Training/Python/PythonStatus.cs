using System;
using System.Collections.Generic;

namespace Training
{
    public class PythonStatus
    {
        public bool Ran;
        public Version Python;
        public Version Torch;
        public Version MlAgents;
        public string TorchError;
        public string MlAgentsError;

        public bool HasUsablePython(PythonSetupSettings settings)
        {
            return Ran && Python != null
                   && Python >= ParseVersion(settings.MinPythonVersion)
                   && Python <= ParseVersion(settings.MaxPythonVersion);
        }

        public bool IsReady(PythonSetupSettings settings, out string problem)
        {
            if (!Ran || Python == null)
                problem = "Python could not be run.";
            else if (!HasUsablePython(settings))
                problem = $"Python {Python} is not supported; ML-Agents needs {settings.MinPythonVersion} to {settings.MaxPythonVersion}.";
            else if (Torch == null)
                problem = $"PyTorch is not installed ({TorchError ?? "import failed"}).";
            else if (Torch > ParseVersion(settings.MaxTorchVersion))
                problem = $"PyTorch {Torch} is too new; ML-Agents model export needs {settings.TorchPackage}.";
            else if (MlAgents == null)
                problem = $"ml-agents is not installed ({MlAgentsError ?? "import failed"}).";
            else if (MlAgents < ParseVersion(settings.MinMlAgentsVersion))
                problem = $"ml-agents {MlAgents} is too old; {settings.MlAgentsPackage} is needed.";
            else
                problem = null;

            return problem == null;
        }

        public static PythonStatus Parse(IEnumerable<string> lines, bool exitedCleanly)
        {
            var status = new PythonStatus { Ran = exitedCleanly };
            foreach (string line in lines)
            {
                string key;
                string value = Split(line, out key);
                switch (key)
                {
                    case "PY": status.Python = ParseVersion(value); break;
                    case "TORCH": status.Torch = ParseVersion(value); break;
                    case "TORCH_ERR": status.TorchError = value; break;
                    case "MLAGENTS": status.MlAgents = ParseVersion(value); break;
                    case "MLAGENTS_ERR": status.MlAgentsError = value; break;
                }
            }

            status.Ran &= status.Python != null;
            return status;
        }

        public static Version ParseVersion(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var parts = new List<int>();
            foreach (string part in text.Trim().Split('.'))
            {
                int digits = 0;
                while (digits < part.Length && char.IsDigit(part[digits]))
                    digits++;
                if (digits == 0)
                    break;

                parts.Add(int.Parse(part.Substring(0, digits)));
                if (parts.Count == 3 || digits < part.Length)
                    break;
            }

            if (parts.Count == 0)
                return null;

            while (parts.Count < 3)
                parts.Add(0);

            return new Version(parts[0], parts[1], parts[2]);
        }

        private static string Split(string line, out string key)
        {
            int space = line.IndexOf(' ');
            key = space < 0 ? line : line.Substring(0, space);
            return space < 0 ? "" : line.Substring(space + 1).Trim();
        }
    }
}
