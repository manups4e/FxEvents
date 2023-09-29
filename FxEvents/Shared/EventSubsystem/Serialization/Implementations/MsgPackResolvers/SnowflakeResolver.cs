using FxEvents.Shared.Snowflakes;
using MsgPack;
using MsgPack.Serialization;

namespace FxEvents.Shared.Serialization.Implementations.MsgPackResolvers
{
    public class SnowflakeResolver : MessagePackSerializer<Snowflake>
    {
        public SnowflakeResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Snowflake objectTree)
        {
            packer.Pack(objectTree.ToInt64());
        }

        protected override Snowflake UnpackFromCore(Unpacker unpacker)
        {
            return new Snowflake(unpacker.LastReadData.AsUInt64());
        }

    }
}