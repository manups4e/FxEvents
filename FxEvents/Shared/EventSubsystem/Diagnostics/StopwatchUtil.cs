using FxEvents.Shared.Diagnostics.Impl;
using System;
using Natives = CitizenFX.Shared.Native.Natives;

namespace FxEvents.Shared.Diagnostics
{
    public abstract class StopwatchUtil
    {
        private static readonly bool IsServer = Natives.IsDuplicityVersion();

        public abstract TimeSpan Elapsed { get; }
        public abstract void Stop();
        public abstract void Start();


        public static long Timestamp
        {
            get
            {
                if (IsServer)
                {
                    return ServerStopwatch.GetTimestamp();
                }
                else
                {
                    return ClientStopwatch.GetTimestamp();
                }
            }
        }

        public static StopwatchUtil StartNew()
        {
            if (IsServer)
            {
                return new ServerStopwatch();
            }

            return new ClientStopwatch();
        }
    }
}