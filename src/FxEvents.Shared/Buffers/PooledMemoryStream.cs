using System;
using System.IO;

namespace FxEvents.Shared.Buffers
{
	/// <summary>
	/// High-performance stream that rents its backing buffer from <see cref="ByteArrayPool"/>
	/// and returns it when disposed, eliminating intermediate heap allocations.
	/// </summary>
	public sealed class PooledMemoryStream : Stream
	{
		private byte[] _buffer;
		private int _position;
		private int _length;
		private bool _isDisposed;

		public PooledMemoryStream(int initialCapacity = 256)
		{
			_buffer = ByteArrayPool.Rent(initialCapacity);
			_position = 0;
			_length = 0;
		}

		public PooledMemoryStream(byte[] initialData)
		{
			if (initialData == null)
				throw new ArgumentNullException(nameof(initialData));

			_buffer = ByteArrayPool.Rent(initialData.Length);
			Buffer.BlockCopy(initialData, 0, _buffer, 0, initialData.Length);
			_position = 0;
			_length = initialData.Length;
		}

		public override bool CanRead => !_isDisposed;
		public override bool CanSeek => !_isDisposed;
		public override bool CanWrite => !_isDisposed;
		public override long Length => _length;

		public override long Position
		{
			get => _position;
			set
			{
				if (value < 0 || value > int.MaxValue)
					throw new ArgumentOutOfRangeException(nameof(value));
				_position = (int)value;
			}
		}

		public override void Flush() { }

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (_isDisposed) throw new ObjectDisposedException(nameof(PooledMemoryStream));
			if (buffer == null) throw new ArgumentNullException(nameof(buffer));
			if (offset < 0 || count < 0 || offset + count > buffer.Length)
				throw new ArgumentOutOfRangeException();

			int remaining = _length - _position;
			if (remaining <= 0) return 0;

			int toRead = count > remaining ? remaining : count;
			Buffer.BlockCopy(_buffer, _position, buffer, offset, toRead);
			_position += toRead;
			return toRead;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			if (_isDisposed) throw new ObjectDisposedException(nameof(PooledMemoryStream));
			if (buffer == null) throw new ArgumentNullException(nameof(buffer));
			if (offset < 0 || count < 0 || offset + count > buffer.Length)
				throw new ArgumentOutOfRangeException();

			EnsureCapacity(_position + count);
			Buffer.BlockCopy(buffer, offset, _buffer, _position, count);
			_position += count;
			if (_position > _length)
			{
				_length = _position;
			}
		}

		public override void WriteByte(byte value)
		{
			if (_isDisposed) throw new ObjectDisposedException(nameof(PooledMemoryStream));
			EnsureCapacity(_position + 1);
			_buffer[_position++] = value;
			if (_position > _length)
			{
				_length = _position;
			}
		}

		public override int ReadByte()
		{
			if (_isDisposed) throw new ObjectDisposedException(nameof(PooledMemoryStream));
			if (_position >= _length) return -1;
			return _buffer[_position++];
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			if (_isDisposed) throw new ObjectDisposedException(nameof(PooledMemoryStream));
			long newPos = origin switch
			{
				SeekOrigin.Begin => offset,
				SeekOrigin.Current => _position + offset,
				SeekOrigin.End => _length + offset,
				_ => throw new ArgumentOutOfRangeException(nameof(origin))
			};

			if (newPos < 0 || newPos > int.MaxValue)
				throw new IOException("Seek out of range");

			_position = (int)newPos;
			return _position;
		}

		public override void SetLength(long value)
		{
			if (_isDisposed) throw new ObjectDisposedException(nameof(PooledMemoryStream));
			if (value < 0 || value > int.MaxValue)
				throw new ArgumentOutOfRangeException(nameof(value));

			int newLen = (int)value;
			EnsureCapacity(newLen);
			_length = newLen;
			if (_position > _length)
			{
				_position = _length;
			}
		}

		private void EnsureCapacity(int required)
		{
			if (required <= _buffer.Length) return;

			int newCap = _buffer.Length * 2;
			if (newCap < required)
				newCap = required;

			byte[] newBuf = ByteArrayPool.Rent(newCap);
			if (_length > 0)
			{
				Buffer.BlockCopy(_buffer, 0, newBuf, 0, _length);
			}
			ByteArrayPool.Return(_buffer);
			_buffer = newBuf;
		}

		/// <summary>
		/// Returns the exact byte array with the current stream content.
		/// (Single final allocation for network transmission).
		/// </summary>
		public byte[] ToArray()
		{
			if (_isDisposed) throw new ObjectDisposedException(nameof(PooledMemoryStream));
			if (_length == 0) return new byte[0];
			byte[] copy = new byte[_length];
			Buffer.BlockCopy(_buffer, 0, copy, 0, _length);
			return copy;
		}

		/// <summary>
		/// Provides direct access to the internal rented buffer. Length is available via <see cref="Length"/>.
		/// </summary>
		public byte[] GetBuffer()
		{
			if (_isDisposed) throw new ObjectDisposedException(nameof(PooledMemoryStream));
			return _buffer;
		}

		protected override void Dispose(bool disposing)
		{
			if (!_isDisposed)
			{
				_isDisposed = true;
				if (_buffer != null)
				{
					ByteArrayPool.Return(_buffer);
					_buffer = null;
				}
			}
			base.Dispose(disposing);
		}
	}
}
