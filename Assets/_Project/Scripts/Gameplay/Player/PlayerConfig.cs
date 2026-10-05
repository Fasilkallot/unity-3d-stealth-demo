using EvidenceRun.Core;
using UnityEngine;

namespace EvidenceRun.Gameplay.Player
{
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "EvidenceRun/Player/PlayerConfig")]
    public sealed class PlayerConfig : ScriptableObject, IMovementConfig
    {
        [Header("Movement")]
        [Min(0f)]
        [SerializeField] private float moveSpeed = 5f;

        [Min(0f)]
        [SerializeField] private float acceleration = 20f;

        [Min(0f)]
        [SerializeField] private float deceleration = 25f;

        [Header("Crouch")]
        [Range(0.1f, 1f)]
        [SerializeField] private float crouchSpeedMultiplier = 0.5f;

        [SerializeField, Min(1f)]
        private float runSpeedMultiplier = 1.5f;

        public float RunSpeedMultiplier => runSpeedMultiplier;
        public float MoveSpeed => moveSpeed;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float CrouchSpeedMultiplier => crouchSpeedMultiplier;
    }
}

