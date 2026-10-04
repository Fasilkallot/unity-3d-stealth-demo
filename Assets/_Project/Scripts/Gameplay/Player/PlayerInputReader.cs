using EvidenceRun.Core;
using UnityEngine;

namespace EvidenceRun.Gameplay.Player
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera playerCamera;

        private ActorIntent _intent;

        private static readonly Plane GroundPlane =
            new Plane(Vector3.up, Vector3.zero);

        public ActorIntent Intent => _intent;

        private void Awake()
        {
            if (playerCamera == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerInputReader)} requires a player camera.",
                    this);

                enabled = false;
            }
        }

        private void Update()
        {
            ReadMovement();
            ReadCrouch();
            ReadAim();
        }

        private void ReadMovement()
        {
            Vector2 input = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));

            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 cameraForward = playerCamera.transform.forward;
            Vector3 cameraRight = playerCamera.transform.right;

            // Ignore camera pitch.
            cameraForward.y = 0f;
            cameraRight.y = 0f;

            cameraForward.Normalize();
            cameraRight.Normalize();

            Vector3 moveDirection =
                cameraRight * input.x +
                cameraForward * input.y;

            if (moveDirection.sqrMagnitude > 1f)
            {
                moveDirection.Normalize();
            }

            // ActorIntent.Move represents world-space X/Z movement.
            _intent.Move = new Vector2(
                moveDirection.x,
                moveDirection.z);
        }

        private void ReadCrouch()
        {
            _intent.Crouch = Input.GetKey(KeyCode.LeftControl);
        }

        private void ReadAim()
        {
            Vector3 aimDirection = playerCamera.transform.forward;
            aimDirection.y = 0f;

            if (aimDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            aimDirection.Normalize();

            _intent.AimPoint =
                transform.position + aimDirection * 100f;
        }
    }
}