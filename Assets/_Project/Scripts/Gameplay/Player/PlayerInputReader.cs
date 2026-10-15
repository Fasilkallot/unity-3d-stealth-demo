using EvidenceRun.Core;
using UnityEngine;

namespace EvidenceRun.Gameplay.Player
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera playerCamera;

        private ICameraInputOrientation _cameraOrientation;
        private ActorIntent _intent;
        public ActorIntent Intent => _intent;

        private Quaternion InputRotation => _cameraOrientation != null
            ? _cameraOrientation.InputRotation
            : playerCamera.transform.rotation;

        private void Awake()
        {
            if (playerCamera == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerInputReader)} requires a player camera.",
                    this);

                enabled = false;
                return;
            }

            _cameraOrientation = playerCamera.GetComponent<ICameraInputOrientation>();
        }

        private void Update()
        {
            ReadMovement(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")));
            ReadCrouch();
            ReadRun();
            ReadAim();
            ReadThrow();
            ReadJump();
        }

        private void ReadThrow()
        {
            _intent.Throw = Input.GetKeyDown(KeyCode.G);
        }

        private void ReadMovement(Vector2 input)
        {
            input = Vector2.ClampMagnitude(input, 1f);

            Quaternion orientation = InputRotation;
            Vector3 cameraForward = orientation * Vector3.forward;
            Vector3 cameraRight = orientation * Vector3.right;

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

        private void ReadRun()
        {
            _intent.Run = Input.GetKey(KeyCode.LeftShift);
        }

        private void ReadAim()
        {
            Vector3 aimDirection = InputRotation * Vector3.forward;
            aimDirection.y = 0f;

            if (aimDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            aimDirection.Normalize();

            _intent.AimPoint =
                transform.position + aimDirection * 100f;
        }

        private void ReadJump()
        {
            _intent.Jump = Input.GetKeyDown(KeyCode.Space);
        }

    }
}
