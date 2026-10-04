namespace EvidenceRun.Core
{
    public interface IState<TContext>
    {
        void Enter(
            TContext context,
            IStateRequester stateRequester);

        void Tick(
            TContext context,
            IStateRequester stateRequester,
            float deltaTime);

        void Exit(TContext context);
    }
}