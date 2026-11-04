using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;

namespace Training
{
    public static class PortOwnerFinder
    {
        private const int CommandTimeoutMilliseconds = 8000;

        public static Task<List<PortOwner>> FindAsync(int port, bool windows)
        {
            return Task.Run(() =>
            {
                var owners = new List<PortOwner>();
                foreach (int pid in windows ? WindowsListeners(port) : UnixListeners(port))
                    owners.Add(new PortOwner(pid, ProcessName(pid), windows ? WindowsCommandLine(pid) : UnixCommandLine(pid)));
                return owners;
            });
        }

        public static List<int> ParseNetstat(string output, int port)
        {
            var pids = new List<int>();
            string suffix = ":" + port.ToString(CultureInfo.InvariantCulture);

            foreach (string line in output.Split('\n'))
            {
                string[] parts = line.Split(new[] { ' ', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5 || !parts[0].Equals("TCP", StringComparison.OrdinalIgnoreCase))
                    continue;

                bool localMatches = parts[1].EndsWith(suffix, StringComparison.Ordinal);
                bool listening = parts[2] == "0.0.0.0:0" || parts[2] == "[::]:0" || parts[2] == "*:*";
                int pid;
                if (localMatches && listening && int.TryParse(parts[parts.Length - 1], out pid) && pid > 0 && !pids.Contains(pid))
                    pids.Add(pid);
            }

            return pids;
        }

        public static List<int> ParsePidList(string output)
        {
            var pids = new List<int>();
            foreach (string line in output.Split('\n'))
            {
                int pid;
                if (int.TryParse(line.Trim(), out pid) && pid > 0 && !pids.Contains(pid))
                    pids.Add(pid);
            }

            return pids;
        }

        public static List<int> ParseSs(string output)
        {
            var pids = new List<int>();
            int index = 0;
            while ((index = output.IndexOf("pid=", index, StringComparison.Ordinal)) >= 0)
            {
                index += 4;
                int end = index;
                while (end < output.Length && char.IsDigit(output[end]))
                    end++;

                int pid;
                if (int.TryParse(output.Substring(index, end - index), out pid) && pid > 0 && !pids.Contains(pid))
                    pids.Add(pid);
            }

            return pids;
        }

        private static List<int> WindowsListeners(int port)
        {
            string output;
            return ShellCommand.TryRun("netstat", "-ano", CommandTimeoutMilliseconds, out output)
                ? ParseNetstat(output, port)
                : new List<int>();
        }

        private static List<int> UnixListeners(int port)
        {
            string output;
            if (ShellCommand.TryRun("lsof", $"-nP -iTCP:{port} -sTCP:LISTEN -t", CommandTimeoutMilliseconds, out output))
            {
                List<int> pids = ParsePidList(output);
                if (pids.Count > 0)
                    return pids;
            }

            return ShellCommand.TryRun("ss", $"-ltnpH sport = :{port}", CommandTimeoutMilliseconds, out output)
                ? ParseSs(output)
                : new List<int>();
        }

        private static string ProcessName(int pid)
        {
            try
            {
                using (Process process = Process.GetProcessById(pid))
                    return process.ProcessName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string WindowsCommandLine(int pid)
        {
            string output;
            return ShellCommand.TryRun("powershell", $"-NoProfile -NonInteractive -Command \"(Get-CimInstance Win32_Process -Filter 'ProcessId={pid}').CommandLine\"",
                CommandTimeoutMilliseconds, out output) ? output.Trim() : "";
        }

        private static string UnixCommandLine(int pid)
        {
            string output;
            return ShellCommand.TryRun("ps", $"-o command= -p {pid}", CommandTimeoutMilliseconds, out output) ? output.Trim() : "";
        }
    }
}
