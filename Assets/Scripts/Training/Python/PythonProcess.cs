using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Debug = UnityEngine.Debug;

namespace Training
{
    public class PythonProcess : IDisposable
    {
        private const int TailLength = 30;

        private readonly ConcurrentQueue<string> pendingLines = new ConcurrentQueue<string>();
        private readonly Queue<string> tail = new Queue<string>();
        private readonly List<string> allLines = new List<string>();
        private readonly string logPrefix;
        private readonly bool logOutput;
        private Process process;

        public PythonProcess(string logPrefix, bool logOutput = true)
        {
            this.logPrefix = logPrefix;
            this.logOutput = logOutput;
        }

        public bool IsRunning => process != null && !process.HasExited;
        public bool HasExited => process != null && process.HasExited;
        public int ExitCode => HasExited ? process.ExitCode : 0;
        public IReadOnlyList<string> Lines => allLines;

        public bool Start(PythonCommand command, string arguments, string workingDirectory, out string error)
        {
            var startInfo = new ProcessStartInfo(command.Executable, command.Arguments(arguments))
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            startInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
            startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

            try
            {
                process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                process.OutputDataReceived += OnOutput;
                process.ErrorDataReceived += OnOutput;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception e)
            {
                process = null;
                error = $"Could not start '{command}': {e.Message}";
                return false;
            }

            error = null;
            return true;
        }

        public void PumpOutput()
        {
            string line;
            while (pendingLines.TryDequeue(out line))
            {
                allLines.Add(line);
                tail.Enqueue(line);
                if (tail.Count > TailLength)
                    tail.Dequeue();

                if (logOutput)
                    Debug.Log($"{logPrefix} {line}");
            }
        }

        public string RecentOutput()
        {
            PumpOutput();
            return string.Join("\n", tail.ToArray());
        }

        public bool WaitForExit(int milliseconds)
        {
            bool exited = process == null || process.WaitForExit(milliseconds);
            if (exited && process != null)
                process.WaitForExit();
            return exited;
        }

        public void Kill()
        {
            if (!IsRunning)
                return;

            string error;
            if (!ProcessKiller.KillTree(process.Id, PythonPaths.IsWindows, out error))
                Debug.LogWarning($"{logPrefix} Could not stop process {process.Id}: {error}");
        }

        public void Dispose()
        {
            if (process == null)
                return;

            process.OutputDataReceived -= OnOutput;
            process.ErrorDataReceived -= OnOutput;
            process.Dispose();
            process = null;
        }

        private void OnOutput(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
                pendingLines.Enqueue(e.Data);
        }
    }
}
