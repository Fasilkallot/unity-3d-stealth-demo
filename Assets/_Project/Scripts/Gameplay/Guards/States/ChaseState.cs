using EvidenceRun.Core;
using EvidenceRun.Gameplay.Guards.Perception;
using UnityEngine;

namespace EvidenceRun.Gameplay.Guards.States
{
    public sealed class ChaseState : IState<GuardContext>
    {
        public void Enter(
            GuardContext context,
            IStateRequester stateRequester)
        {
            context.ResetStateTimer();
        }

        public void Tick(
            GuardContext context,
            IStateRequester stateRequester,
            float deltaTime)
        {
            SightResult sight = context.LastSightResult;

            // Catch check.
            Vector3 toTarget =
                context.Target.position - context.Position;

            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <=
                context.Config.CatchRadiusSqr)
            {
                stateRequester.Request(
                    (int)GuardStateId.Caught);

                return;
            }

            if (sight.Visible)
            {
                context.ResetStateTimer();

                context.SetLastKnownPlayerPosition(
                    context.Target.position);

                context.Mover.SetDestination(
                    context.Target.position);
            }
            else
            {
                context.TickStateTimer(deltaTime);

                if (context.StateTimer >=
                    context.Config.ChaseLoseSightDuration)
                {
                    stateRequester.Request(
                        (int)GuardStateId.Suspicious);

                    return;
                }

                if (context.HasLastKnownPlayerPosition)
                {
                    context.Mover.SetDestination(
                        context.LastKnownPlayerPosition);
                }
            }

            context.Mover.Tick(
                context.Position,
                deltaTime);
        }

        public void Exit(GuardContext context)
        {
            context.ResetStateTimer();
        }
    }
}