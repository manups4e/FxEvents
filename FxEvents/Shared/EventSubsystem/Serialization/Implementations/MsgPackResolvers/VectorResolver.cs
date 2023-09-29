using MsgPack;
using MsgPack.Serialization;

namespace FxEvents.Shared.Serialization.Implementations.MsgPackResolvers
{
    public class Vector2Resolver : MessagePackSerializer<Vector2>
    {
        public Vector2Resolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Vector2 objectTree)
        {
            packer.Pack(objectTree.ToArray());
        }


        protected override Vector2 UnpackFromCore(Unpacker unpacker)
        {
            return new Vector2(unpacker.Unpack<float[]>(OwnerContext));
        }

    }

    public class Vector3Resolver : MessagePackSerializer<Vector3>
    {
        public Vector3Resolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Vector3 objectTree)
        {
            packer.Pack(objectTree.ToArray());
        }


        protected override Vector3 UnpackFromCore(Unpacker unpacker)
        {
            return new Vector3(unpacker.Unpack<float[]>(OwnerContext));
        }

    }
    public class Vector4Resolver : MessagePackSerializer<Vector4>
    {
        public Vector4Resolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Vector4 objectTree)
        {
            packer.Pack(objectTree.ToArray());
        }


        protected override Vector4 UnpackFromCore(Unpacker unpacker)
        {
            return new Vector4(unpacker.Unpack<float[]>(OwnerContext));
        }

    }
}