using EvidenceRun.Core;
using UnityEngine;

namespace EvidenceRun.Presentation.Effects
{
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class PooledImpactEffect : MonoBehaviour, IPoolable
    {
        private ParticleSystem _particleSystem;

        public bool IsFinished => !_particleSystem.IsAlive(true);

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
        }

        public void Initialize(Vector3 position)
        {
            transform.position = position;

            _particleSystem.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);

            _particleSystem.Clear(true);

            _particleSystem.Play(true);
        }

        public void OnSpawn()
        {
            gameObject.SetActive(true);
        }

        public void OnDespawn()
        {
            _particleSystem.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);

            gameObject.SetActive(false);
        }
    }
}