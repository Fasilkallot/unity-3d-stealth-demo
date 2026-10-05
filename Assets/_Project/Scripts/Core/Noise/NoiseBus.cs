using UnityEngine;

namespace EvidenceRun.Core
{
    public sealed class NoiseBus
    {
        private readonly INoiseListener[] _listeners;

        private int _listenerCount;

        public int ListenerCount => _listenerCount;

        public NoiseBus(int capacity)
        {
            if (capacity <= 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(capacity));
            }

            _listeners = new INoiseListener[capacity];
            _listenerCount = 0;
        }

        public bool Register(INoiseListener listener)
        {
            if (listener == null)
            {
                return false;
            }

            // Prevent duplicate registration.
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i], listener))
                {
                    return false;
                }
            }

            if (_listenerCount >= _listeners.Length)
            {
                return false;
            }

            _listeners[_listenerCount] = listener;
            _listenerCount++;

            return true;
        }

        public void Emit(in NoiseEvent noiseEvent)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                _listeners[i].OnNoise(in noiseEvent);
            }
        }
    }
}