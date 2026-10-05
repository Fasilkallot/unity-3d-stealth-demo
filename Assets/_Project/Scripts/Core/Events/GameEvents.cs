using System;

namespace EvidenceRun.Core
{
    public sealed class GameEvents
    {
        public event Action<PlayerCaughtEvent> PlayerCaught;
        public event Action<GuardStateChangedEvent> GuardStateChanged;
        public event Action<ProjectileImpactEvent> ProjectileImpact;

        public void PublishPlayerCaught()
        {
            PlayerCaught?.Invoke(new PlayerCaughtEvent());
        }

        public void PublishGuardStateChanged(
            in GuardStateChangedEvent eventData)
        {
            GuardStateChanged?.Invoke(eventData);
        }
        public void PublishProjectileImpact(in ProjectileImpactEvent eventData)
        {
            ProjectileImpact?.Invoke(eventData);
        }
    }
}