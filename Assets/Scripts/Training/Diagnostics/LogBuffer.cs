using System;
using System.Collections.Generic;
using UnityEngine;

namespace Training
{
    public class LogBuffer
    {
        private readonly object gate = new object();
        private readonly Queue<LogEntry> entries = new Queue<LogEntry>();
        private readonly int capacity;

        public LogBuffer(int capacity)
        {
            this.capacity = Math.Max(1, capacity);
        }

        public int Version { get; private set; }

        public void Add(string message, string stackTrace, LogType type)
        {
            var entry = new LogEntry(DateTime.Now, type, message, stackTrace);
            lock (gate)
            {
                entries.Enqueue(entry);
                while (entries.Count > capacity)
                    entries.Dequeue();
                Version++;
            }
        }

        public void Clear()
        {
            lock (gate)
            {
                entries.Clear();
                Version++;
            }
        }

        public int CopyTo(List<LogEntry> target)
        {
            lock (gate)
            {
                target.Clear();
                target.AddRange(entries);
                return Version;
            }
        }
    }
}
