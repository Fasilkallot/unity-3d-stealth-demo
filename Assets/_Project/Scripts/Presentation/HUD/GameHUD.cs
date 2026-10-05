using EvidenceRun.Core;
using EvidenceRun.Gameplay.Guards;
using EvidenceRun.Gameplay.Session;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EvidenceRun.Presentation.HUD
{
    public sealed class GameHUD : MonoBehaviour
    {
        [Header("Objective")]
        [SerializeField] private TextMeshProUGUI objectiveText;

        [Header("Panels")]
        [SerializeField] private GameObject winPanel;
        [SerializeField] private GameObject losePanel;

        [Header("Alert")]
        [SerializeField] private GameObject alertIndicator;

        private GameSession _gameSession;
        private GameEvents _gameEvents;

        private GameSessionState _lastState;
        private bool _lastHasEvidence;

        private int _alertGuardCount;
        private bool _alertStateDirty;

        public void Initialize(
            GameSession gameSession,
            GameEvents gameEvents)
        {
            if (gameSession == null)
            {
                Debug.LogError(
                    $"{nameof(GameHUD)} requires a GameSession.",
                    this);

                return;
            }

            if (gameEvents == null)
            {
                Debug.LogError(
                    $"{nameof(GameHUD)} requires GameEvents.",
                    this);

                return;
            }

            _gameSession = gameSession;
            _gameEvents = gameEvents;

            _alertGuardCount = 0;
            _alertStateDirty = true;

            _lastState = GameSessionState.Boot;
            _lastHasEvidence = false;

            _gameEvents.GuardStateChanged += OnGuardStateChanged;

            ApplyState();
        }

        public void Tick()
        {
            if (_gameSession == null)
            {
                return;
            }

            if (_gameSession.State != _lastState ||
                _gameSession.HasEvidence != _lastHasEvidence ||
                _alertStateDirty)
            {
                ApplyState();
            }
        }

        public void Restart()
        {
            Scene activeScene = SceneManager.GetActiveScene();

            SceneManager.LoadScene(
                activeScene.buildIndex);
        }

        private void OnGuardStateChanged(
            GuardStateChangedEvent eventData)
        {
            bool wasAlert =
                IsAlertState(eventData.PreviousStateId);

            bool isAlert =
                IsAlertState(eventData.CurrentStateId);

            if (wasAlert == isAlert)
            {
                return;
            }

            if (isAlert)
            {
                _alertGuardCount++;
            }
            else
            {
                _alertGuardCount--;

                if (_alertGuardCount < 0)
                {
                    _alertGuardCount = 0;
                }
            }

            _alertStateDirty = true;
        }

        private static bool IsAlertState(int stateId)
        {
            return stateId == (int)GuardStateId.Suspicious ||
                   stateId == (int)GuardStateId.Chase;
        }

        private void ApplyState()
        {
            _lastState = _gameSession.State;
            _lastHasEvidence = _gameSession.HasEvidence;

            bool playing =
                _gameSession.State == GameSessionState.Playing;

            bool won =
                _gameSession.State == GameSessionState.Won;

            bool lost =
                _gameSession.State == GameSessionState.Lost;

            if (winPanel != null)
            {
                winPanel.SetActive(won);
            }

            if (losePanel != null)
            {
                losePanel.SetActive(lost);
            }

            if (alertIndicator != null)
            {
                alertIndicator.SetActive(
                    playing && _alertGuardCount > 0);
            }

            if (objectiveText != null)
            {
                if (playing)
                {
                    objectiveText.text = _gameSession.HasEvidence
                        ? "Return to extraction."
                        : "Find the evidence.";
                }
                else
                {
                    objectiveText.text = string.Empty;
                }
            }

            _alertStateDirty = false;
        }

        private void OnDestroy()
        {
            if (_gameEvents != null)
            {
                _gameEvents.GuardStateChanged -=
                    OnGuardStateChanged;
            }
        }
    }
}