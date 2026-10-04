using EvidenceRun.Core;

namespace EvidenceRun.Gameplay.Guards.States
{
    public sealed class PatrolState : IState<GuardContext>
    {
        public void Enter(
            GuardContext context,
            IStateRequester stateRequester)
        {
            context.ResetStateTimer();
            context.Mover.ResumeRoute();
        }

        public void Tick(GuardContext context, IStateRequester stateRequester, float deltaTime)
        {
            context.Mover.Tick(
                context.Position,
                deltaTime);

            context.TickStateTimer(deltaTime);

            if (context.Awareness.Value >=
                context.Config.SuspiciousThreshold)
            {
                stateRequester.Request(
                    (int)GuardStateId.Suspicious);
            }
        }

        public void Exit(GuardContext context)
        {
        }
    }
}