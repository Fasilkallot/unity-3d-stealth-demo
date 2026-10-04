using EvidenceRun.Core;
using UnityEngine;

namespace EvidenceRun.Gameplay.Guards
{
    [CreateAssetMenu(
        fileName = "GuardConfig",
        menuName = "EvidenceRun/Guard/Guard Config")]
    public sealed class GuardConfig : ScriptableObject, IMovementConfig
    {
        [Header("Vision")]
        [Min(0f)]
        [SerializeField] private float visionRange = 10f;

        [Header("Awareness")]
        [SerializeField, Min(0f)]
        private float awarenessGainPerSecond = 0.5f;

        [SerializeField, Min(0f)]
        private float awarenessDecayPerSecond = 0.25f;

        [Range(0f, 360f)]
        [SerializeField] private float fieldOfView = 90f;

        [SerializeField, HideInInspector]
        private float cosHalfFov;

        [Header("Movement")]
        [Min(0f)]
        [SerializeField] private float moveSpeed = 3.5f;

        [Min(0f)]
        [SerializeField] private float acceleration = 15f;

        [Min(0f)]
        [SerializeField] private float deceleration = 20f;

        [Range(0.1f, 1f)]
        [SerializeField] private float crouchSpeedMultiplier = 0.5f;

        [Header("State Thresholds")]
        [Range(0f, 1f)]
        [SerializeField] private float suspiciousThreshold = 0.4f;

        [Range(0f, 1f)]
        [SerializeField] private float patrolReturnThreshold = 0.1f;

        [Header("Investigation")]
        [SerializeField, Min(0f)]
        private float investigationDuration = 2f;

        [Header("Chase")]
        [SerializeField, Min(0f)]
        private float chaseLoseSightDuration = 3f;

        [SerializeField, Min(0f)]
        private float catchRadius = 1.25f;

        public float ChaseLoseSightDuration => chaseLoseSightDuration;
        public float CatchRadius => catchRadius;
        public float CatchRadiusSqr => catchRadius * catchRadius;
        public float InvestigationDuration => investigationDuration;

        public float SuspiciousThreshold => suspiciousThreshold;
        public float PatrolReturnThreshold => patrolReturnThreshold;

        public float MoveSpeed => moveSpeed;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float CrouchSpeedMultiplier => crouchSpeedMultiplier;

        public float VisionRange => visionRange;
        public float FieldOfView => fieldOfView;
        public float VisionRangeSqr => visionRange * visionRange;
        public float CosHalfFov => cosHalfFov;
        public float AwarenessGainPerSecond => awarenessGainPerSecond;
        public float AwarenessDecayPerSecond => awarenessDecayPerSecond;
        private void OnValidate()
        {
            cosHalfFov = Mathf.Cos(
                fieldOfView * 0.5f * Mathf.Deg2Rad);
        }
    }
}