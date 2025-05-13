using FxEvents.Shared.TypeExtensions;
using MsgPack;
using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace FxEvents.Shared.EventSubsystem.Serialization.Implementations.MsgPackResolvers
{
    internal class Matrix3x3Resolver : MessagePackSerializer<Matrix3x3>
    {
        public Matrix3x3Resolver(SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Matrix3x3 objectTree)
        {
            packer.PackArray(objectTree.ToArray());
        }

        protected override Matrix3x3 UnpackFromCore(Unpacker unpacker)
        {
            float[] values = new float[9];
            for (int i = 0; i < 9; i++)
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
                        throw new Exception($"FxEvents Matrix3x3 - Cannot deserialize {data.UnderlyingType.FullName} into {typeof(float).FullName}");
                    if (unpacker.IsArrayHeader)
                        throw new Exception($"FxEvents Matrix3x3 - Cannot deserialize {data.UnderlyingType.FullName}[] array into {typeof(float).FullName}");

                    float.TryParse(data.ToObject().ToString(), out item);
                }
                values[i] = item;
            }
            return new Matrix3x3(values);

        }

    }
}
