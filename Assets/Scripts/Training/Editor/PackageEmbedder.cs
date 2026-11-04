using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Training.EditorTools
{
    public static class PackageEmbedder
    {
        private static EmbedRequest request;
        private static Action<PackageInfo, string> onDone;

        public static bool IsBusy => request != null;

        public static void Embed(string packageName, Action<PackageInfo, string> done)
        {
            onDone = done;
            request = Client.Embed(packageName);
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (request == null || !request.IsCompleted)
                return;

            EditorApplication.update -= Poll;
            EmbedRequest finished = request;
            Action<PackageInfo, string> done = onDone;
            request = null;
            onDone = null;

            if (finished.Status == StatusCode.Success)
                done(finished.Result, null);
            else
                done(null, finished.Error != null ? finished.Error.message : "Unknown Package Manager error.");
        }
    }
}
