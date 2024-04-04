namespace FxEvents.Shared.Snowflakes
{
    public class SnowflakeConfiguration
    {
        public byte TimestampBits { get; set; }
        public byte InstanceBits { get; set; }
        public byte SequenceBits { get; set; }

        public SnowflakeConfiguration(byte timestampBits, byte instanceBits, byte sequenceBits)
        {
            TimestampBits = timestampBits;
            InstanceBits = instanceBits;
            SequenceBits = sequenceBits;
        }

        public SnowflakeConfiguration() : this(42, 10, 12)
        {
        }
    }
}