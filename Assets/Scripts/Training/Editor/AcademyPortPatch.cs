using System.Text.RegularExpressions;

namespace Training.EditorTools
{
    public static class AcademyPortPatch
    {
        public const string VariableName = "MLAGENTS_RUNTIME_PORT";

        private static readonly Regex ReadPortMethod = new Regex(@"(?<indent>[ \t]*)static int ReadPortFromArgs\(\)(?<newline>\r?\n)[ \t]*\{");

        public static bool IsApplied(string source)
        {
            return source.Contains(AcademyConnection.RuntimePortField);
        }

        public static bool TryApply(string source, out string patched)
        {
            Match match = ReadPortMethod.Match(source);
            if (!match.Success)
            {
                patched = null;
                return false;
            }

            patched = source.Substring(0, match.Index) + Header(match) + source.Substring(match.Index + match.Length);
            return true;
        }

        private static string Header(Match match)
        {
            string i = match.Groups["indent"].Value;
            string n = match.Groups["newline"].Value;
            string field = AcademyConnection.RuntimePortField;

            return $"{i}internal const string {field} = \"{VariableName}\";{n}{n}" +
                   $"{i}static int ReadPortFromArgs(){n}" +
                   $"{i}{{{n}" +
                   $"{i}    var runtimePort = Environment.GetEnvironmentVariable({field});{n}" +
                   $"{i}    if (int.TryParse(runtimePort, out var parsedRuntimePort) && parsedRuntimePort > 0){n}" +
                   $"{i}    {{{n}" +
                   $"{i}        return parsedRuntimePort;{n}" +
                   $"{i}    }}{n}";
        }
    }
}
