﻿using FxEvents.Shared.EventSubsystem;
using FxEvents.Shared.Snowflakes;
using System;
using System.IO;

namespace FxEvents.Shared.Message
{
    public class EventResponseMessage : IMessage
    {
        public Snowflake Id { get; set; }
        public string Endpoint { get; set; }
        public byte[]? Data { get; set; }

        public EventResponseMessage() { }
        public EventResponseMessage(Snowflake id, string endpoint, byte[]? data)
        {
            Id = id;
            Endpoint = endpoint;
            Data = data;
        }

        public override string ToString() => Id.ToString();
    }
}