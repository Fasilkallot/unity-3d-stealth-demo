using EvidenceRun.Core;
using EvidenceRun.Gameplay.Actors;
using UnityEngine;

namespace EvidenceRun.Presentation.Characters
{
    public sealed class CharacterAnimationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ActorMotor actorMotor;
        [SerializeField] private Animator animator;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int CrouchHash = Animator.StringToHash("Crouch");
        private static readonly int ActionHash = Animator.StringToHash("Action");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");

        private float _lastSpeed;
        private bool _lastCrouch;
        private bool _lastThrow;
        private bool _lastGrounded;
        private bool _forceSync;
        private bool _isValid;

        private void Awake()
        {
            if (actorMotor == null)
            {
                DisableWithError($"requires an assigned {nameof(ActorMotor)}");
                return;
            }

            if (animator == null)
            {
                DisableWithError("requires an assigned Animator");
                return;
            }

            if (animator.runtimeAnimatorController == null)
            {
                DisableWithError("requires a Runtime Animator Controller on the assigned Animator");
                return;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            _isValid =
                ValidateParameter(parameters, SpeedHash, "Speed", AnimatorControllerParameterType.Float) &&
                ValidateParameter(parameters, CrouchHash, "Crouch", AnimatorControllerParameterType.Bool) &&
                ValidateParameter(parameters, ActionHash, "Action", AnimatorControllerParameterType.Trigger) &&
                ValidateParameter(parameters, JumpHash, "Jump", AnimatorControllerParameterType.Trigger) &&
                ValidateParameter(parameters, IsGroundedHash, "IsGrounded", AnimatorControllerParameterType.Bool);
        }

        private void OnEnable()
        {
            if (!_isValid)
            {
                return;
            }

            actorMotor.Jumped += OnJumped;
            _forceSync = true;
            _lastThrow = false;
            animator.ResetTrigger(JumpHash);
            animator.ResetTrigger(ActionHash);
        }

        private void OnDisable()
        {
            if (actorMotor != null)
            {
                actorMotor.Jumped -= OnJumped;
            }
        }

        private void Update()
        {
            // Use the intent submitted by PlayerController after its session-state check.
            ActorIntent intent = actorMotor.CurrentIntent;

            UpdateLocomotion(intent);
            UpdateCrouch(intent);
            UpdateAction(intent);
            UpdateGrounded();
            _forceSync = false;
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

            if (_forceSync || !Mathf.Approximately(speed, _lastSpeed))
            {
                animator.SetFloat(SpeedHash, speed);
                _lastSpeed = speed;
            }
        }

        private void UpdateCrouch(in ActorIntent intent)
        {
            if (!_forceSync && intent.Crouch == _lastCrouch)
            {
                return;
            }

            animator.SetBool(CrouchHash, intent.Crouch);
            _lastCrouch = intent.Crouch;
        }

        private void UpdateAction(in ActorIntent intent)
        {
            if (intent.Throw && !_lastThrow && actorMotor.IsGrounded)
            {
                animator.SetTrigger(ActionHash);
            }

            _lastThrow = intent.Throw;
        }

        private void UpdateGrounded()
        {
            bool isGrounded = actorMotor.IsGrounded;

            if (_forceSync || isGrounded != _lastGrounded)
            {
                animator.SetBool(IsGroundedHash, isGrounded);
                _lastGrounded = isGrounded;
            }
        }

        private void OnJumped()
        {
            // Jump takes priority over a pending full-body throw.
            animator.ResetTrigger(ActionHash);
            animator.SetBool(IsGroundedHash, false);
            _lastGrounded = false;
            animator.SetTrigger(JumpHash);
        }

        private bool ValidateParameter(
            AnimatorControllerParameter[] parameters,
            int hash,
            string parameterName,
            AnimatorControllerParameterType type)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == hash && parameters[i].type == type)
                {
                    return true;
                }
            }

            DisableWithError($"requires Animator parameter '{parameterName}' of type {type}");
            return false;
        }

        private void DisableWithError(string reason)
        {
            Debug.LogError($"{nameof(CharacterAnimationController)} {reason}. Animation driver disabled.", this);
            enabled = false;
        }
    }
}
