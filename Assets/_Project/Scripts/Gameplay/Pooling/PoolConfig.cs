using UnityEngine;

namespace EvidenceRun.Gameplay.Pooling
{
    [CreateAssetMenu(
        fileName = "PoolConfig",
        menuName = "EvidenceRun/Pooling/Pool Config")]
    public sealed class PoolConfig : ScriptableObject
    {
        [Header("Projectile Pool")]
        [Min(1)]
        [SerializeField] private int projectileCapacity = 10;

        [Header("Ripple Effect Pool")]
        [Min(1)]
        [SerializeField] private int rippleCapacity = 10;

        public int ProjectileCapacity => projectileCapacity;
        public int RippleCapacity => rippleCapacity;
    }
}