using EvidenceRun.Core;
using UnityEngine;

namespace EvidenceRun.Gameplay.Projectiles
{
    public sealed class ProjectileSystem
    {
        private readonly ObjectPool<PooledProjectile> _pool;
        private readonly PooledProjectile[] _activeProjectiles;
        private readonly NoiseBus _noiseBus;
        private readonly GameEvents _gameEvents;

        private int _activeCount;

        public int ActiveCount => _activeCount;


        public ProjectileSystem(
            ObjectPool<PooledProjectile> pool,
            int capacity,
            NoiseBus noiseBus,
            GameEvents gameEvents)
        {
            _pool = pool;
            _noiseBus = noiseBus;
            _gameEvents = gameEvents;


            _activeProjectiles =
                new PooledProjectile[capacity];

            _activeCount = 0;
        }

        public bool TrySpawn(
            Vector3 position,
            Vector3 velocity,
            float lifetime,
            float noiseRadius,
            Object source)
        {
            if (_activeCount >= _activeProjectiles.Length)
            {
                return false;
            }

            if (!_pool.TryGet(out PooledProjectile projectile))
            {
                return false;
            }

            projectile.Initialize(
                position,
                velocity,
                lifetime,
                noiseRadius,
                source);

            _activeProjectiles[_activeCount] = projectile;
            _activeCount++;

            return true;
        }

        public void Tick(
            float deltaTime,
            LayerMask hitMask)
        {
            for (int i = _activeCount - 1; i >= 0; i--)
            {
                PooledProjectile projectile =
                    _activeProjectiles[i];

                projectile.Lifetime -= deltaTime;

                if (projectile.Lifetime <= 0f)
                {
                    RemoveProjectileAt(i);
                    continue;
                }
                Vector3 start =
                    projectile.Position;

                projectile.Velocity +=
                    Physics.gravity * deltaTime;

                Vector3 displacement =
                    projectile.Velocity * deltaTime;

                float distance =
                    displacement.magnitude;

                if (distance > 0f)
                {
                    Vector3 direction =
                        displacement / distance;

                    if (Physics.Raycast(
                            start,
                            direction,
                            out RaycastHit hit,
                            distance,
                            hitMask,
                            QueryTriggerInteraction.Ignore))
                    {
                        projectile.Position = hit.point;

                        NoiseEvent noiseEvent = new NoiseEvent(
                            hit.point,
                            projectile.NoiseRadius,
                            projectile.Source);

                        _noiseBus.Emit(in noiseEvent);

                        ProjectileImpactEvent impactEvent =
                            new ProjectileImpactEvent(hit.point);

                        _gameEvents.PublishProjectileImpact(
                            in impactEvent);

                        RemoveProjectileAt(i);
                        continue;
                    }
                }

                projectile.Position =
                    start + displacement;
            }
        }

        private void RemoveProjectileAt(int index)
        {
            PooledProjectile projectile =
                _activeProjectiles[index];

            _pool.Release(projectile);

            int lastIndex = _activeCount - 1;

            if (index != lastIndex)
            {
                _activeProjectiles[index] =
                    _activeProjectiles[lastIndex];
            }

            _activeProjectiles[lastIndex] = null;

            _activeCount--;
        }
    }
}