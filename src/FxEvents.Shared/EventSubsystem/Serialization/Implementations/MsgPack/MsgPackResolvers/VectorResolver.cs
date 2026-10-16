using FxEvents.Shared.TypeExtensions;
using Logger;
using MsgPack;
using MsgPack.Serialization;
using System;
using System.Globalization;
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
            packer.PackArrayHeader(2);
            packer.Pack(objectTree.X);
            packer.Pack(objectTree.Y);
        }

        protected override Vector2 UnpackFromCore(Unpacker unpacker)
        {
            float x = 0, y = 0;
            for (int i = 0; i < 2; i++)
            {
                if (!unpacker.Read()) break;
                var data = unpacker.LastReadData;
                float item = data.UnderlyingType == typeof(string)
                    ? (float.TryParse(data.AsString(), NumberStyles.Any, CultureInfo.InvariantCulture, out float p) ? p : 0f)
                    : data.AsSingle();

                if (i == 0) x = item;
                else if (i == 1) y = item;
            }
            return new Vector2(x, y);
        }
    }

    public class Vector3Resolver : MessagePackSerializer<Vector3>
    {
        public Vector3Resolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Vector3 objectTree)
        {
            packer.PackArrayHeader(3);
            packer.Pack(objectTree.X);
            packer.Pack(objectTree.Y);
            packer.Pack(objectTree.Z);
        }

        protected override Vector3 UnpackFromCore(Unpacker unpacker)
        {
            float x = 0, y = 0, z = 0;
            for (int i = 0; i < 3; i++)
            {
                if (!unpacker.Read()) break;
                var data = unpacker.LastReadData;
                float item = data.UnderlyingType == typeof(string)
                    ? (float.TryParse(data.AsString(), NumberStyles.Any, CultureInfo.InvariantCulture, out float p) ? p : 0f)
                    : data.AsSingle();

                if (i == 0) x = item;
                else if (i == 1) y = item;
                else if (i == 2) z = item;
            }
            return new Vector3(x, y, z);
        }
    }

    public class Vector4Resolver : MessagePackSerializer<Vector4>
    {
        public Vector4Resolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Vector4 objectTree)
        {
            packer.PackArrayHeader(4);
            packer.Pack(objectTree.X);
            packer.Pack(objectTree.Y);
            packer.Pack(objectTree.Z);
            packer.Pack(objectTree.W);
        }

        protected override Vector4 UnpackFromCore(Unpacker unpacker)
        {
            float x = 0, y = 0, z = 0, w = 0;
            for (int i = 0; i < 4; i++)
            {
                if (!unpacker.Read()) break;
                var data = unpacker.LastReadData;
                float item = data.UnderlyingType == typeof(string)
                    ? (float.TryParse(data.AsString(), NumberStyles.Any, CultureInfo.InvariantCulture, out float p) ? p : 0f)
                    : data.AsSingle();

                if (i == 0) x = item;
                else if (i == 1) y = item;
                else if (i == 2) z = item;
                else if (i == 3) w = item;
            }
            return new Vector4(x, y, z, w);
        }
    }
}