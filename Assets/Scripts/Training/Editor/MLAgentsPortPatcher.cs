using System;
using System.IO;
using Unity.MLAgents;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Training.EditorTools
{
    public static class MLAgentsPortPatcher
    {
        private const string MenuPath = "Tools/RoverSim/Patch ML-Agents For In-App Training";
        private const string AcademyFile = "Runtime/Academy.cs";
        private const string PendingKey = "RoverSim.MLAgentsPatchPending";

        [MenuItem(MenuPath)]
        public static void Patch()
        {
            if (PackageEmbedder.IsBusy)
            {
                Report("Already copying ML-Agents into the project. Wait for it to finish.", false);
                return;
            }

            PackageInfo package = PackageInfo.FindForAssembly(typeof(Academy).Assembly);
            if (package == null)
            {
                Report("Could not find the ML-Agents package.", true);
                return;
            }

            if (package.source == PackageSource.Embedded)
            {
                PatchEmbedded(package);
                return;
            }

            Debug.Log($"[MLAgentsPortPatcher] Copying ML-Agents {package.version} into Packages/{package.name}...");
            SessionState.SetBool(PendingKey, true);
            PackageEmbedder.Embed(package.name, OnEmbedded);
        }

        [InitializeOnLoadMethod]
        private static void ResumeAfterReload()
        {
            if (!SessionState.GetBool(PendingKey, false))
                return;

            EditorApplication.delayCall += () =>
            {
                PackageInfo package = PackageInfo.FindForAssembly(typeof(Academy).Assembly);
                if (package != null && package.source == PackageSource.Embedded && !PackageEmbedder.IsBusy)
                    PatchEmbedded(package);
            };
        }

        private static void OnEmbedded(PackageInfo package, string error)
        {
            if (error != null)
            {
                SessionState.SetBool(PendingKey, false);
                Report($"Unity could not copy ML-Agents into the project: {error}", true);
                return;
            }

            PatchEmbedded(package);
        }

        private static void PatchEmbedded(PackageInfo package)
        {
            SessionState.SetBool(PendingKey, false);

            try
            {
                string academyPath = Path.Combine(package.resolvedPath, AcademyFile);
                string source = File.ReadAllText(academyPath);

                if (AcademyPortPatch.IsApplied(source))
                {
                    Report($"ML-Agents {package.version} in {package.resolvedPath} is patched. Commit the Packages/{package.name} folder so the whole team gets it.", false);
                    return;
                }

                string patched;
                if (!AcademyPortPatch.TryApply(source, out patched))
                {
                    Report($"Could not find ReadPortFromArgs in {academyPath}. This ML-Agents version needs a manual patch.", true);
                    return;
                }

                File.SetAttributes(academyPath, FileAttributes.Normal);
                File.WriteAllText(academyPath, patched);
                AssetDatabase.Refresh();

                Report($"Patched ML-Agents {package.version} in {package.resolvedPath}. Commit the Packages/{package.name} folder so the whole team gets it.", false);
            }
            catch (Exception e)
            {
                Report($"Patching failed: {e.Message}", true);
            }
        }

        private static void Report(string message, bool isError)
        {
            if (isError)
                Debug.LogError($"[MLAgentsPortPatcher] {message}");
            else
                Debug.Log($"[MLAgentsPortPatcher] {message}");

            EditorUtility.DisplayDialog("Patch ML-Agents", message, "OK");
        }
    }
}
