using System;

namespace Training
{
    public readonly struct PortOwner
    {
        private const int MaxCommandLength = 160;

        public readonly int Pid;
        public readonly string Name;
        public readonly string CommandLine;

        public PortOwner(int pid, string name, string commandLine)
        {
            Pid = pid;
            Name = string.IsNullOrWhiteSpace(name) ? "unknown program" : name.Trim();
            CommandLine = commandLine == null ? "" : commandLine.Trim();
        }

        public bool IsMlAgentsTrainer => Contains(CommandLine, "mlagents") || Contains(Name, "mlagents");

        public bool IsPython
        {
            get
            {
                string name = Name.ToLowerInvariant();
                return name.StartsWith("python") || name == "py" || name == "pyw";
            }
        }

        public bool MayBeForceStopped => IsMlAgentsTrainer || IsPython;

        public string Describe()
        {
            string command = CommandLine.Length > MaxCommandLength ? CommandLine.Substring(0, MaxCommandLength) + "..." : CommandLine;
            return command.Length > 0 ? $"{Name} (PID {Pid}): {command}" : $"{Name} (PID {Pid})";
        }

        private static bool Contains(string text, string value)
        {
            return text != null && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
