using EvidenceRun.Core;
using EvidenceRun.Gameplay.Guards;
using UnityEngine;

namespace EvidenceRun.Presentation.Characters
{
    public sealed class GuardAnimationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Guard guard;
        [SerializeField] private Animator animator;

        private static readonly int SpeedHash =
            Animator.StringToHash("Speed");

        private float _lastSpeed = -1f;

        private void Awake()
        {
            if (guard == null)
            {
                Debug.LogError(
                    $"{nameof(GuardAnimationController)} requires a {nameof(Guard)}.",
                    this);

                enabled = false;
                return;
            }

            if (animator == null)
            {
                Debug.LogError(
                    $"{nameof(GuardAnimationController)} requires an Animator.",
                    this);

                enabled = false;
            }
        }

        private void LateUpdate()
        {
            ActorIntent intent = guard.CurrentIntent;

            float speed = 0f;

            if (intent.Move.sqrMagnitude > 0.0001f)
            {
                speed = intent.Run ? 1f : 0.5f;
            }

            if (Mathf.Approximately(speed, _lastSpeed))
            {
                return;
            }

            animator.SetFloat(SpeedHash, speed);
            _lastSpeed = speed;
        }
    }
}