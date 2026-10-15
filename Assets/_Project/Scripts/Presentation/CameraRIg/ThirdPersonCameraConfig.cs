using UnityEngine;

namespace EvidenceRun.Presentation.Camera
{
    [CreateAssetMenu(fileName = "ThirdPersonCameraConfig", menuName = "EvidenceRun/Camera/Third Person Camera Config")]
    public sealed class ThirdPersonCameraConfig : ScriptableObject
    {
        [Header("Framing")]
        [Tooltip("World-space offset from the dedicated, non-animated camera target.")]
        [SerializeField] private Vector3 targetOffset = Vector3.zero;
        [SerializeField, Min(0.1f)] private float distance = 4.5f;
        [SerializeField, Range(-85f, 85f)] private float initialPitch = 15f;

        [Header("Mouse Orbit")]
        [SerializeField, Min(0.01f)] private float sensitivityX = 3f;
        [SerializeField, Min(0.01f)] private float sensitivityY = 3f;
        [SerializeField, Range(-85f, 85f)] private float minPitch = -25f;
        [SerializeField, Range(-85f, 85f)] private float maxPitch = 65f;

        [Header("Response Rates (per second)")]
        [SerializeField, Min(0.1f)] private float pivotResponse = 25f;
        [SerializeField, Min(0.1f)] private float orbitResponse = 35f;
        [SerializeField, Min(0.1f)] private float distanceRestoreResponse = 8f;
        [SerializeField, Min(0.1f)] private float reframingResponse = 6f;
        [SerializeField, Min(1f)] private float reframingAngularSpeed = 300f;
        [SerializeField, Min(0.1f)] private float teleportDistance = 8f;

        [Header("Obstacles")]
        [SerializeField] private LayerMask collisionLayers = 265;
        [Tooltip("Minimum radius. The camera near-plane corners enlarge this when necessary.")]
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.25f;
        [SerializeField, Min(0.001f)] private float surfaceOffset = 0.03f;

        [Header("Player Clearance")]
        [SerializeField] private Vector3 playerClearanceCenter = new Vector3(0f, 0.15f, 0.15f);
        [SerializeField, Min(0.1f)] private float playerClearanceRadius = 1.2f;
        [SerializeField, Min(0.2f)] private float playerClearanceHeight = 3.8f;

        public Vector3 TargetOffset => targetOffset;
        public float Distance => distance;
        public float InitialPitch => initialPitch;
        public float SensitivityX => sensitivityX;
        public float SensitivityY => sensitivityY;
        public float MinPitch => minPitch;
        public float MaxPitch => maxPitch;
        public float PivotResponse => pivotResponse;
        public float OrbitResponse => orbitResponse;
        public float DistanceRestoreResponse => distanceRestoreResponse;
        public float ReframingResponse => reframingResponse;
        public float ReframingAngularSpeed => reframingAngularSpeed;
        public float TeleportDistance => teleportDistance;
        public LayerMask CollisionLayers => collisionLayers;
        public float CollisionRadius => collisionRadius;
        public float SurfaceOffset => surfaceOffset;
        public Vector3 PlayerClearanceCenter => playerClearanceCenter;
        public float PlayerClearanceRadius => playerClearanceRadius;
        public float PlayerClearanceHeight => playerClearanceHeight;

        private void OnValidate()
        {
            distance = Mathf.Max(0.1f, distance);
            sensitivityX = Mathf.Max(0.01f, sensitivityX);
            sensitivityY = Mathf.Max(0.01f, sensitivityY);
            minPitch = Mathf.Clamp(minPitch, -85f, 85f);
            maxPitch = Mathf.Clamp(maxPitch, minPitch, 85f);
            initialPitch = Mathf.Clamp(initialPitch, minPitch, maxPitch);
            pivotResponse = Mathf.Max(0.1f, pivotResponse);
            orbitResponse = Mathf.Max(0.1f, orbitResponse);
            distanceRestoreResponse = Mathf.Max(0.1f, distanceRestoreResponse);
            reframingResponse = Mathf.Max(0.1f, reframingResponse);
            reframingAngularSpeed = Mathf.Max(1f, reframingAngularSpeed);
            teleportDistance = Mathf.Max(0.1f, teleportDistance);
            collisionRadius = Mathf.Max(0.01f, collisionRadius);
            surfaceOffset = Mathf.Max(0.001f, surfaceOffset);
            playerClearanceRadius = Mathf.Max(0.1f, playerClearanceRadius);
            playerClearanceHeight = Mathf.Max(playerClearanceRadius * 2f, playerClearanceHeight);
        }
    }
}
