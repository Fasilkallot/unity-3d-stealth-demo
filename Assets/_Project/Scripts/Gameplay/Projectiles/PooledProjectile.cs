using EvidenceRun.Core;
using UnityEngine;

namespace EvidenceRun.Gameplay.Projectiles
{
    public sealed class PooledProjectile : MonoBehaviour, IPoolable
    {
        public float Lifetime { get; internal set; }
        public float NoiseRadius { get; private set; }
        public Object Source { get; private set; }
        public Vector3 Position
        {
            get => transform.position;
            set => transform.position = value;
        }

        public Vector3 Velocity { get; set; }

        public bool IsActive { get; private set; }

        public void Initialize(
            Vector3 position,
            Vector3 velocity,
            float lifetime,
            float noiseRadius,
            Object source)
        {
            transform.position = position;

            Velocity = velocity;
            Lifetime = lifetime;
            NoiseRadius = noiseRadius;
            Source = source;
        }

        public void OnSpawn()
        {
            IsActive = true;
            gameObject.SetActive(true);
        }

        public void OnDespawn()
        {
            IsActive = false;

            Velocity = Vector3.zero;
            Lifetime = 0f;
            NoiseRadius = 0f;
            Source = null;

            gameObject.SetActive(false);
        }
    }
}