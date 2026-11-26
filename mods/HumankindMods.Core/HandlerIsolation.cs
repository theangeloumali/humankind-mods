using System;

namespace HumankindMods.Core
{
    /// <summary>
    /// Runs one handler of a game event so its exception can't stop the handlers after it, and reports each
    /// distinct exception once, with its real stack, up to a cap (the event may fire every frame).
    /// </summary>
    public sealed class HandlerIsolation
    {
        private readonly DistinctReporter reporter;
        private readonly Action<string> report;

        public HandlerIsolation(int maxReports, Action<string> report)
        {
            reporter = new DistinctReporter(maxReports);
            this.report = report;
        }

        public void Run(Action handler)
        {
            try
            {
                handler();
            }
            catch (Exception exception)
            {
                if (reporter.ShouldReport(exception.GetType().FullName + ": " + exception.Message + " @ " + exception.TargetSite))
                {
                    report(exception.ToString());
                }
            }
        }
    }
}
