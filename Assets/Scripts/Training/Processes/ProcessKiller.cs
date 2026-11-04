using System;
using System.Diagnostics;

namespace Training
{
    public static class ProcessKiller
    {
        private const int CommandTimeoutMilliseconds = 5000;

        public static bool KillTree(int pid, bool windows, out string error)
        {
            string ignored;
            try
            {
                if (windows)
                {
                    ShellCommand.TryRun("taskkill", $"/PID {pid} /T /F", CommandTimeoutMilliseconds, out ignored);
                }
                else
                {
                    ShellCommand.TryRun("pkill", $"-KILL -P {pid}", CommandTimeoutMilliseconds, out ignored);
                    ShellCommand.TryRun("kill", $"-KILL {pid}", CommandTimeoutMilliseconds, out ignored);
                }

                using (Process process = Process.GetProcessById(pid))
                {
                    if (!process.HasExited)
                        process.Kill();
                }
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }

            error = null;
            return true;
        }
    }
}
