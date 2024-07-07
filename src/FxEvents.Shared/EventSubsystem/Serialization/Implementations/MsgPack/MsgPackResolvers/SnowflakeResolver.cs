using FxEvents.Shared.Snowflakes;
using FxEvents.Shared.TypeExtensions;
using MsgPack;
using MsgPack.Serialization;
using System;

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
            var data = unpacker.LastReadData;
            if (!TypeCache.IsSimpleType(data.UnderlyingType) || unpacker.IsMapHeader)
                throw new Exception($"FxEvents Snowflake - Cannot deserialize {data.UnderlyingType.FullName} into {typeof(ulong).FullName}");
            if (unpacker.IsArrayHeader)
                throw new Exception($"FxEvents Snowflake - Cannot deserialize {data.UnderlyingType.FullName}[] array into {typeof(ulong).FullName}");

            ulong.TryParse(data.ToObject().ToString(), out ulong item);

            return new Snowflake(item);
        }

    }
}