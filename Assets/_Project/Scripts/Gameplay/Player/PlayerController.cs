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

        private void Awake()
        {
            actorMotor.Initialize(playerConfig);
        }

        private void LateUpdate()
        {
            actorMotor.SetIntent(inputReader.Intent);
        }
    }
}