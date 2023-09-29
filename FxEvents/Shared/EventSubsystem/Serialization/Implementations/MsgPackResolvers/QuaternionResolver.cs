using MsgPack;
using MsgPack.Serialization;

namespace FxEvents.Shared.Serialization.Implementations.MsgPackResolvers
{
    public class QuaternionResolver : MessagePackSerializer<Quaternion>
    {
        public QuaternionResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Quaternion objectTree)
        {
            packer.Pack(objectTree.ToArray());
        }

        protected override Quaternion UnpackFromCore(Unpacker unpacker)
        {
            return new Quaternion((float[])unpacker.LastReadData.ToObject());
        }

    }
}