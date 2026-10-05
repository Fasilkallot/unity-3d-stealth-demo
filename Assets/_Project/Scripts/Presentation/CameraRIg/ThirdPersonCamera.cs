using UnityEngine;

namespace EvidenceRun.Presentation.Camera
{
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Position")]
        [SerializeField, Min(0.1f)] private float distance = 5f;
        [SerializeField, Min(0f)] private float height = 2f;

        [Header("Orbit")]
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 3f;
        [SerializeField] private float minPitch = -20f;
        [SerializeField] private float maxPitch = 60f;

        [Header("Smoothing")]
        [SerializeField, Min(0f)] private float positionSmoothTime = 0.08f;
        [SerializeField, Min(0f)] private float rotationSmoothSpeed = 12f;

        [Header("Player Alignment")]
        [SerializeField, Min(0f)] private float playerAlignmentSpeed = 10f;

        private float _yaw;
        private float _pitch = 15f;

        private Vector3 _positionVelocity;

        private void Awake()
        {
            if (target == null)
            {
                Debug.LogError(
                    $"{nameof(ThirdPersonCamera)} requires a target.",
                    this);

                enabled = false;
                return;
            }

            _yaw = target.eulerAngles.y;
        }

        private void LateUpdate()
        {
            UpdateOrbit();
            UpdatePosition();
            UpdateRotation();
        }

        private void UpdateOrbit()
        {
            if (Input.GetMouseButton(0))
            {
                _yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
                _pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;

                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
                return;
            }

            float targetYaw = target.eulerAngles.y;

            _yaw = Mathf.LerpAngle(
                _yaw,
                targetYaw,
                playerAlignmentSpeed * Time.deltaTime);
        }

        private void UpdatePosition()
        {
            Vector3 focusPoint =
                target.position + Vector3.up * height;

            Quaternion orbitRotation =
                Quaternion.Euler(_pitch, _yaw, 0f);

            Vector3 desiredPosition =
                focusPoint + orbitRotation * Vector3.back * distance;

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref _positionVelocity,
                positionSmoothTime);
        }

        private void UpdateRotation()
        {
            Vector3 focusPoint =
                target.position + Vector3.up * height;

            Vector3 lookDirection =
                focusPoint - transform.position;

            if (lookDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion targetRotation =
                Quaternion.LookRotation(lookDirection);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSmoothSpeed * Time.deltaTime);
        }
    }
}