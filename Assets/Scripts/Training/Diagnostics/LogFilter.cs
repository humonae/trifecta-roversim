using System;
using System.Collections.Generic;

namespace Training
{
    public class LogFilter
    {
        private readonly HashSet<string> backendTags;

        public LogFilter(IEnumerable<string> tags)
        {
            backendTags = new HashSet<string>(tags ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        }

        public bool ShowInfo = true;
        public bool ShowWarnings = true;
        public bool ShowErrors = true;
        public bool BackendOnly = true;
        public string Search = "";

        public bool Matches(LogEntry entry)
        {
            if (entry.IsError ? !ShowErrors : entry.IsWarning ? !ShowWarnings : !ShowInfo)
                return false;

            if (BackendOnly && !entry.IsError && !backendTags.Contains(entry.Tag))
                return false;

            return string.IsNullOrEmpty(Search) || entry.Message.IndexOf(Search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
