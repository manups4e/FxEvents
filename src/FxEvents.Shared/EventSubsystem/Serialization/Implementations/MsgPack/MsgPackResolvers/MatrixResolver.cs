using FxEvents.Shared.TypeExtensions;
using MsgPack;
using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace FxEvents.Shared.EventSubsystem.Serialization.Implementations.MsgPackResolvers
{
    internal class MatrixResolver : MessagePackSerializer<Matrix>
    {
        public MatrixResolver(SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Matrix objectTree)
        {
            packer.PackArray(objectTree.ToArray());
        }

        protected override Matrix UnpackFromCore(Unpacker unpacker)
        {
            float[] values = new float[16];
            for (int i = 0; i < 16; i++)
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
                        throw new Exception($"FxEvents Matrix - Cannot deserialize {data.UnderlyingType.FullName} into {typeof(float).FullName}");
                    if (unpacker.IsArrayHeader)
                        throw new Exception($"FxEvents Matrix - Cannot deserialize {data.UnderlyingType.FullName}[] array into {typeof(float).FullName}");

                    float.TryParse(data.ToObject().ToString(), out item);
                }
                values[i] = item;
            }
            return new Matrix(values);

        }

    }
}
