using EvidenceRun.Core;
using EvidenceRun.Gameplay.Player;
using UnityEngine;

namespace EvidenceRun.Presentation.Characters
{
    public sealed class CharacterAnimationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private Animator animator;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int CrouchHash = Animator.StringToHash("Crouch");
        private static readonly int ActionHash = Animator.StringToHash("Action");

        private float _lastSpeed = -1f;
        private bool _lastCrouch;
        private bool _lastThrow;

        private void Awake()
        {
            if (inputReader == null)
            {
                Debug.LogError(
                    $"{nameof(CharacterAnimationController)} requires a {nameof(PlayerInputReader)}.",
                    this);

                enabled = false;
                return;
            }

            if (animator == null)
            {
                Debug.LogError(
                    $"{nameof(CharacterAnimationController)} requires an Animator.",
                    this);

                enabled = false;
            }
        }

        private void Update()
        {
            ActorIntent intent = inputReader.Intent;

            UpdateLocomotion(intent);
            UpdateCrouch(intent);
            UpdateAction(intent);
        }

        private void UpdateLocomotion(in ActorIntent intent)
        {
            float movementMagnitude = intent.Move.magnitude;

            float speed = 0f;

            if (movementMagnitude > 0.01f)
            {
                speed = intent.Run ? 1f : 0.5f;

                if (intent.Crouch)
                {
                    speed = movementMagnitude;
                }
            }

            if (!Mathf.Approximately(speed, _lastSpeed))
            {
                animator.SetFloat(SpeedHash, speed);
                _lastSpeed = speed;
            }
        }

        private void UpdateCrouch(in ActorIntent intent)
        {
            if (intent.Crouch == _lastCrouch)
            {
                return;
            }

            animator.SetBool(CrouchHash, intent.Crouch);
            _lastCrouch = intent.Crouch;
        }

        private void UpdateAction(in ActorIntent intent)
        {
            if (intent.Throw && !_lastThrow)
            {
                animator.SetTrigger(ActionHash);
            }

            _lastThrow = intent.Throw;
        }
    }
}