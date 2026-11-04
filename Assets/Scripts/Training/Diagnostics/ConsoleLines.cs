using System.Collections.Generic;
using UnityEngine;

namespace Training
{
    public readonly struct ConsoleLine
    {
        public readonly string Text;
        public readonly LogType Type;
        public readonly bool IsStackTrace;

        public ConsoleLine(string text, LogType type, bool isStackTrace)
        {
            Text = text;
            Type = type;
            IsStackTrace = isStackTrace;
        }
    }

    public static class ConsoleLines
    {
        private const string Indent = "           ";

        public static int Build(List<LogEntry> entries, LogFilter filter, bool stackTraces, List<ConsoleLine> output)
        {
            output.Clear();
            int widest = 0;

            foreach (LogEntry entry in entries)
            {
                if (!filter.Matches(entry))
                    continue;

                string[] parts = Split(entry.Message);
                for (int i = 0; i < parts.Length; i++)
                {
                    string text = i == 0 ? entry.Time.ToString("HH:mm:ss") + "   " + parts[i] : Indent + parts[i];
                    widest = Add(output, new ConsoleLine(text, entry.Type, false), widest);
                }

                if (!stackTraces || !entry.IsError)
                    continue;

                foreach (string frame in Split(entry.StackTrace))
                {
                    if (frame.Length > 0)
                        widest = Add(output, new ConsoleLine(Indent + frame, entry.Type, true), widest);
                }
            }

            return widest;
        }

        public static void Count(List<LogEntry> entries, out int info, out int warnings, out int errors)
        {
            info = warnings = errors = 0;
            foreach (LogEntry entry in entries)
            {
                if (entry.IsError)
                    errors++;
                else if (entry.IsWarning)
                    warnings++;
                else
                    info++;
            }
        }

        public static string ToText(List<ConsoleLine> lines)
        {
            var text = new System.Text.StringBuilder();
            foreach (ConsoleLine line in lines)
                text.AppendLine(line.Text);
            return text.ToString();
        }

        private static string[] Split(string text)
        {
            return text.Replace("\r", "").TrimEnd('\n').Split('\n');
        }

        private static int Add(List<ConsoleLine> output, ConsoleLine line, int widest)
        {
            output.Add(line);
            return Mathf.Max(widest, line.Text.Length);
        }
    }
}
