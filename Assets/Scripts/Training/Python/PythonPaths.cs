using System;
using System.IO;
using UnityEngine;

namespace Training
{
    public static class PythonPaths
    {
        public static string BaseDirectory => Path.GetDirectoryName(Application.dataPath);

        public static bool IsWindows =>
            Application.platform == RuntimePlatform.WindowsEditor ||
            Application.platform == RuntimePlatform.WindowsPlayer;

        public static string Resolve(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return BaseDirectory;

            string expanded = Environment.ExpandEnvironmentVariables(path.Trim());
            return Path.GetFullPath(Path.IsPathRooted(expanded) ? expanded : Path.Combine(BaseDirectory, expanded));
        }

        public static string VenvPython(string venvFolder)
        {
            string root = Resolve(venvFolder);
            return IsWindows
                ? Path.Combine(root, "Scripts", "python.exe")
                : Path.Combine(root, "bin", "python");
        }

        public static string MakeRunId(string runName)
        {
            string name = string.IsNullOrWhiteSpace(runName) ? "rover" : runName.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');

            return $"{name.Replace(' ', '_')}_{DateTime.Now:yyyyMMdd_HHmmss}";
        }

        public static bool LooksLikePath(string value)
        {
            return value.IndexOf('/') >= 0 || value.IndexOf('\\') >= 0;
        }
    }
}
