using EvidenceRun.Core;
using UnityEngine;

namespace EvidenceRun.Gameplay.Session
{
    public sealed class ObjectiveSystem
    {
        private readonly GameEvents _gameEvents;
        private readonly GameSession _gameSession;

        private readonly Vector3 _evidencePosition;
        private readonly Vector3 _extractionPosition;

        private readonly float _evidencePickupRadiusSqr;
        private readonly float _extractionRadiusSqr;

        public ObjectiveSystem(
            GameEvents gameEvents,
            GameSession gameSession,
            Vector3 evidencePosition,
            Vector3 extractionPosition,
            ObjectiveConfig config)
        {
            _gameEvents = gameEvents;
            _gameSession = gameSession;

            _evidencePosition = evidencePosition;
            _extractionPosition = extractionPosition;

            _evidencePickupRadiusSqr =
                config.EvidencePickupRadius *
                config.EvidencePickupRadius;

            _extractionRadiusSqr =
                config.ExtractionRadius *
                config.ExtractionRadius;
        }

        public void Tick(Vector3 playerPosition)
        {
            if (_gameSession.State != GameSessionState.Playing)
            {
                return;
            }

            if (!_gameSession.HasEvidence)
            {
                if (IsWithinRadius(
                        playerPosition,
                        _evidencePosition,
                        _evidencePickupRadiusSqr))
                {
                    _gameEvents.PublishEvidencePicked();
                }

                return;
            }

            if (IsWithinRadius(
                    playerPosition,
                    _extractionPosition,
                    _extractionRadiusSqr))
            {
                _gameSession.TryWin();
            }
        }

        private static bool IsWithinRadius(
            Vector3 position,
            Vector3 target,
            float radiusSqr)
        {
            return (position - target).sqrMagnitude <= radiusSqr;
        }
    }
}