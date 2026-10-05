using EvidenceRun.Gameplay.Guards;
using EvidenceRun.Gameplay.Projectiles;
using EvidenceRun.Presentation.Effects;
using UnityEngine;

namespace EvidenceRun.Bootstrap
{
    public sealed class GameLoop : MonoBehaviour
    {
        private GuardSystem _guardSystem;
        private ProjectileSystem _projectileSystem;
        private ImpactEffectSystem _impactEffectSystem;

        public void Initialize(
            Guard[] guards,
            ProjectileSystem projectileSystem,
            ImpactEffectSystem impactEffectSystem)
        {
            _guardSystem = new GuardSystem(guards);
            _projectileSystem = projectileSystem;
            _impactEffectSystem = impactEffectSystem;
        }

        private void Update()
        {
            _guardSystem?.Tick(Time.deltaTime);

            _projectileSystem?.Tick(
                Time.deltaTime,
                Physics.AllLayers);
        }
        private void LateUpdate()
        {
            _impactEffectSystem?.Tick();
        }
    }
}