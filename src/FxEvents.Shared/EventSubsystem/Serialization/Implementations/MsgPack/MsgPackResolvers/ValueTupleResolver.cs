//using Logger;
//using MsgPack;
//using MsgPack.Serialization;
//using System;

//namespace FxEvents.Shared.Serialization.Implementations.MsgPackResolvers
//{
//    [Obsolete("Ignored by messagepack apparently due to its non generic behaviour, kept for reference and other uses")]
//    public class ValueTupleResolver<A> : MessagePackSerializer<ValueTuple<A>>
//    {
//        public ValueTupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
//        {
//        }

//        protected override void PackToCore(Packer packer, ValueTuple<A> objectTree)
//        {
//            packer.Pack(new object[] { objectTree.Item1 });
//        }

//        protected override ValueTuple<A> UnpackFromCore(Unpacker unpacker)
//        {
//            object[] obj = (object[])unpacker.LastReadData.ToObject();
//            return new ValueTuple<A>((A)obj[0]);
//        }
//    }

//    [Obsolete("Ignored by messagepack apparently due to its non generic behaviour, kept for reference and other uses")]
//    public class ValueTupleResolver<A, B> : MessagePackSerializer<ValueTuple<A, B>>
//    {
//        Log logger = new Log();
//        public ValueTupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
//        {

//        }

//        protected override void PackToCore(Packer packer, ValueTuple<A, B> objectTree)
//        {
//            logger.Info("sono chiamato");
//            packer.Pack(new object[] { objectTree.Item1, objectTree.Item2 });
//        }

//        protected override ValueTuple<A, B> UnpackFromCore(Unpacker unpacker)
//        {
//            object[] obj = (object[])unpacker.LastReadData.ToObject();
//            return new ValueTuple<A, B>((A)obj[0], (B)obj[1]);
//        }
//    }

//    [Obsolete("Ignored by messagepack apparently due to its non generic behaviour, kept for reference and other uses")]
//    public class ValueTupleResolver<A, B, C> : MessagePackSerializer<ValueTuple<A, B, C>>
//    {
//        public ValueTupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
//        {
//        }

//        protected override void PackToCore(Packer packer, ValueTuple<A, B, C> objectTree)
//        {
//            packer.Pack(new object[] { objectTree.Item1, objectTree.Item2, objectTree.Item3 });
//        }

//        protected override ValueTuple<A, B, C> UnpackFromCore(Unpacker unpacker)
//        {
//            object[] obj = (object[])unpacker.LastReadData.ToObject();
//            return new ValueTuple<A, B, C>((A)obj[0], (B)obj[1], (C)obj[2]);
//        }
//    }

//    [Obsolete("Ignored by messagepack apparently due to its non generic behaviour, kept for reference and other uses")]
//    public class ValueTupleResolver<A, B, C, D> : MessagePackSerializer<ValueTuple<A, B, C, D>>
//    {
//        public ValueTupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
//        {
//        }

//        protected override void PackToCore(Packer packer, ValueTuple<A, B, C, D> objectTree)
//        {
//            packer.Pack(new object[] { objectTree.Item1, objectTree.Item2, objectTree.Item3, objectTree.Item4 });
//        }

//        protected override ValueTuple<A, B, C, D> UnpackFromCore(Unpacker unpacker)
//        {
//            object[] obj = (object[])unpacker.LastReadData.ToObject();
//            return new ValueTuple<A, B, C, D>((A)obj[0], (B)obj[1], (C)obj[2], (D)obj[3]);
//        }
//    }

//    [Obsolete("Ignored by messagepack apparently due to its non generic behaviour, kept for reference and other uses")]
//    public class ValueTupleResolver<A, B, C, D, E> : MessagePackSerializer<ValueTuple<A, B, C, D, E>>
//    {
//        public ValueTupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
//        {
//        }

//        protected override void PackToCore(Packer packer, ValueTuple<A, B, C, D, E> objectTree)
//        {
//            packer.Pack(new object[] { objectTree.Item1, objectTree.Item2, objectTree.Item3, objectTree.Item4, objectTree.Item5 });
//        }

//        protected override ValueTuple<A, B, C, D, E> UnpackFromCore(Unpacker unpacker)
//        {
//            object[] obj = (object[])unpacker.LastReadData.ToObject();
//            return new ValueTuple<A, B, C, D, E>((A)obj[0], (B)obj[1], (C)obj[2], (D)obj[3], (E)obj[4]);
//        }
//    }

//    [Obsolete("Ignored by messagepack apparently due to its non generic behaviour, kept for reference and other uses")]
//    public class ValueTupleResolver<A, B, C, D, E, F> : MessagePackSerializer<ValueTuple<A, B, C, D, E, F>>
//    {
//        public ValueTupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
//        {
//        }

//        protected override void PackToCore(Packer packer, ValueTuple<A, B, C, D, E, F> objectTree)
//        {
//            packer.Pack(new object[] { objectTree.Item1, objectTree.Item2, objectTree.Item3, objectTree.Item4, objectTree.Item5, objectTree.Item6 });
//        }

//        protected override ValueTuple<A, B, C, D, E, F> UnpackFromCore(Unpacker unpacker)
//        {
//            object[] obj = (object[])unpacker.LastReadData.ToObject();
//            return new ValueTuple<A, B, C, D, E, F>((A)obj[0], (B)obj[1], (C)obj[2], (D)obj[3], (E)obj[4], (F)obj[5]);
//        }
//    }

//    [Obsolete("Ignored by messagepack apparently due to its non generic behaviour, kept for reference and other uses")]
//    public class ValueTupleResolver<A, B, C, D, E, F, G> : MessagePackSerializer<ValueTuple<A, B, C, D, E, F, G>>
//    {
//        public ValueTupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
//        {
//        }

//        protected override void PackToCore(Packer packer, ValueTuple<A, B, C, D, E, F, G> objectTree)
//        {
//            packer.Pack(new object[] { objectTree.Item1, objectTree.Item2, objectTree.Item3, objectTree.Item4, objectTree.Item5, objectTree.Item6, objectTree.Item7 });
//        }

//        protected override ValueTuple<A, B, C, D, E, F, G> UnpackFromCore(Unpacker unpacker)
//        {
//            object[] obj = (object[])unpacker.LastReadData.ToObject();
//            return new ValueTuple<A, B, C, D, E, F, G>((A)obj[0], (B)obj[1], (C)obj[2], (D)obj[3], (E)obj[4], (F)obj[5], (G)obj[6]);
//        }
//    }
//}