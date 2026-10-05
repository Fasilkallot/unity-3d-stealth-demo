using EvidenceRun.Core;
using UnityEngine;

namespace EvidenceRun.Gameplay.Noise
{
    public sealed class DebugNoiseEmitter : MonoBehaviour
    {
        [Header("Noise")]
        [SerializeField, Min(0f)]
        private float noiseRadius = 8f;

        [SerializeField]
        private KeyCode emitKey = KeyCode.N;

        private NoiseBus _noiseBus;

        public void Initialize(NoiseBus noiseBus)
        {
            if (noiseBus == null)
            {
                Debug.LogError(
                    $"{nameof(DebugNoiseEmitter)} requires a NoiseBus.",
                    this);

                enabled = false;
                return;
            }

            _noiseBus = noiseBus;
        }

        private void Update()
        {
            if (!Input.GetKeyDown(emitKey))
            {
                return;
            }

            NoiseEvent noiseEvent = new NoiseEvent(
                transform.position,
                noiseRadius,
                this);

            _noiseBus.Emit(in noiseEvent);
        }
    }
}