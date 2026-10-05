using EvidenceRun.Core;

namespace EvidenceRun.Gameplay.Session
{
    public sealed class GameSession
    {
        private readonly GameEvents _gameEvents;

        private GameSessionState _state;
        public GameSessionState State => _state;

        private bool _hasEvidence;
        public bool HasEvidence => _hasEvidence;

        public GameSession(GameEvents gameEvents)
        {
            _gameEvents = gameEvents;
            _state = GameSessionState.Boot;

            _gameEvents.PlayerCaught += OnPlayerCaught;
            _gameEvents.EvidencePicked += OnEvidencePicked;
        }

        public void Start()
        {
            if (_state != GameSessionState.Boot)
            {
                return;
            }

            _state = GameSessionState.Playing;
        }

        public void Dispose()
        {
            _gameEvents.PlayerCaught -= OnPlayerCaught;
            _gameEvents.EvidencePicked -= OnEvidencePicked;
        }

        private void OnPlayerCaught(PlayerCaughtEvent eventData)
        {
            if (_state != GameSessionState.Playing)
            {
                return;
            }

            _state = GameSessionState.Lost;
        }

        public bool TryWin()
        {
            if (_state != GameSessionState.Playing)
            {
                return false;
            }

            _state = GameSessionState.Won;
            return true;
        }

        private void OnEvidencePicked(EvidencePickedEvent eventData)
      {
            if (_state != GameSessionState.Playing)
            {
                return;
            }

            _hasEvidence = true;
        }
    }
}