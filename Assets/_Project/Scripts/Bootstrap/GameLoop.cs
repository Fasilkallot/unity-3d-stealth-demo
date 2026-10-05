using EvidenceRun.Gameplay.Guards;
using EvidenceRun.Gameplay.Projectiles;
using UnityEngine;

namespace EvidenceRun.Bootstrap
{
    public sealed class GameLoop : MonoBehaviour
    {
        private GuardSystem _guardSystem;
        private ProjectileSystem _projectileSystem;

        public void Initialize(
            Guard[] guards,
            ProjectileSystem projectileSystem)
        {
            _guardSystem = new GuardSystem(guards);
            _projectileSystem = projectileSystem;
        }

        private void Update()
        {
            _guardSystem?.Tick(Time.deltaTime);

            _projectileSystem?.Tick(
                Time.deltaTime,
                Physics.AllLayers);
        }
    }
}