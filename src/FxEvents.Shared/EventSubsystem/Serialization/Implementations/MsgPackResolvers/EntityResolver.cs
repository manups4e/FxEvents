using MsgPack;
using MsgPack.Serialization;

namespace FxEvents.Shared.EventSubsystem.Serialization.Implementations.MsgPackResolvers
{
    /// <summary>
    /// For this one and all its derivates.. we use NetworkID to keep consistency between each side.
    /// </summary>
    public class EntityResolver : MessagePackSerializer<Entity>
    {
        public EntityResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Entity objectTree)
        {
            packer.Pack(objectTree.NetworkId);
        }

        protected override Entity UnpackFromCore(Unpacker unpacker)
        {
            return Entity.FromNetworkId(unpacker.LastReadData.AsInt32());
        }
    }

    public class PedResolver : MessagePackSerializer<Ped>
    {
        public PedResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Ped objectTree)
        {
            packer.Pack(objectTree.NetworkId);
        }

        protected override Ped UnpackFromCore(Unpacker unpacker)
        {
            return (Ped)Entity.FromNetworkId(unpacker.LastReadData.AsInt32());
        }
    }

    public class VehicleResolver : MessagePackSerializer<Vehicle>
    {
        public VehicleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Vehicle objectTree)
        {
            packer.Pack(objectTree.NetworkId);
        }

        protected override Vehicle UnpackFromCore(Unpacker unpacker)
        {
            return (Vehicle)Entity.FromNetworkId(unpacker.LastReadData.AsInt32());
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
            return (Prop)Entity.FromNetworkId(unpacker.LastReadData.AsInt32());
        }
    }    
}
