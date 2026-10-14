using System;
using EvidenceRun.Core;
using UnityEngine;

namespace EvidenceRun.Gameplay.Actors
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ActorMotor : MonoBehaviour
    {
        [Header("Ground Check")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField] private float groundCheckRadius = 0.25f;
        [SerializeField] private float groundCheckOffset = 0.15f;
        [SerializeField] private float groundCheckDistance = 0.2f;

        private IMovementConfig _movementConfig;
        private Rigidbody _rigidbody;
        private ActorIntent _intent;
        private bool _isGrounded;
        private bool _jumpRequested;

        public bool IsGrounded => _isGrounded;
        public ActorIntent CurrentIntent => _intent;

        // Presentation observes an accepted jump; input alone cannot confirm takeoff.
        public event Action Jumped;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            ConfigureRigidbody();
        }

        public void Initialize(IMovementConfig movementConfig)
        {
            if (movementConfig == null)
            {
                Debug.LogError(
                    $"{nameof(ActorMotor)} requires a movement config.",
                    this);

                enabled = false;
                return;
            }

            _movementConfig = movementConfig;
        }

        public void SetIntent(in ActorIntent intent)
        {
            _intent = intent;

            if (intent.Jump)
            {
                _jumpRequested = true;
            }
        }

        private void FixedUpdate()
        {
            if (_movementConfig == null)
            {
                return;
            }

            CheckGround();
            Move();
            Jump();
            Rotate();
        }

        private void CheckGround()
        {
            Vector3 origin = transform.position + Vector3.up * groundCheckOffset;

            // The probe can still touch the floor on the first rising physics steps.
            // Do not report a landing (or permit a second jump) during ascent.
            _isGrounded = _rigidbody.linearVelocity.y <= 0.1f && Physics.SphereCast(
                origin,
                groundCheckRadius,
                Vector3.down,
                out _,
                groundCheckDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);
        }

        private void Jump()
        {
            if (!_jumpRequested)
            {
                return;
            }

            _jumpRequested = false;

            if (!_isGrounded || _intent.Crouch)
            {
                return;
            }

            Vector3 velocity = _rigidbody.linearVelocity;
            velocity.y = _movementConfig.JumpForce;
            _rigidbody.linearVelocity = velocity;

            _isGrounded = false;
            Jumped?.Invoke();
        }

        private void Move()
        {
            Vector2 input = _intent.Move;

            Vector3 desiredDirection = new Vector3(
                input.x,
                0f,
                input.y);

            if (desiredDirection.sqrMagnitude > 1f)
            {
                desiredDirection.Normalize();
            }

            float speed = _movementConfig.MoveSpeed;

            if (_intent.Crouch)
            {
                speed *= _movementConfig.CrouchSpeedMultiplier;
            }
            else if (_intent.Run)
            {
                speed *= _movementConfig.RunSpeedMultiplier;
            }

            Vector3 targetVelocity = desiredDirection * speed;
            Vector3 currentVelocity = _rigidbody.linearVelocity;

            Vector3 currentHorizontalVelocity = new Vector3(
                currentVelocity.x,
                0f,
                currentVelocity.z);

            float acceleration =
                targetVelocity.sqrMagnitude > 0.001f
                    ? _movementConfig.Acceleration
                    : _movementConfig.Deceleration;

            Vector3 newHorizontalVelocity = Vector3.MoveTowards(
                currentHorizontalVelocity,
                targetVelocity,
                acceleration * Time.fixedDeltaTime);

            _rigidbody.linearVelocity = new Vector3(
                newHorizontalVelocity.x,
                currentVelocity.y,
                newHorizontalVelocity.z);
        }

        private void Rotate()
        {
            Vector3 direction =
                _intent.AimPoint - transform.position;

            direction.y = 0f;

            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            _rigidbody.MoveRotation(
                Quaternion.LookRotation(direction));
        }

        private void ConfigureRigidbody()
        {
            _rigidbody.interpolation =
                RigidbodyInterpolation.Interpolate;

            _rigidbody.collisionDetectionMode =
                CollisionDetectionMode.Continuous;

            _rigidbody.constraints =
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;
        }

        public void Stop()
        {
            _intent = default;
            _jumpRequested = false;

            if (_rigidbody == null)
            {
                return;
            }

            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Vector3 origin = transform.position + Vector3.up * groundCheckOffset;
            Gizmos.DrawWireSphere(origin + Vector3.down * groundCheckDistance, groundCheckRadius);
        }
    }
}
