using EvidenceRun.Core;
using EvidenceRun.Gameplay.Projectiles;
using UnityEngine;

namespace EvidenceRun.Gameplay.Combat
{
    public sealed class ThrowableWeapon : IWeapon
    {
        private readonly ThrowableConfig _config;
        private readonly ProjectileSystem _projectileSystem;

        public ThrowableWeapon(
            ThrowableConfig config,
            ProjectileSystem projectileSystem)
        {
            _config = config;
            _projectileSystem = projectileSystem;
        }

        public bool TryUse(
            Vector3 origin,
            Vector3 aimPoint,
            Object source)
        {
            Vector3 direction =
                aimPoint - origin;

            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            direction.Normalize();

            Vector3 velocity =
                direction * _config.ThrowSpeed;

            return _projectileSystem.TrySpawn(
                origin,
                velocity,
                _config.Lifetime,
                _config.NoiseRadius,
                source);
        }
    }
}