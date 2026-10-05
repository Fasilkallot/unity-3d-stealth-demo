namespace EvidenceRun.Core
{
    public readonly struct GuardStateChangedEvent
    {
        public readonly int PreviousStateId;
        public readonly int CurrentStateId;

        public GuardStateChangedEvent(
            int previousStateId,
            int currentStateId)
        {
            PreviousStateId = previousStateId;
            CurrentStateId = currentStateId;
        }
    }
}