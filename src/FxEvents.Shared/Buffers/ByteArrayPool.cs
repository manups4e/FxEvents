using System;
using System.Collections.Concurrent;

namespace FxEvents.Shared.Buffers
{
	/// <summary>
	/// Zero-dependency, high-performance byte array pool compatible with .NET Framework 4.5.2 and .NET Standard 2.0.
	/// Pools common buffer sizes to eliminate GC Gen 0 allocations in the network and encryption pipelines.
	/// </summary>
	public static class ByteArrayPool
	{
		private static readonly int[] BucketSizes =
		{
			16,
			64,
			256,
			1024,
			4096,
			16384,
			65536
		};

		private static readonly ConcurrentQueue<byte[]>[] Buckets = new ConcurrentQueue<byte[]>[BucketSizes.Length];
		private const int MaxPerBucket = 50;

		static ByteArrayPool()
		{
			for (int i = 0; i < Buckets.Length; i++)
			{
				Buckets[i] = new ConcurrentQueue<byte[]>();
			}
		}

		/// <summary>
		/// Rents a byte array with at least the requested length.
		/// </summary>
		public static byte[] Rent(int minimumLength)
		{
			if (minimumLength <= 0)
				return new byte[0];

			for (int i = 0; i < BucketSizes.Length; i++)
			{
				if (BucketSizes[i] >= minimumLength)
				{
					if (Buckets[i].TryDequeue(out byte[] buffer))
					{
						return buffer;
					}
					return new byte[BucketSizes[i]];
				}
			}

			// Exceeds max bucket (64 KB); allocate directly
			return new byte[minimumLength];
		}

		/// <summary>
		/// Returns a previously rented byte array to the pool.
		/// </summary>
		public static void Return(byte[] array, bool clearArray = false)
		{
			if (array == null || array.Length == 0)
				return;

			for (int i = 0; i < BucketSizes.Length; i++)
			{
				if (BucketSizes[i] == array.Length)
				{
					if (clearArray)
					{
						Array.Clear(array, 0, array.Length);
					}

					if (Buckets[i].Count < MaxPerBucket)
					{
						Buckets[i].Enqueue(array);
					}
					return;
				}
			}
		}

		/// <summary>
		/// Rents a buffer wrapped in a disposable struct for clean using-statement scopes.
		/// </summary>
		public static RentedBuffer RentScoped(int minimumLength, bool clearOnDispose = false)
		{
			return new RentedBuffer(Rent(minimumLength), clearOnDispose);
		}
	}

	/// <summary>
	/// Disposable wrapper for a rented byte array.
	/// </summary>
	public readonly struct RentedBuffer : IDisposable
	{
		public readonly byte[] Buffer;
		private readonly bool _clearOnDispose;

		public RentedBuffer(byte[] buffer, bool clearOnDispose)
		{
			Buffer = buffer;
			_clearOnDispose = clearOnDispose;
		}

		public void Dispose()
		{
			if (Buffer != null)
			{
				ByteArrayPool.Return(Buffer, _clearOnDispose);
			}
		}
	}
}
