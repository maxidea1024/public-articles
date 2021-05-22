using System;

namespace G.Util
{
    /// <summary>
    /// ...
    /// </summary>
    public class UID64Generator
    {
        /// <summary>...</summary>
        public static UID64Generator Shared { get; } = new UID64Generator(0, 0, 0);

        /// <summary>...</summary>
        public const long Epoch = 63618825600000; // 2017-1-1 0:00:00 (UTC) (in millisec)

        /// <summary>...</summary>
        public const int SequenceBits = 13;
        /// <summary>...</summary>
        public const int WorkerIdBits = 2;
        /// <summary>...</summary>
        public const int DatacenterIdBits = 2;
        /// <summary>...</summary>
        public const int ObjectTypeBits = 5;
        /// <summary>...</summary>
        public const int TimestampBits = 42;

        /// <summary>...</summary>
        public const int WorkerIdShift = SequenceBits;
        /// <summary>...</summary>
        public const int DatacenterIdShift = (SequenceBits + WorkerIdBits);
        /// <summary>...</summary>
        public const int ObjectTypeShift = (SequenceBits + WorkerIdBits + DatacenterIdBits);
        /// <summary>...</summary>
        public const int TimestampShift = (SequenceBits + WorkerIdBits + DatacenterIdBits + ObjectTypeBits);

        /// <summary>...</summary>
        public const long MaxWorkerId = (1L << WorkerIdBits) - 1;
        /// <summary>...</summary>
        public const long MaxDatacenterId = (1L << DatacenterIdBits) - 1;
        /// <summary>...</summary>
        public const long MaxObjectType = (1L << ObjectTypeBits) - 1;
        /// <summary>...</summary>
        public const long MaxTimestamp = (1L << TimestampBits) - 1;

        /// <summary>...</summary>
        public const long SequenceMask = (1L << SequenceBits) - 1;

        private long _sequence;
        private long _workerId;
        private long _datacenterId;
        private long _lastTimestamp;

        private readonly object _lock = new object();

        /// <summary>...</summary>
        public UID64Generator(int datacenterId, int workerId, int sequence)
        {
            if (datacenterId < 0 || datacenterId > MaxDatacenterId)
            {
                throw new ArgumentOutOfRangeException(nameof(datacenterId));
            }

            if (workerId < 0 || workerId > MaxWorkerId)
            {
                throw new ArgumentOutOfRangeException(nameof(workerId));
            }

            if (sequence < 0 || sequence > SequenceMask)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            _datacenterId = datacenterId;
            _workerId = workerId;
            _sequence = sequence;
            _lastTimestamp = -1;
        }

        /// <summary>...</summary>
        public ulong Next(int objectType = 0)
        {
            if (objectType < 0 || objectType > MaxObjectType)
            {
                throw new ArgumentOutOfRangeException(nameof(objectType));
            }

            lock (_lock)
            {
                long timestamp = SystemClock.Milliseconds;
                if (timestamp < _lastTimestamp)
                {
                    throw new InvalidOperationException("Clock is moving backwards. It is assumed that the system clock is adjusted arbitrarily, and ID can not be normally issued.");
                }

                if (_lastTimestamp == timestamp)
                {
                    _sequence = (_sequence + 1) & SequenceMask;
                    if (_sequence == 0) // Overflow
                    {
                        timestamp = TilNextMillisec(_lastTimestamp);
                    }
                }
                else
                {
                    _sequence = 0;
                }

                _lastTimestamp = timestamp;

                long timeOffset = timestamp - Epoch;
                long id =
                    (timeOffset << TimestampShift) |
                    ((long)objectType << ObjectTypeShift) |
                    (_datacenterId << DatacenterIdShift) |
                    (_workerId << WorkerIdShift) |
                    _sequence;
                return (ulong)id;
            }
        }

        private long TilNextMillisec(long lastTimestamp)
        {
            long timestamp = SystemClock.Milliseconds;
            while (timestamp <= lastTimestamp)
            {
                timestamp = SystemClock.Milliseconds;
            }

            return timestamp;
        }
    }
}
