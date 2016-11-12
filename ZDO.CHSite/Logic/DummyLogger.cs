using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ZDO.CHSite.Logic
{
    public class DummyLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) { return null; }

        public bool IsEnabled(LogLevel logLevel) { return false; }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            // Dummy.
        }
    }
}
