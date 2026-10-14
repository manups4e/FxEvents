using System;

namespace FxEvents.Shared.Snowflakes
{
	public partial class SnowflakeGenerator
	{
		private readonly long _maskSequence;
		private readonly long _maskTime;
		private readonly long _maskInstance;
		private readonly int _shiftTime;
		private readonly int _shiftInstance;
		private readonly object _lock = new object();
		private SnowflakeConfiguration _configuration;
		private long _instanceId;
		private long _sequence;
		private long _lastTimeslot;

		public static SnowflakeGenerator Create(short instance)
		{
			SnowflakeGenerator value = new SnowflakeGenerator(instance, new SnowflakeConfiguration());
			_singletonInstance = value;
			return value;
		}

		public SnowflakeGenerator(short instance, SnowflakeConfiguration configuration)
		{
			_configuration = configuration;
			_instanceId = instance;
			_maskTime = GetMask(configuration.TimestampBits);
			_maskInstance = GetMask(configuration.InstanceBits);
			_maskSequence = GetMask(configuration.SequenceBits);
			_shiftTime = configuration.InstanceBits + configuration.SequenceBits;
			_shiftInstance = configuration.SequenceBits;
		}

		public Snowflake Next(long time)
		{
			lock (_lock)
			{
				long timestamp = time & _maskTime;

				if (timestamp < _lastTimeslot)
					timestamp = _lastTimeslot;

				if (_lastTimeslot == timestamp)
				{
					_sequence = (_sequence + 1) & _maskSequence;

					if (_sequence == 0)
					{
						long nextTimestamp;
						do
							nextTimestamp = Clock.GetMilliseconds() & _maskTime;
						while (nextTimestamp <= _lastTimeslot);

						timestamp = nextTimestamp;
						_lastTimeslot = timestamp;
					}
				}
				else
				{
					_lastTimeslot = timestamp;
					_sequence = 0;
				}
				long generatedId = (timestamp << _shiftTime) | (_instanceId << _shiftInstance) | _sequence;
				return new Snowflake(generatedId);
			}
		}

		public Snowflake Next() => Next(Clock.GetMilliseconds());

		public SnowflakeFragments Deconstruct(long value)
		{
			SnowflakeFragments fragments = new SnowflakeFragments
			{
				Sequence = value & _maskSequence,
				Instance = (value >> _shiftInstance) & _maskInstance,
				Timestamp = (value >> _shiftTime) & _maskTime
			};

			return fragments;
		}

		private long GetMask(byte bits) => (1L << bits) - 1;

		public static SnowflakeGenerator Instance
		{
			get
			{
				if (_singletonInstance == null)
					throw new Exception("SnowflakeGenerator.Instance property was null");

				return _singletonInstance;
			}
		}

		private static SnowflakeGenerator _singletonInstance;
	}
}