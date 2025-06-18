using NLog.Config;
using NLog.Targets;
using System.Collections.Generic;

namespace MyCompany.Logging.Tests
{
    [Target("UnitTestTarget")]
    public sealed class UnitTestTarget : TargetWithLayout
    {
        public List<NLog.LogEventInfo> Events { get; } = new List<NLog.LogEventInfo>();
        public UnitTestTarget() { this.Name = "UnitTestTarget"; }
        protected override void Write(NLog.LogEventInfo logEvent) { Events.Add(logEvent); }
    }
}