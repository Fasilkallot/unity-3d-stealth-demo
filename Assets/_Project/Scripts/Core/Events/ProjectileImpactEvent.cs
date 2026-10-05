using UnityEngine;

namespace EvidenceRun.Core
{
    public readonly struct ProjectileImpactEvent
    {
        public readonly Vector3 Position;

        public ProjectileImpactEvent(Vector3 position)
        {
            Position = position;
        }
    }
}