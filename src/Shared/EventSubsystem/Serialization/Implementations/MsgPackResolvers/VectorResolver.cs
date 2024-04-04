using MsgPack;
using MsgPack.Serialization;

namespace FxEvents.Shared.Serialization.Implementations.MsgPackResolvers
{
    public class Vector2Resolver : MessagePackSerializer<Vector2>
    {

        private readonly MessagePackSerializer<float> _itemSerializer;

        public Vector2Resolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
            this._itemSerializer = ownerContext.GetSerializer<float>();
        }

        protected override void PackToCore(Packer packer, Vector2 objectTree)
        {
            packer.PackArrayHeader(2);
            _itemSerializer.PackTo(packer, objectTree.X);
            _itemSerializer.PackTo(packer, objectTree.Y);
        }


        protected override Vector2 UnpackFromCore(Unpacker unpacker)
        {
            float[] values = new float[2];
            for (int i = 0; i < 2; i++)
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
            return new Vector2(values[0], values[1]);
        }

    }

    public class Vector3Resolver : MessagePackSerializer<Vector3>
    {
        private readonly MessagePackSerializer<float> _itemSerializer;
        public Vector3Resolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
            this._itemSerializer = ownerContext.GetSerializer<float>();
        }

        protected override void PackToCore(Packer packer, Vector3 objectTree)
        {
            packer.PackArrayHeader(3);
            _itemSerializer.PackTo(packer, objectTree.X);
            _itemSerializer.PackTo(packer, objectTree.Y);
            _itemSerializer.PackTo(packer, objectTree.Z);
        }


        protected override Vector3 UnpackFromCore(Unpacker unpacker)
        {
            float[] values = new float[3];
            for (int i = 0; i < 3; i++)
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
            return new Vector3(values[0], values[1], values[2]);
        }

    }
    public class Vector4Resolver : MessagePackSerializer<Vector4>
    {
        private readonly MessagePackSerializer<float> _itemSerializer;
        public Vector4Resolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
            this._itemSerializer = ownerContext.GetSerializer<float>();
        }

        protected override void PackToCore(Packer packer, Vector4 objectTree)
        {
            packer.PackArrayHeader(4);
            _itemSerializer.PackTo(packer, objectTree.X);
            _itemSerializer.PackTo(packer, objectTree.Y);
            _itemSerializer.PackTo(packer, objectTree.Z);
            _itemSerializer.PackTo(packer, objectTree.W);
        }


        protected override Vector4 UnpackFromCore(Unpacker unpacker)
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
            return new Vector4(values[0], values[1], values[2], values[3]);
        }

    }
}