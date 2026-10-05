using EvidenceRun.Core;
using EvidenceRun.Gameplay.Guards;
using UnityEngine;

namespace EvidenceRun.Bootstrap
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Guard[] guards;

        [SerializeField] private GameLoop gameLoop;

        private GameEvents _gameEvents;

        public GameEvents GameEvents => _gameEvents;

        private void Awake()
        {
            _gameEvents = new GameEvents();

            for (int i = 0; i < guards.Length; i++)
            {
                guards[i].Initialize(_gameEvents);
            }

            gameLoop.Initialize(guards);
        }
    }
}