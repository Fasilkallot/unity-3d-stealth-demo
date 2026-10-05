using EvidenceRun.Core;
using NUnit.Framework;

namespace EvidenceRun.Tests.EditMode
{
    public sealed class ObjectPoolTests
    {
        private sealed class TestPoolable : IPoolable
        {
            public int SpawnCount { get; private set; }
            public int DespawnCount { get; private set; }

            public void OnSpawn()
            {
                SpawnCount++;
            }

            public void OnDespawn()
            {
                DespawnCount++;
            }
        }

        [Test]
        public void Pool_IsPrewarmedWithFullCapacity()
        {
            var pool = new ObjectPool<TestPoolable>(
                () => new TestPoolable(),
                3);

            Assert.That(pool.Capacity, Is.EqualTo(3));
            Assert.That(pool.InUse, Is.EqualTo(0));
        }

        [Test]
        public void TryGet_DecreasesAvailableItems()
        {
            var pool = new ObjectPool<TestPoolable>(
                () => new TestPoolable(),
                3);

            bool result = pool.TryGet(out TestPoolable item);

            Assert.That(result, Is.True);
            Assert.That(item, Is.Not.Null);
            Assert.That(pool.InUse, Is.EqualTo(1));
            Assert.That(item.SpawnCount, Is.EqualTo(1));
        }

        [Test]
        public void TryGet_ReturnsFalse_WhenPoolIsExhausted()
        {
            var pool = new ObjectPool<TestPoolable>(
                () => new TestPoolable(),
                2);

            pool.TryGet(out _);
            pool.TryGet(out _);

            bool result = pool.TryGet(out TestPoolable item);

            Assert.That(result, Is.False);
            Assert.That(item, Is.Null);
            Assert.That(pool.InUse, Is.EqualTo(2));
        }

        [Test]
        public void Release_ReturnsItemToPool()
        {
            var pool = new ObjectPool<TestPoolable>(
                () => new TestPoolable(),
                2);

            pool.TryGet(out TestPoolable item);

            pool.Release(item);

            Assert.That(pool.InUse, Is.EqualTo(0));
            Assert.That(item.DespawnCount, Is.EqualTo(1));
        }

        [Test]
        public void DoubleRelease_DoesNotCorruptPool()
        {
            var pool = new ObjectPool<TestPoolable>(
                () => new TestPoolable(),
                3);

            pool.TryGet(out TestPoolable first);
            pool.TryGet(out TestPoolable second);

            pool.Release(first);
            pool.Release(first);

            Assert.That(pool.InUse, Is.EqualTo(1));
        }
    }
}