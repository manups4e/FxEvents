using MsgPack;
using MsgPack.Serialization;
using System;

namespace FxEvents.Shared.Serialization.Implementations.MsgPackResolvers
{
    public class TupleResolver<T1> : MessagePackSerializer<Tuple<T1>>
    {
        public TupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Tuple<T1> objectTree)
        {
            packer.Pack(new object[] { objectTree.Item1 });
        }

        protected override Tuple<T1> UnpackFromCore(Unpacker unpacker)
        {
            object[] obj = (object[])unpacker.LastReadData.ToObject();
            return new Tuple<T1>((T1)obj[0]);
        }
    }

    public class TupleResolver<T1, T2> : MessagePackSerializer<Tuple<T1, T2>>
    {
        public TupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {

        }

        protected override void PackToCore(Packer packer, Tuple<T1, T2> objectTree)
        {
            packer.Pack(new object[] { objectTree.Item1, objectTree.Item2 });
        }

        protected override Tuple<T1, T2> UnpackFromCore(Unpacker unpacker)
        {
            object[] obj = (object[])unpacker.LastReadData.ToObject();
            return new Tuple<T1, T2>((T1)obj[0], (T2)obj[1]);
        }
    }

    public class TupleResolver<T1, T2, T3> : MessagePackSerializer<Tuple<T1, T2, T3>>
    {
        public TupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Tuple<T1, T2, T3> objectTree)
        {
            packer.Pack(new object[] { objectTree.Item1, objectTree.Item2, objectTree.Item3 });
        }

        protected override Tuple<T1, T2, T3> UnpackFromCore(Unpacker unpacker)
        {
            object[] obj = (object[])unpacker.LastReadData.ToObject();
            return new Tuple<T1, T2, T3>((T1)obj[0], (T2)obj[1], (T3)obj[2]);
        }
    }

    public class TupleResolver<T1, T2, T3, T4> : MessagePackSerializer<Tuple<T1, T2, T3, T4>>
    {
        public TupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Tuple<T1, T2, T3, T4> objectTree)
        {
            packer.Pack(new object[] { objectTree.Item1, objectTree.Item2, objectTree.Item3, objectTree.Item4 });
        }

        protected override Tuple<T1, T2, T3, T4> UnpackFromCore(Unpacker unpacker)
        {
            object[] obj = (object[])unpacker.LastReadData.ToObject();
            return new Tuple<T1, T2, T3, T4>((T1)obj[0], (T2)obj[1], (T3)obj[2], (T4)obj[3]);
        }
    }

    public class TupleResolver<T1, T2, T3, T4, T5> : MessagePackSerializer<Tuple<T1, T2, T3, T4, T5>>
    {
        public TupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Tuple<T1, T2, T3, T4, T5> objectTree)
        {
            packer.Pack(new object[] { objectTree.Item1, objectTree.Item2, objectTree.Item3, objectTree.Item4, objectTree.Item5 });
        }

        protected override Tuple<T1, T2, T3, T4, T5> UnpackFromCore(Unpacker unpacker)
        {
            object[] obj = (object[])unpacker.LastReadData.ToObject();
            return new Tuple<T1, T2, T3, T4, T5>((T1)obj[0], (T2)obj[1], (T3)obj[2], (T4)obj[3], (T5)obj[4]);
        }
    }

    public class TupleResolver<T1, T2, T3, T4, T5, T6> : MessagePackSerializer<Tuple<T1, T2, T3, T4, T5, T6>>
    {
        public TupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Tuple<T1, T2, T3, T4, T5, T6> objectTree)
        {
            packer.Pack(new object[] { objectTree.Item1, objectTree.Item2, objectTree.Item3, objectTree.Item4, objectTree.Item5, objectTree.Item6 });
        }

        protected override Tuple<T1, T2, T3, T4, T5, T6> UnpackFromCore(Unpacker unpacker)
        {
            object[] obj = (object[])unpacker.LastReadData.ToObject();
            return new Tuple<T1, T2, T3, T4, T5, T6>((T1)obj[0], (T2)obj[1], (T3)obj[2], (T4)obj[3], (T5)obj[4], (T6)obj[5]);
        }
    }

    public class TupleResolver<T1, T2, T3, T4, T5, T6, T7> : MessagePackSerializer<Tuple<T1, T2, T3, T4, T5, T6, T7>>
    {
        public TupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Tuple<T1, T2, T3, T4, T5, T6, T7> objectTree)
        {
            packer.Pack(new object[] { objectTree.Item1, objectTree.Item2, objectTree.Item3, objectTree.Item4, objectTree.Item5, objectTree.Item6, objectTree.Item7 });
        }

        protected override Tuple<T1, T2, T3, T4, T5, T6, T7> UnpackFromCore(Unpacker unpacker)
        {
            object[] obj = (object[])unpacker.LastReadData.ToObject();
            return new Tuple<T1, T2, T3, T4, T5, T6, T7>((T1)obj[0], (T2)obj[1], (T3)obj[2], (T4)obj[3], (T5)obj[4], (T6)obj[5], (T7)obj[6]);
        }
    }
}