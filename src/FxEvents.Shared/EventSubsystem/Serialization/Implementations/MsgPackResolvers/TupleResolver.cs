using FxEvents.Shared.EventSubsystem;
using MsgPack;
using MsgPack.Serialization;
using System;
using System.Runtime.CompilerServices;

namespace FxEvents.Shared.Serialization.Implementations.MsgPackResolvers
{
    [Obsolete("Ignored by messagepack apparently due to its non generic behaviour, kept for reference and other uses")]
    public class TupleResolver<T1> : MessagePackSerializer<Tuple<T1>>
    {
        private readonly MessagePackSerializer<object> _itemSerializer;
        public TupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
            _itemSerializer = ownerContext.GetSerializer<object>();
        }

        protected override void PackToCore(Packer packer, Tuple<T1> objectTree)
        {
            _itemSerializer.PackTo(packer, objectTree.Item1);
        }

        protected override Tuple<T1> UnpackFromCore(Unpacker unpacker)
        {
            if (!unpacker.Read())
            {
                throw SerializationExceptions.NewMissingItem(0);
            }

            MessagePackObject item;
            if (!unpacker.IsArrayHeader && !unpacker.IsMapHeader)
            {
                item = (MessagePackObject)this._itemSerializer.UnpackFrom(unpacker);
            }
            else
            {
                using Unpacker subtreeUnpacker = unpacker.ReadSubtree();
                item = (MessagePackObject)this._itemSerializer.UnpackFrom(subtreeUnpacker);
            }

            return Tuple.Create((T1)BaseGateway.GetHolder(item, typeof(T1)));
        }
    }

    [Obsolete("Ignored by messagepack apparently due to its non generic behaviour, kept for reference and other uses")]
    public class TupleResolver<T1, T2> : MessagePackSerializer<Tuple<T1, T2>>
    {
        private Logger.Log logger = new Logger.Log();
        private readonly MessagePackSerializer<object> _itemSerializer;
        public TupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
            this._itemSerializer = ownerContext.GetSerializer<object>();
        }

        protected override void PackToCore(Packer packer, Tuple<T1, T2> objectTree)
        {
            packer.PackArrayHeader(2);
            _itemSerializer.PackTo(packer, objectTree.Item1);
            _itemSerializer.PackTo(packer, objectTree.Item2);
        }

        protected override Tuple<T1, T2> UnpackFromCore(Unpacker unpacker)
        {
            MessagePackObject[] values = new MessagePackObject[2];
            for (int i = 0; i < 2; i++)
            {
                if (!unpacker.Read())
                {
                    throw SerializationExceptions.NewMissingItem(i);
                }

                MessagePackObject item;
                if (!unpacker.IsArrayHeader && !unpacker.IsMapHeader)
                {
                    item = (MessagePackObject)this._itemSerializer.UnpackFrom(unpacker);
                }
                else
                {
                    using Unpacker subtreeUnpacker = unpacker.ReadSubtree();
                    item = (MessagePackObject)this._itemSerializer.UnpackFrom(subtreeUnpacker);
                }

                values[i] = item;
            }
            return Tuple.Create((T1)BaseGateway.GetHolder(values[0], typeof(T1)), (T2)BaseGateway.GetHolder(values[1], typeof(T2)));
        }
    }

    [Obsolete("Ignored by messagepack apparently due to its non generic behaviour, kept for reference and other uses")]
    public class TupleResolver<T1, T2, T3> : MessagePackSerializer<Tuple<T1, T2, T3>>
    {
        private readonly MessagePackSerializer<object> _itemSerializer;
        public TupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
            _itemSerializer = ownerContext.GetSerializer<object>();
        }

        protected override void PackToCore(Packer packer, Tuple<T1, T2, T3> objectTree)
        {
            packer.PackArrayHeader(3);
            _itemSerializer.PackTo(packer, objectTree.Item1);
            _itemSerializer.PackTo(packer, objectTree.Item2);
            _itemSerializer.PackTo(packer, objectTree.Item3);
        }

        protected override Tuple<T1, T2, T3> UnpackFromCore(Unpacker unpacker)
        {
            MessagePackObject[] values = new MessagePackObject[2];
            for (int i = 0; i < 2; i++)
            {
                if (!unpacker.Read())
                {
                    throw SerializationExceptions.NewMissingItem(i);
                }

                MessagePackObject item;
                if (!unpacker.IsArrayHeader && !unpacker.IsMapHeader)
                {
                    item = (MessagePackObject)this._itemSerializer.UnpackFrom(unpacker);
                }
                else
                {
                    using Unpacker subtreeUnpacker = unpacker.ReadSubtree();
                    item = (MessagePackObject)this._itemSerializer.UnpackFrom(subtreeUnpacker);
                }

                values[i] = item;
            }
            return Tuple.Create((T1)BaseGateway.GetHolder(values[0], typeof(T1)), (T2)BaseGateway.GetHolder(values[1], typeof(T2)), (T3)BaseGateway.GetHolder(values[2], typeof(T3)));
        }
    }

    [Obsolete("Ignored by messagepack apparently due to its non generic behaviour, kept for reference and other uses")]
    public class TupleResolver<T1, T2, T3, T4> : MessagePackSerializer<Tuple<T1, T2, T3, T4>>
    {
        private readonly MessagePackSerializer<object> _itemSerializer;
        public TupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
            _itemSerializer = ownerContext.GetSerializer<object>();
        }

        protected override void PackToCore(Packer packer, Tuple<T1, T2, T3, T4> objectTree)
        {
            packer.PackArrayHeader(4);
            _itemSerializer.PackTo(packer, objectTree.Item1);
            _itemSerializer.PackTo(packer, objectTree.Item2);
            _itemSerializer.PackTo(packer, objectTree.Item3);
            _itemSerializer.PackTo(packer, objectTree.Item4);
        }

        protected override Tuple<T1, T2, T3, T4> UnpackFromCore(Unpacker unpacker)
        {
            MessagePackObject[] values = new MessagePackObject[2];
            for (int i = 0; i < 2; i++)
            {
                if (!unpacker.Read())
                {
                    throw SerializationExceptions.NewMissingItem(i);
                }

                MessagePackObject item;
                if (!unpacker.IsArrayHeader && !unpacker.IsMapHeader)
                {
                    item = (MessagePackObject)this._itemSerializer.UnpackFrom(unpacker);
                }
                else
                {
                    using Unpacker subtreeUnpacker = unpacker.ReadSubtree();
                    item = (MessagePackObject)this._itemSerializer.UnpackFrom(subtreeUnpacker);
                }

                values[i] = item;
            }
            return Tuple.Create((T1)BaseGateway.GetHolder(values[0], typeof(T1)), (T2)BaseGateway.GetHolder(values[1], typeof(T2)), (T3)BaseGateway.GetHolder(values[2], typeof(T3)), (T4)BaseGateway.GetHolder(values[3], typeof(T4)));
        }
    }

    [Obsolete("Ignored by messagepack apparently due to its non generic behaviour, kept for reference and other uses")]
    public class TupleResolver<T1, T2, T3, T4, T5> : MessagePackSerializer<Tuple<T1, T2, T3, T4, T5>>
    {
        private readonly MessagePackSerializer<object> _itemSerializer;
        public TupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
            _itemSerializer = ownerContext.GetSerializer<object>();
        }

        protected override void PackToCore(Packer packer, Tuple<T1, T2, T3, T4, T5> objectTree)
        {
            packer.PackArrayHeader(5);
            _itemSerializer.PackTo(packer, objectTree.Item1);
            _itemSerializer.PackTo(packer, objectTree.Item2);
            _itemSerializer.PackTo(packer, objectTree.Item3);
            _itemSerializer.PackTo(packer, objectTree.Item4);
            _itemSerializer.PackTo(packer, objectTree.Item5);
        }

        protected override Tuple<T1, T2, T3, T4, T5> UnpackFromCore(Unpacker unpacker)
        {
            MessagePackObject[] values = new MessagePackObject[2];
            for (int i = 0; i < 2; i++)
            {
                if (!unpacker.Read())
                {
                    throw SerializationExceptions.NewMissingItem(i);
                }

                MessagePackObject item;
                if (!unpacker.IsArrayHeader && !unpacker.IsMapHeader)
                {
                    item = (MessagePackObject)this._itemSerializer.UnpackFrom(unpacker);
                }
                else
                {
                    using Unpacker subtreeUnpacker = unpacker.ReadSubtree();
                    item = (MessagePackObject)this._itemSerializer.UnpackFrom(subtreeUnpacker);
                }

                values[i] = item;
            }
            return Tuple.Create((T1)BaseGateway.GetHolder(values[0], typeof(T1)), (T2)BaseGateway.GetHolder(values[1], typeof(T2)), (T3)BaseGateway.GetHolder(values[2], typeof(T3)), (T4)BaseGateway.GetHolder(values[3], typeof(T4)), (T5)BaseGateway.GetHolder(values[4], typeof(T5)));
        }
    }

    [Obsolete("Ignored by messagepack apparently due to its non generic behaviour, kept for reference and other uses")]
    public class TupleResolver<T1, T2, T3, T4, T5, T6> : MessagePackSerializer<Tuple<T1, T2, T3, T4, T5, T6>>
    {
        private readonly MessagePackSerializer<object> _itemSerializer;
        public TupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
            _itemSerializer = ownerContext.GetSerializer<object>();
        }

        protected override void PackToCore(Packer packer, Tuple<T1, T2, T3, T4, T5, T6> objectTree)
        {
            packer.PackArrayHeader(6);
            _itemSerializer.PackTo(packer, objectTree.Item1);
            _itemSerializer.PackTo(packer, objectTree.Item2);
            _itemSerializer.PackTo(packer, objectTree.Item3);
            _itemSerializer.PackTo(packer, objectTree.Item4);
            _itemSerializer.PackTo(packer, objectTree.Item5);
            _itemSerializer.PackTo(packer, objectTree.Item6);
        }

        protected override Tuple<T1, T2, T3, T4, T5, T6> UnpackFromCore(Unpacker unpacker)
        {
            MessagePackObject[] values = new MessagePackObject[2];
            for (int i = 0; i < 2; i++)
            {
                if (!unpacker.Read())
                {
                    throw SerializationExceptions.NewMissingItem(i);
                }

                MessagePackObject item;
                if (!unpacker.IsArrayHeader && !unpacker.IsMapHeader)
                {
                    item = (MessagePackObject)this._itemSerializer.UnpackFrom(unpacker);
                }
                else
                {
                    using Unpacker subtreeUnpacker = unpacker.ReadSubtree();
                    item = (MessagePackObject)this._itemSerializer.UnpackFrom(subtreeUnpacker);
                }

                values[i] = item;
            }
            return Tuple.Create((T1)BaseGateway.GetHolder(values[0], typeof(T1)), (T2)BaseGateway.GetHolder(values[1], typeof(T2)), (T3)BaseGateway.GetHolder(values[2], typeof(T3)), (T4)BaseGateway.GetHolder(values[3], typeof(T4)), (T5)BaseGateway.GetHolder(values[4], typeof(T5)), (T6)BaseGateway.GetHolder(values[5], typeof(T6)));
        }
    }

    [Obsolete("Ignored by messagepack apparently due to its non generic behaviour, kept for reference and other uses")]
    public class TupleResolver<T1, T2, T3, T4, T5, T6, T7> : MessagePackSerializer<Tuple<T1, T2, T3, T4, T5, T6, T7>>
    {
        private readonly MessagePackSerializer<object> _itemSerializer;
        public TupleResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
            _itemSerializer = ownerContext.GetSerializer<object>();
        }

        protected override void PackToCore(Packer packer, Tuple<T1, T2, T3, T4, T5, T6, T7> objectTree)
        {
            packer.PackArrayHeader(7);
            _itemSerializer.PackTo(packer, objectTree.Item1);
            _itemSerializer.PackTo(packer, objectTree.Item2);
            _itemSerializer.PackTo(packer, objectTree.Item3);
            _itemSerializer.PackTo(packer, objectTree.Item4);
            _itemSerializer.PackTo(packer, objectTree.Item5);
            _itemSerializer.PackTo(packer, objectTree.Item6);
            _itemSerializer.PackTo(packer, objectTree.Item7);
        }

        protected override Tuple<T1, T2, T3, T4, T5, T6, T7> UnpackFromCore(Unpacker unpacker)
        {
            MessagePackObject[] values = new MessagePackObject[2];
            for (int i = 0; i < 2; i++)
            {
                if (!unpacker.Read())
                {
                    throw SerializationExceptions.NewMissingItem(i);
                }

                MessagePackObject item;
                if (!unpacker.IsArrayHeader && !unpacker.IsMapHeader)
                {
                    item = (MessagePackObject)this._itemSerializer.UnpackFrom(unpacker);
                }
                else
                {
                    using Unpacker subtreeUnpacker = unpacker.ReadSubtree();
                    item = (MessagePackObject)this._itemSerializer.UnpackFrom(subtreeUnpacker);
                }

                values[i] = item;
            }
            return Tuple.Create((T1)BaseGateway.GetHolder(values[0], typeof(T1)), (T2)BaseGateway.GetHolder(values[1], typeof(T2)), (T3)BaseGateway.GetHolder(values[2], typeof(T3)), (T4)BaseGateway.GetHolder(values[3], typeof(T4)), (T5)BaseGateway.GetHolder(values[4], typeof(T5)), (T6)BaseGateway.GetHolder(values[5], typeof(T6)), (T7)BaseGateway.GetHolder(values[6], typeof(T7)));
        }
    }
}