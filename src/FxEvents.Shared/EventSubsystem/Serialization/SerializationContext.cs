using System;
using System.IO;

namespace FxEvents.Shared.Serialization
{
    public class SerializationContext : IDisposable
    {
        internal string Source { get; set; }
        internal string Details { get; set; }
        internal BinaryWriter? Writer
        {
            get => _writer;
            set
            {
                _writer?.Dispose();
                _writer = value;
            }
        }

        internal BinaryReader? Reader
        {
            get => _reader;
            set
            {
                _reader?.Dispose();
                _reader = value;
            }
        }

        internal byte[]? Original { get; set; }

        private ISerialization _serialization;
        private MemoryStream _memory;
        private BinaryReader? _reader;
        private BinaryWriter? _writer;

        internal byte[] GetData()
        {
            return _memory.ToArray();
        }

        internal MemoryStream GetMemory()
        {
            return _memory;
        }

        internal SerializationContext(string source, string details, ISerialization serialization, byte[]? data = null)
        {
            Source = source;
            Details = details;
            _serialization = serialization;
            _memory = data != null ? new MemoryStream(data) : new MemoryStream();
            _writer = new BinaryWriter(_memory);
            _reader = new BinaryReader(_memory);

            if (data != null)
            {
                Original = data;
            }
        }

        public void Dispose()
        {
            Writer?.Dispose();
            Reader?.Dispose();
        }

        internal void Serialize(Type type, object value) => _serialization.Serialize(type, value, this);
        internal void Serialize<T>(T value) => _serialization.Serialize(value, this);
        internal object Deserialize(Type type) => _serialization.Deserialize(type, this);
        internal T Deserialize<T>() => _serialization.Deserialize<T>(this);
    }
}