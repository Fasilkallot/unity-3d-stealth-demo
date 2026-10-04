namespace EvidenceRun.Core
{
    public sealed class StateMachine<TContext> : IStateRequester
    {
        private readonly IState<TContext>[] _states;

        private int _current = -1;
        private int _pending = -1;

        public int Current => _current;

        public StateMachine(IState<TContext>[] states)
        {
            _states = states;
        }

        public void Request(int stateId)
        {
            _pending = stateId;
        }

        public void Tick(
            TContext context,
            float deltaTime)
        {
            if (_pending >= 0)
            {
                if (_current >= 0)
                {
                    _states[_current].Exit(context);
                }

                _current = _pending;
                _pending = -1;

                _states[_current].Enter(
                    context,
                    this);
            }

            if (_current >= 0)
            {
                _states[_current].Tick(
                    context,
                    this,
                    deltaTime);
            }
        }
    }
}