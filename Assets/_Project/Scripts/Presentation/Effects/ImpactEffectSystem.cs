using EvidenceRun.Core;
using UnityEngine;

namespace EvidenceRun.Presentation.Effects
{
    public sealed class ImpactEffectSystem
    {
        private readonly ObjectPool<PooledImpactEffect> _pool;
        private readonly PooledImpactEffect[] _activeEffects;

        private int _activeCount;

        public int ActiveCount => _activeCount;

        public ImpactEffectSystem(
            ObjectPool<PooledImpactEffect> pool,
            int capacity)
        {
            _pool = pool;
            _activeEffects = new PooledImpactEffect[capacity];
        }

        public bool TrySpawn(Vector3 position)
        {
            if (_activeCount >= _activeEffects.Length)
            {
                return false;
            }

            if (!_pool.TryGet(
                    out PooledImpactEffect effect))
            {
                return false;
            }

            effect.Initialize(position);

            _activeEffects[_activeCount] = effect;
            _activeCount++;

            return true;
        }

        public void Tick()
        {
            for (int i = _activeCount - 1; i >= 0; i--)
            {
                PooledImpactEffect effect =
                    _activeEffects[i];

                if (!effect.IsFinished)
                {
                    continue;
                }

                RemoveAt(i);
            }
        }

        private void RemoveAt(int index)
        {
            PooledImpactEffect effect =
                _activeEffects[index];

            _pool.Release(effect);

            int lastIndex = _activeCount - 1;

            if (index != lastIndex)
            {
                _activeEffects[index] =
                    _activeEffects[lastIndex];
            }

            _activeEffects[lastIndex] = null;
            _activeCount--;
        }

        public void OnProjectileImpact(ProjectileImpactEvent eventData)
        {
            TrySpawn(eventData.Position);
        }
    }


}