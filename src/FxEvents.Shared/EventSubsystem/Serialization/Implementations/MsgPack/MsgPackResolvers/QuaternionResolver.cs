using FxEvents.Shared.TypeExtensions;
using MsgPack;
using MsgPack.Serialization;
using System;

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
            packer.PackArray(objectTree.ToArray());
        }

        protected override Quaternion UnpackFromCore(Unpacker unpacker)
        {
            float[] values = new float[4];
            for (int i = 0; i < 4; i++)
            {
                float item;
                if (!unpacker.Read())
                {
                    item = 0;
                }
                else
                {
                    var data = unpacker.LastReadData;
                    if (!TypeCache.IsSimpleType(data.UnderlyingType) || unpacker.IsMapHeader)
                        throw new Exception($"Cannot deserialize type {data.UnderlyingType.Name} into Quaternion float parameter type");
                    if(unpacker.IsArrayHeader)
                        throw new Exception($"Cannot deserialize type array {data.UnderlyingType.Name}[] into Quaternion float parameter type");                    

                    float.TryParse(data.ToObject().ToString(), out item);
                }
                values[i] = item;
            }
            return new Quaternion(values);
        }

    }
}