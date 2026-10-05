using EvidenceRun.Core;
using EvidenceRun.Gameplay.Actors;
using UnityEngine;

namespace EvidenceRun.Gameplay.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private ActorMotor actorMotor;
        [SerializeField] private PlayerConfig playerConfig;
        [SerializeField] private Transform throwOrigin;

        private IWeapon _weapon;

        private void Awake()
        {
            actorMotor.Initialize(playerConfig);
        }

        public void Initialize(IWeapon weapon)
        {
            if (weapon == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerController)} requires a weapon.",
                    this);

                enabled = false;
                return;
            }

            if (throwOrigin == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerController)} requires a throw origin.",
                    this);

                enabled = false;
                return;
            }

            _weapon = weapon;
        }

        private void LateUpdate()
        {
            ActorIntent intent = inputReader.Intent;

            actorMotor.SetIntent(intent);

            if (intent.Throw)
            {
                _weapon.TryUse(
                    throwOrigin.position,
                    intent.AimPoint,
                    gameObject);
            }
        }
    }
}