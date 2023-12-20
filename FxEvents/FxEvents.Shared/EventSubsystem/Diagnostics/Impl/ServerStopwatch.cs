using System;
using System.Diagnostics;

namespace FxEvents.Shared.Diagnostics.Impl
{
    internal class ServerStopwatch : StopwatchUtil
    {
        private readonly Stopwatch _stopwatch;
        public override TimeSpan Elapsed => _stopwatch.Elapsed;

        public ServerStopwatch()
        {
            _stopwatch = Stopwatch.StartNew();
        }

        public override void Stop()
        {
            _stopwatch.Stop();
        }

        public override void Start()
        {
            _stopwatch.Start();
        }

        internal static long GetTimestamp()
        {
            return Stopwatch.GetTimestamp() / 10000;
        }
    }
}