using System;

namespace EvidenceRun.Core
{
    public sealed class ObjectPool<T>
        where T : class, IPoolable
    {
        private readonly T[] _free;

        private int _count;

        public int Capacity => _free.Length;
        public int InUse => _free.Length - _count;

        public ObjectPool(
            Func<T> create,
            int capacity)
        {
            if (create == null)
            {
                throw new ArgumentNullException(nameof(create));
            }

            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(capacity));
            }

            _free = new T[capacity];

            for (int i = 0; i < capacity; i++)
            {
                _free[i] = create();
            }

            _count = capacity;
        }

        public bool TryGet(out T item)
        {
            if (_count == 0)
            {
                item = null;
                return false;
            }

            item = _free[--_count];

            item.OnSpawn();

            return true;
        }

        public void Release(T item)
        {
            if (item == null)
            {
                return;
            }

            if (_count == _free.Length)
            {
                return;
            }

            item.OnDespawn();

            _free[_count++] = item;
        }
    }
}