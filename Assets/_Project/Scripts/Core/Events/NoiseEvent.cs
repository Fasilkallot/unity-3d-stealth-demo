using UnityEngine;

namespace EvidenceRun.Core
{
    public readonly struct NoiseEvent
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly Object Source;

        public NoiseEvent(
            Vector3 position,
            float radius,
            Object source)
        {
            Position = position;
            Radius = radius;
            Source = source;
        }
    }
}