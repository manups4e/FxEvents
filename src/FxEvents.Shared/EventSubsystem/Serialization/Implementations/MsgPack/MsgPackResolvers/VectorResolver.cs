using FxEvents.Shared.TypeExtensions;
using Logger;
using MsgPack;
using MsgPack.Serialization;
using System;
using System.Linq;

namespace FxEvents.Shared.Serialization.Implementations.MsgPackResolvers
{
    public class Vector2Resolver : MessagePackSerializer<Vector2>
    {
        public Vector2Resolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Vector2 objectTree)
        {
            packer.PackArray(objectTree.ToArray());
        }


        protected override Vector2 UnpackFromCore(Unpacker unpacker)
        {
            float[] values = new float[2];
            for (int i = 0; i < 2; i++)
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
                        throw new Exception($"FxEvents Vector2 - Cannot deserialize {data.UnderlyingType.FullName} into {typeof(float).FullName}");
                    if (unpacker.IsArrayHeader)
                        throw new Exception($"FxEvents Vector2 - Cannot deserialize {data.UnderlyingType.FullName}[] array into {typeof(float).FullName}");

                    float.TryParse(data.ToObject().ToString(), out item);
                }
                values[i] = item;
            }
            return new Vector2(values);
        }

    }

    public class Vector3Resolver : MessagePackSerializer<Vector3>
    {
        public Vector3Resolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Vector3 objectTree)
        {
            packer.PackArray(objectTree.ToArray());
        }


        protected override Vector3 UnpackFromCore(Unpacker unpacker)
        {
            float[] values = new float[3];
            for (int i = 0; i < 3; i++)
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
                        throw new Exception($"FxEvents Vector3 - Cannot deserialize {data.UnderlyingType.FullName} into {typeof(float).FullName}");
                    if (unpacker.IsArrayHeader)
                        throw new Exception($"FxEvents Vector3 - Cannot deserialize {data.UnderlyingType.FullName}[] array into {typeof(float).FullName}");

                    float.TryParse(data.ToObject().ToString(), out item);
                }
                values[i] = item;
            }
            return new Vector3(values);
        }

    }
    public class Vector4Resolver : MessagePackSerializer<Vector4>
    {
        public Vector4Resolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Vector4 objectTree)
        {
            packer.PackArray(objectTree.ToArray());
        }


        protected override Vector4 UnpackFromCore(Unpacker unpacker)
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
                        throw new Exception($"FxEvents Vector4 - Cannot deserialize {data.UnderlyingType.FullName} into {typeof(float).FullName}");
                    if (unpacker.IsArrayHeader)
                        throw new Exception($"FxEvents Vector4 - Cannot deserialize {data.UnderlyingType.FullName}[] array into {typeof(float).FullName}");

                    float.TryParse(data.ToObject().ToString(), out item);
                }
                values[i] = item;
            }
            return new Vector4(values);
        }

    }
}