using System;

namespace EvidenceRun.Core
{
    public sealed class GameEvents
    {
        public event Action<PlayerCaughtEvent> PlayerCaught;
        public event Action<GuardStateChangedEvent> GuardStateChanged;

        public void PublishPlayerCaught()
        {
            PlayerCaught?.Invoke(new PlayerCaughtEvent());
        }

        public void PublishGuardStateChanged(
            in GuardStateChangedEvent eventData)
        {
            GuardStateChanged?.Invoke(eventData);
        }
    }
}