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

        protected override Entity UnpackFromCore(Unpacker unpacker)
        {
            var data = unpacker.LastReadData;
            if (!TypeCache.IsSimpleType(data.UnderlyingType) || unpacker.IsMapHeader)
                throw new Exception($"FxEvents Entity - Cannot deserialize {data.UnderlyingType.FullName} into {typeof(int).FullName}");
            if (unpacker.IsArrayHeader)
                throw new Exception($"FxEvents Entity - Cannot deserialize {data.UnderlyingType.FullName}[] array into {typeof(int).FullName}");

            int.TryParse(data.ToObject().ToString(), out int item);
            return Entity.FromNetworkId(item);
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
            if (!TypeCache.IsSimpleType(data.UnderlyingType) || unpacker.IsMapHeader)
                throw new Exception($"FxEvents Ped - Cannot deserialize {data.UnderlyingType.FullName} into {typeof(int).FullName}");
            if (unpacker.IsArrayHeader)
                throw new Exception($"FxEvents Ped - Cannot deserialize {data.UnderlyingType.FullName}[] array into {typeof(int).FullName}");

            int.TryParse(data.ToObject().ToString(), out int item);
            return (Ped)Entity.FromNetworkId(item);
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
            if (!TypeCache.IsSimpleType(data.UnderlyingType) || unpacker.IsMapHeader)
                throw new Exception($"FxEvents Vehicle - Cannot deserialize {data.UnderlyingType.FullName} into {typeof(int).FullName}");
            if (unpacker.IsArrayHeader)
                throw new Exception($"FxEvents Vehicle - Cannot deserialize {data.UnderlyingType.FullName}[] array into {typeof(int).FullName}");

            int.TryParse(data.ToObject().ToString(), out int item);
            return (Vehicle)Entity.FromNetworkId(item);
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
            if (!TypeCache.IsSimpleType(data.UnderlyingType) || unpacker.IsMapHeader)
                throw new Exception($"FxEvents Prop - Cannot deserialize {data.UnderlyingType.FullName} into {typeof(int).FullName}");
            if (unpacker.IsArrayHeader)
                throw new Exception($"FxEvents Prop - Cannot deserialize {data.UnderlyingType.FullName}[] array into {typeof(int).FullName}");

            int.TryParse(data.ToObject().ToString(), out int item);
            return (Prop)Entity.FromNetworkId(item);
        }
    }    
}
