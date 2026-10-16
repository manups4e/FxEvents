using FxEvents.Shared.TypeExtensions;
using MsgPack;
using MsgPack.Serialization;
using System;

namespace FxEvents.Shared.EventSubsystem.Serialization.Implementations.MsgPackResolvers
{
    /// <summary>
    /// For this one and all its derivates.. we use NetworkID to keep consistency between each side.
    /// </summary>
    public class EntityResolver : MessagePackSerializer<Entity>
    {
        public EntityResolver(SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Entity objectTree)
        {
            packer.Pack(objectTree.NetworkId);
        }

        private static int ReadNetworkId(MessagePackObject data)
        {
            return data.UnderlyingType == typeof(string)
                ? (int.TryParse(data.AsString(), out int parsed) ? parsed : 0)
                : data.AsInt32();
        }

        protected override Entity UnpackFromCore(Unpacker unpacker)
        {
            var data = unpacker.LastReadData;
            if (unpacker.IsMapHeader || unpacker.IsArrayHeader)
                throw new Exception($"FxEvents Entity - Cannot deserialize complex structure into Entity NetworkId");

            return Entity.FromNetworkId(ReadNetworkId(data));
        }
    }

    public class PedResolver : MessagePackSerializer<Ped>
    {
        public PedResolver(SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Ped objectTree)
        {
            packer.Pack(objectTree.NetworkId);
        }

        protected override Ped UnpackFromCore(Unpacker unpacker)
        {
            var data = unpacker.LastReadData;
            if (unpacker.IsMapHeader || unpacker.IsArrayHeader)
                throw new Exception($"FxEvents Ped - Cannot deserialize complex structure into Ped NetworkId");

            return (Ped)Entity.FromNetworkId(data.UnderlyingType == typeof(string) ? (int.TryParse(data.AsString(), out int p) ? p : 0) : data.AsInt32());
        }
    }

    public class VehicleResolver : MessagePackSerializer<Vehicle>
    {
        public VehicleResolver(SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Vehicle objectTree)
        {
            packer.Pack(objectTree.NetworkId);
        }

        protected override Vehicle UnpackFromCore(Unpacker unpacker)
        {
            var data = unpacker.LastReadData;
            if (unpacker.IsMapHeader || unpacker.IsArrayHeader)
                throw new Exception($"FxEvents Vehicle - Cannot deserialize complex structure into Vehicle NetworkId");

            return (Vehicle)Entity.FromNetworkId(data.UnderlyingType == typeof(string) ? (int.TryParse(data.AsString(), out int p) ? p : 0) : data.AsInt32());
        }
    }

    public class PropResolver : MessagePackSerializer<Prop>
    {
        public PropResolver(SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Prop objectTree)
        {
            packer.Pack(objectTree.NetworkId);
        }

        protected override Prop UnpackFromCore(Unpacker unpacker)
        {
            var data = unpacker.LastReadData;
            if (unpacker.IsMapHeader || unpacker.IsArrayHeader)
                throw new Exception($"FxEvents Prop - Cannot deserialize complex structure into Prop NetworkId");

            return (Prop)Entity.FromNetworkId(data.UnderlyingType == typeof(string) ? (int.TryParse(data.AsString(), out int p) ? p : 0) : data.AsInt32());
        }
    }    
}
