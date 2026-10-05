using System;

namespace EvidenceRun.Core
{
    public sealed class GameEvents
    {
        public event Action<PlayerCaughtEvent> PlayerCaught;
        public event Action<GuardStateChangedEvent> GuardStateChanged;
        public event Action<ProjectileImpactEvent> ProjectileImpact;
        public event Action<EvidencePickedEvent> EvidencePicked;

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
        public void PublishEvidencePicked()
        {
            EvidencePicked?.Invoke(new EvidencePickedEvent());
        }
    }
}