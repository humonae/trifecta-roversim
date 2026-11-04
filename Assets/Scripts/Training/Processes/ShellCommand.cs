using System;
using System.Diagnostics;
using System.Text;

namespace Training
{
    public static class ShellCommand
    {
        public static bool TryRun(string fileName, string arguments, int timeoutMilliseconds, out string output)
        {
            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            try
            {
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        output = "";
                        return false;
                    }

                    var text = new StringBuilder();
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                            lock (text) text.AppendLine(e.Data);
                    };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (!process.WaitForExit(timeoutMilliseconds))
                    {
                        process.Kill();
                        output = "";
                        return false;
                    }

                    process.WaitForExit();
                    lock (text) output = text.ToString();
                    return true;
                }
            }
            catch (Exception)
            {
                output = "";
                return false;
            }
        }
    }
}
