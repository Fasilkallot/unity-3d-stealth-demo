using EvidenceRun.Gameplay.Guards;
using UnityEngine;

namespace EvidenceRun.Presentation.VisionCone
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class VisionConeRenderer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Guard guard;

        [Header("Rendering")]
        [SerializeField, Range(10, 60)]
        private int rayCount = 40;

        [SerializeField, Min(0.01f)]
        private float updateInterval = 1f / 30f;

        [SerializeField]
        private LayerMask visionBlockingMask;

        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;

        private Vector3[] _vertices;
        private int[] _triangles;

        private Vector3[] _directions;

        private float _updateTimer;

        private MaterialPropertyBlock _propertyBlock;

        private int _vertexCount;

        private static readonly int ColorProperty =
            Shader.PropertyToID("_Color");

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            InitializeBuffers();
            InitializeMesh();

            _propertyBlock = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            _updateTimer += Time.deltaTime;

            if (_updateTimer < updateInterval)
            {
                return;
            }

            _updateTimer -= updateInterval;

            UpdateCone();
            UpdateColor();
        }

        private void InitializeBuffers()
        {
            _vertexCount = rayCount + 2;

            _vertices = new Vector3[_vertexCount];
            _triangles = new int[rayCount * 3];
            _directions = new Vector3[rayCount + 1];

            float halfFov =
                guard.Config.FieldOfView * 0.5f;

            for (int i = 0; i <= rayCount; i++)
            {
                float t = i / (float)rayCount;

                float angle =
                    Mathf.Lerp(-halfFov, halfFov, t);

                float radians =
                    angle * Mathf.Deg2Rad;

                float sin = Mathf.Sin(radians);
                float cos = Mathf.Cos(radians);

                _directions[i] =
                    new Vector3(sin, 0f, cos);
            }

            for (int i = 0; i < rayCount; i++)
            {
                int triangleIndex = i * 3;

                _triangles[triangleIndex] = 0;
                _triangles[triangleIndex + 1] = i + 1;
                _triangles[triangleIndex + 2] = i + 2;
            }
        }

        private void InitializeMesh()
        {
            _mesh = new Mesh
            {
                name = "VisionCone"
            };

            _mesh.MarkDynamic();

            _mesh.vertices = _vertices;
            _mesh.triangles = _triangles;

            _meshFilter.sharedMesh = _mesh;
        }

        private void UpdateCone()
        {
            Transform guardTransform = guard.transform;

            Vector3 eyePosition =
                guardTransform.position +
                Vector3.up * guard.SightSensor.EyeHeight;

            _vertices[0] =
                guardTransform.InverseTransformPoint(
                    eyePosition);

            float maxRange = guard.Config.VisionRange;

            for (int i = 0; i <= rayCount; i++)
            {
                Vector3 worldDirection =
                    guardTransform.TransformDirection(
                        _directions[i]);

                Vector3 endpoint =
                    eyePosition +
                    worldDirection * maxRange;

                if (Physics.Raycast(
                        eyePosition,
                        worldDirection,
                        out RaycastHit hit,
                        maxRange,
                        visionBlockingMask,
                        QueryTriggerInteraction.Ignore))
                {
                    endpoint = hit.point;
                }

                _vertices[i + 1] =
                    guardTransform.InverseTransformPoint(
                        endpoint);
            }

            _mesh.SetVertices(
                _vertices,
                0,
                _vertexCount);
        }

        private void UpdateColor()
        {
            float awareness =
                guard.AwarenessValue;

            Color color;

            if (awareness < 0.4f)
            {
                color = Color.white;
            }
            else if (awareness < 1f)
            {
                color = Color.yellow;
            }
            else
            {
                color = Color.red;
            }

            _meshRenderer.GetPropertyBlock(
                _propertyBlock);

            _propertyBlock.SetColor(
                ColorProperty,
                color);

            _meshRenderer.SetPropertyBlock(
                _propertyBlock);
        }
    }
}