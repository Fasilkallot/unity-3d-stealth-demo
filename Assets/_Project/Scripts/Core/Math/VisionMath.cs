using UnityEngine;

namespace EvidenceRun.Core
{
    public static class VisionMath
    {
        public static bool InCone(
            Vector3 eye,
            Vector3 forwardFlat,
            Vector3 target,
            float rangeSqr,
            float cosHalfFov)
        {
            Vector3 to = target - eye;
            to.y = 0f;

            float sqrDistance = to.sqrMagnitude;

            // 1. Range check
            if (sqrDistance > rangeSqr)
            {
                return false;
            }

            // Target is effectively at the guard's position.
            if (sqrDistance < 0.0001f)
            {
                return true;
            }

            // 2. Angle check
            float inverseDistance = 1f / Mathf.Sqrt(sqrDistance);

            return Vector3.Dot(
                forwardFlat,
                to * inverseDistance) >= cosHalfFov;
        }
    }
}