using UnityEngine;

namespace EvidenceRun.Gameplay.Combat
{
    [CreateAssetMenu(
        fileName = "ThrowableConfig",
        menuName = "EvidenceRun/Combat/Throwable Config")]
    public sealed class ThrowableConfig : ScriptableObject
    {
        [Header("Projectile")]
        [Min(0.1f)]
        [SerializeField] private float throwSpeed = 12f;

        [Min(0.1f)]
        [SerializeField] private float lifetime = 5f;

        [Header("Noise")]
        [Min(0f)]
        [SerializeField] private float noiseRadius = 8f;

        public float ThrowSpeed => throwSpeed;
        public float Lifetime => lifetime;
        public float NoiseRadius => noiseRadius;
    }
}