using System;

namespace FxEvents.Shared.Diagnostics.Impl
{
    internal class ClientStopwatch : StopwatchUtil
    {
        private long _startTicks;
        private long _totalPauseTicks;

        public override TimeSpan Elapsed
        {
            get
            {
                long currentTicks = GetTimestamp();
                long elapsedTicks = currentTicks - _startTicks - _totalPauseTicks;
                return TimeSpan.FromTicks(elapsedTicks);
            }
        }

        public ClientStopwatch()
        {
            _startTicks = GetTimestamp();
        }

        public override void Stop()
        {
            _totalPauseTicks += GetTimestamp() - _startTicks;
        }

        public override void Start()
        {
            _startTicks = GetTimestamp() - _totalPauseTicks;
        }

        internal static long GetTimestamp()
        {
            return DateTime.UtcNow.Ticks;
        }
    }
}