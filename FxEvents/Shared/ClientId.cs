using System;
using System.Linq;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using Newtonsoft.Json;
using FxEvents.Shared.Attributes;
using System.IO;
using FxEvents.Shared;
using FxEvents.Shared.Snowflakes;
using FxEvents.Shared.EventSubsystem;

namespace FxEvents.Shared
{
    [Serialization]
    public partial class ClientId : ISource
    {
        public int Handle { get; set; }

        public ClientId(int handle)
        {
            Handle = handle;
        }

        public static explicit operator ClientId(int handle) => new(handle);
        public static explicit operator ClientId(string netId)
        {
            if (int.TryParse(netId.Replace("net:", string.Empty), out int handle))
            {
                return new ClientId(handle);
            }

            throw new Exception($"Could not parse net id: {netId}");
        }

    }
}