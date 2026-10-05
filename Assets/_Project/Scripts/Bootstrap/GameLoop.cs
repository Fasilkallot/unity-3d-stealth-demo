using EvidenceRun.Gameplay.Guards;
using EvidenceRun.Gameplay.Projectiles;
using EvidenceRun.Gameplay.Session;
using EvidenceRun.Presentation.Effects;
using EvidenceRun.Presentation.HUD;
using UnityEngine;

namespace EvidenceRun.Bootstrap
{
    public sealed class GameLoop : MonoBehaviour
    {
        private GuardSystem _guardSystem;
        private ProjectileSystem _projectileSystem;
        private ImpactEffectSystem _impactEffectSystem;
        private ObjectiveSystem _objectiveSystem;
        private Transform _playerTransform;
        private GameHUD _gameHUD;

        public void Initialize(
            Guard[] guards,
            ProjectileSystem projectileSystem,
            ImpactEffectSystem impactEffectSystem,
            ObjectiveSystem objectiveSystem,
            Transform playerTransform,
            GameHUD gameHUD)
        {
            _guardSystem = new GuardSystem(guards);
            _projectileSystem = projectileSystem;
            _impactEffectSystem = impactEffectSystem;
            _objectiveSystem = objectiveSystem;
            _playerTransform = playerTransform;
            _gameHUD = gameHUD;
        }

        private void Update()
        {
            _guardSystem?.Tick(Time.deltaTime);

            _projectileSystem?.Tick(
                Time.deltaTime,
                Physics.AllLayers);

            if (_objectiveSystem != null && _playerTransform != null)
            {
                _objectiveSystem.Tick(_playerTransform.position);
            }
        }
        private void LateUpdate()
        {
            _impactEffectSystem?.Tick();
            _gameHUD?.Tick();
        }
    }
}