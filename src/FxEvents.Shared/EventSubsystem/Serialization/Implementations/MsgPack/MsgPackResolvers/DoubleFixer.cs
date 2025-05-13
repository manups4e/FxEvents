using FxEvents.Shared.TypeExtensions;
using MsgPack;
using MsgPack.Serialization;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;

namespace FxEvents.Shared.EventSubsystem.Serialization.Implementations.MsgPack.MsgPackResolvers
{
    public class DoubleFixer : MessagePackSerializer<double>
    {
        public DoubleFixer(SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, double objectTree)
        {
            packer.Pack(objectTree.ToString("G17", CultureInfo.InvariantCulture));
        }

        protected override double UnpackFromCore(Unpacker unpacker)
        {
            var data = unpacker.LastReadData;
            if (!TypeCache.IsSimpleType(data.UnderlyingType) || unpacker.IsMapHeader)
                throw new Exception($"FxEvents double - Cannot deserialize {data.UnderlyingType.FullName} into {typeof(int).FullName}");
            if (unpacker.IsArrayHeader)
                throw new Exception($"FxEvents double - Cannot deserialize {data.UnderlyingType.FullName}[] array into {typeof(int).FullName}");

            return double.Parse(data.AsString(), CultureInfo.InvariantCulture);

        }
    }

}
