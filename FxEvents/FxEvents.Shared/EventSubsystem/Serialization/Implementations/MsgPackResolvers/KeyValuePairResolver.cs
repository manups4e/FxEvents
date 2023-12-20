using MsgPack;
using MsgPack.Serialization;
using System.Collections.Generic;

namespace FxEvents.Shared.Serialization.Implementations.MsgPackResolvers
{
    public class KeyValuePairResolver<TKey, TValue> : MessagePackSerializer<KeyValuePair<TKey, TValue>>
    {
        private readonly MessagePackSerializer<TKey> _keySerializer;
        private readonly MessagePackSerializer<TValue> _valueSerializer;
        public KeyValuePairResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, KeyValuePair<TKey, TValue> objectTree)
        {
            packer.PackArrayHeader(2);
            this._keySerializer.PackTo(packer, objectTree.Key);
            this._valueSerializer.PackTo(packer, objectTree.Value);
        }

        protected override KeyValuePair<TKey, TValue> UnpackFromCore(Unpacker unpacker)
        {
            if (!unpacker.Read())
            {
                return default;
            }

            TKey key = unpacker.LastReadData.IsNil ? default(TKey) : this._keySerializer.UnpackFrom(unpacker);

            if (!unpacker.Read())
            {
                return default;
            }

            TValue value = unpacker.LastReadData.IsNil ? default(TValue) : this._valueSerializer.UnpackFrom(unpacker);

            return new KeyValuePair<TKey, TValue>(key, value);
        }

    }
}