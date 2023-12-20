using System;
using System.IO;

namespace FxEvents.Shared.Snowflakes
{

    public struct Snowflake : IEquatable<Snowflake>
    {
        public static readonly Snowflake Empty = new Snowflake(0);

        private ulong _value;
        public ulong Value { get => _value; set => _value = value; }

        public Snowflake() { _value = 0; }
        public static Snowflake Next()
        {
            return SnowflakeGenerator.Instance.Next();
        }

        public ulong ToInt64() => _value;

        public static Snowflake Parse(string id) => Parse(ulong.Parse(id));

        public static Snowflake Parse(ulong id)
        {
            return new Snowflake(id);
        }

        public SnowflakeFragments Deconstruct()
        {
            SnowflakeGenerator instance = SnowflakeGenerator.Instance;

            return instance.Deconstruct((long)_value);
        }

        public Snowflake(ulong value)
        {
            _value = value;
        }

        public Snowflake(long value) : this((ulong)value)
        {
        }

        public Snowflake(string value)
        {
            _value = (ulong)long.Parse(value);
        }

        public Snowflake(BinaryReader reader)
        {
            _value = reader.ReadUInt64();
        }

        public void PackSerializedBytes(BinaryWriter writer)
        {
            writer.Write(_value);
        }

        public override string ToString()
        {
            return _value.ToString();
        }

        public bool Equals(Snowflake other)
        {
            return _value == other._value;
        }

        public override bool Equals(object obj)
        {
            return obj switch
            {
                ulong unsigned => _value == unsigned,
                long signed => _value == (ulong)signed,
                string serialized => ToString() == serialized,
                _ => obj is Snowflake other && Equals(other)
            };
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public static bool operator ==(Snowflake first, Snowflake second)
        {
            return first.Equals(Empty) ? second.Equals(Empty) : first.Equals(second);
        }

        public static bool operator !=(Snowflake first, Snowflake second)
        {
            return !(first == second);
        }
    }
}