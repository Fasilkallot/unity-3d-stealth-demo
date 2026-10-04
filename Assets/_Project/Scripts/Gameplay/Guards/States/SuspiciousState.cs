using EvidenceRun.Core;

namespace EvidenceRun.Gameplay.Guards.States
{
    public sealed class SuspiciousState : IState<GuardContext>
    {
        public void Enter(
            GuardContext context,
            IStateRequester stateRequester)
        {
            context.ResetStateTimer();

            if (context.HasLastKnownPlayerPosition)
            {
                context.Mover.SetDestination(
                    context.LastKnownPlayerPosition);
            }
        }

        public void Tick(
            GuardContext context,
            IStateRequester stateRequester,
            float deltaTime)
        {
            context.Mover.Tick(
                context.Position,
                deltaTime);

            if (context.Awareness.Value >= 1f)
            {
                stateRequester.Request(
                    (int)GuardStateId.Chase);

                return;
            }

            if (!context.Mover.HasArrived)
            {
                return;
            }

            context.TickStateTimer(deltaTime);

            if (context.Awareness.Value <
                context.Config.PatrolReturnThreshold &&
                context.StateTimer >=
                context.Config.InvestigationDuration)
            {
                stateRequester.Request(
                    (int)GuardStateId.Patrol);
            }
        }

        public void Exit(GuardContext context)
        {
            context.ResetStateTimer();
            context.Mover.ResumeRoute();
        }
    }
}