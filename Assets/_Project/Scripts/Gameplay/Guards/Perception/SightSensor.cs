using EvidenceRun.Core;
using EvidenceRun.Gameplay.Guards;
using UnityEngine;

namespace EvidenceRun.Gameplay.Guards.Perception
{
    public sealed class SightSensor : MonoBehaviour
    {
        [Header("Vision")]
        [SerializeField, Min(0f)] private float eyeHeight = 1.6f;
        [SerializeField, Min(0f)] private float targetHeight = 1.0f;

        [Header("Layers")]
        [SerializeField] private LayerMask visionBlockingMask;

        private SightResult _lastResult;

        public SightResult LastResult => _lastResult;

        private GuardConfig _guardConfig;
        private Transform _target;


        public void Initialize(GuardConfig guardConfig, Transform target)
        {
            _guardConfig = guardConfig;
            _target = target;
        }

        public SightResult Evaluate()
        {
            Vector3 eyePosition =
                transform.position + Vector3.up * eyeHeight;

            Vector3 forwardFlat = transform.forward;
            forwardFlat.y = 0f;

            if (forwardFlat.sqrMagnitude < 0.0001f)
            {
                return new SightResult(false, float.MaxValue);
            }

            forwardFlat.Normalize();

            Vector3 targetPosition =
                _target.position + Vector3.up * targetHeight;

            Vector3 toTarget = targetPosition - eyePosition;
            toTarget.y = 0f;

            float sqrDistance = toTarget.sqrMagnitude;

            // 1. Distance
            if (sqrDistance > _guardConfig.VisionRangeSqr)
            {
                return CacheResult(false, sqrDistance);
            }

            // 2. FOV
            if (!VisionMath.InCone(
                    eyePosition,
                    forwardFlat,
                    targetPosition,
                    _guardConfig.VisionRangeSqr,
                    _guardConfig.CosHalfFov))
            {
                return CacheResult(false, sqrDistance);
            }

            // 3. Line of sight
            Vector3 rayDirection = targetPosition - eyePosition;
            float distance = rayDirection.magnitude;

            if (Physics.Raycast(
                    eyePosition,
                    rayDirection / distance,
                    distance,
                    visionBlockingMask,
                    QueryTriggerInteraction.Ignore))
            {
                return CacheResult(false, sqrDistance);
            }

            return CacheResult(true, sqrDistance);
        }

        private SightResult CacheResult(
            bool visible,
            float sqrDistance)
        {
            _lastResult = new SightResult(
                visible,
                sqrDistance);

            return _lastResult;
        }
    }


}