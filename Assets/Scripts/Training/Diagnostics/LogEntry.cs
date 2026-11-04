using System;
using UnityEngine;

namespace Training
{
    public readonly struct LogEntry
    {
        public readonly DateTime Time;
        public readonly LogType Type;
        public readonly string Message;
        public readonly string StackTrace;
        public readonly string Tag;

        public LogEntry(DateTime time, LogType type, string message, string stackTrace)
        {
            Time = time;
            Type = type;
            Message = message ?? "";
            StackTrace = stackTrace ?? "";
            Tag = ReadTag(Message);
        }

        public bool IsError => Type == LogType.Error || Type == LogType.Exception || Type == LogType.Assert;
        public bool IsWarning => Type == LogType.Warning;

        private static string ReadTag(string message)
        {
            if (message.Length < 3 || message[0] != '[')
                return "";

            int end = message.IndexOf(']');
            return end > 1 ? message.Substring(1, end - 1) : "";
        }
    }
}
