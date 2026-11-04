namespace Training
{
    public readonly struct PythonCommand
    {
        public readonly string Executable;
        private readonly string prefixArguments;

        public PythonCommand(string executable, string prefixArguments = "")
        {
            Executable = executable;
            this.prefixArguments = prefixArguments;
        }

        public string PrefixArguments => prefixArguments ?? "";
        public bool IsEmpty => string.IsNullOrEmpty(Executable);

        public string Arguments(string rest)
        {
            if (PrefixArguments.Length == 0)
                return rest;
            return string.IsNullOrEmpty(rest) ? PrefixArguments : PrefixArguments + " " + rest;
        }

        public override string ToString()
        {
            if (IsEmpty)
                return "(none)";
            return PrefixArguments.Length == 0 ? Executable : Executable + " " + PrefixArguments;
        }
    }
}
