using MsgPack;
using MsgPack.Serialization;

namespace FxEvents.Shared.Serialization.Implementations.MsgPackResolvers
{
    public class QuaternionResolver : MessagePackSerializer<Quaternion>
    {
        private readonly MessagePackSerializer<float> _itemSerializer;
        public QuaternionResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
            this._itemSerializer = ownerContext.GetSerializer<float>();
        }

        protected override void PackToCore(Packer packer, Quaternion objectTree)
        {
            packer.PackArrayHeader(4);
            _itemSerializer.PackTo(packer, objectTree.X);
            _itemSerializer.PackTo(packer, objectTree.Y);
            _itemSerializer.PackTo(packer, objectTree.Z);
            _itemSerializer.PackTo(packer, objectTree.W);
        }

        protected override Quaternion UnpackFromCore(Unpacker unpacker)
        {
            float[] values = new float[4];
            for (int i = 0; i < 4; i++)
            {
                if (!unpacker.Read())
                {
                    throw SerializationExceptions.NewMissingItem(i);
                }

                float item;
                if (!unpacker.IsArrayHeader && !unpacker.IsMapHeader)
                {
                    item = this._itemSerializer.UnpackFrom(unpacker);
                }
                else
                {
                    using (Unpacker subtreeUnpacker = unpacker.ReadSubtree())
                    {
                        item = this._itemSerializer.UnpackFrom(subtreeUnpacker);
                    }
                }

                values[i] = item;
            }
            return new Quaternion(values[0], values[1], values[2], values[3]);
        }

    }
}