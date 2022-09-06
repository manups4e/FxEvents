using System;

namespace FxEvents.Shared.Diagnostics.Impl
{
    internal class ClientStopwatch : StopwatchUtil
    {
        private readonly long _timestamp;
        private long _reduction;
        private long _haltedAt;

        public override TimeSpan Elapsed
        {
            get
            {
                EnsureReduction();

                return new TimeSpan((GetTimestamp() - _timestamp - _reduction) * 10000);
            }
        }

        public ClientStopwatch()
        {
            _timestamp = GetTimestamp();
        }

        public override void Stop()
        {
            _haltedAt = GetTimestamp();
        }

        public override void Start()
        {
            EnsureReduction();

            _haltedAt = 0;
        }

        private void EnsureReduction()
        {
            if (_haltedAt != 0)
            {
                _reduction += GetTimestamp() - _haltedAt;
            }
        }

        internal static long GetTimestamp()
        {
            return (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
        }
    }
}