using System;
using System.IO;
using FxEvents.Shared.Buffers;

namespace FxEvents.Shared.Serialization
{
    public class SerializationContext : IDisposable
    {
        internal string Source { get; set; }
        internal string Details { get; set; }
        internal BinaryWriter? Writer
        {
            get => _writer ??= new BinaryWriter(_memory);
            set
            {
                _writer?.Dispose();
                _writer = value;
            }
        }

        internal BinaryReader? Reader
        {
            get => _reader ??= new BinaryReader(_memory);
            set
            {
                _reader?.Dispose();
                _reader = value;
            }
        }

        internal byte[]? Original { get; set; }

        private ISerialization _serialization;
        private Stream _memory;
        private BinaryReader? _reader;
        private BinaryWriter? _writer;

        internal byte[] GetData()
        {
            if (_memory is PooledMemoryStream pooled)
                return pooled.ToArray();
            if (_memory is MemoryStream ms)
                return ms.ToArray();

            using MemoryStream temp = new MemoryStream();
            _memory.Position = 0;
            _memory.CopyTo(temp);
            return temp.ToArray();
        }

        internal Stream GetMemory()
        {
            return _memory;
        }

        internal SerializationContext(string source, string details, ISerialization serialization, byte[]? data = null)
        {
            Source = source;
            Details = details;
            _serialization = serialization;
            _memory = data != null ? new MemoryStream(data, writable: false) : new PooledMemoryStream();

            if (data != null)
            {
                Original = data;
            }
        }

        public void Dispose()
        {
            _writer?.Dispose();
            _reader?.Dispose();
            _memory?.Dispose();
        }

        internal void Serialize(Type type, object value) => _serialization.Serialize(type, value, this);
        internal void Serialize<T>(T value) => _serialization.Serialize(value, this);
        internal object Deserialize(Type type) => _serialization.Deserialize(type, this);
        internal T Deserialize<T>() => _serialization.Deserialize<T>(this);
    }
}
