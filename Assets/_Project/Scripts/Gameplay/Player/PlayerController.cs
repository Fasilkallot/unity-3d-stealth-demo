using EvidenceRun.Core;
using EvidenceRun.Gameplay.Actors;
using EvidenceRun.Gameplay.Session;
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
        private GameSession _gameSession;

        private void Awake()
        {
            actorMotor.Initialize(playerConfig);
        }

        public void Initialize(IWeapon weapon, GameSession gameSession)
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

            if (gameSession == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerController)} requires a GameSession.",
                    this);

                return;
            }

            _weapon = weapon;
            _gameSession = gameSession;
        }

        private void LateUpdate()
        {
            if (_gameSession == null)
            {
                return;
            }

            if (_gameSession.State != GameSessionState.Playing)
            {
                actorMotor.SetIntent(default);
                actorMotor.Stop();
                return;
            }

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