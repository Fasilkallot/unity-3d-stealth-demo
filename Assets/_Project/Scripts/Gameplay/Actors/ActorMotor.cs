using EvidenceRun.Core;
using UnityEngine;

namespace EvidenceRun.Gameplay.Actors
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ActorMotor : MonoBehaviour
    {
        private IMovementConfig _movementConfig;
        private Rigidbody _rigidbody;
        private ActorIntent _intent;

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
        }

        private void FixedUpdate()
        {
            if (_movementConfig == null)
            {
                return;
            }

            Move();
            Rotate();
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
            if (_rigidbody == null)
            {
                return;
            }

            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
    }
}