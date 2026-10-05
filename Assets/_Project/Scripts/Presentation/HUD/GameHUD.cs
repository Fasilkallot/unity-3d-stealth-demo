using EvidenceRun.Gameplay.Session;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

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

        private GameSessionState _lastState;
        private bool _lastHasEvidence;

        public void Initialize(GameSession gameSession)
        {
            if (gameSession == null)
            {
                Debug.LogError(
                    $"{nameof(GameHUD)} requires a GameSession.",
                    this);

                return;
            }

            _gameSession = gameSession;

            _lastState = GameSessionState.Boot;
            _lastHasEvidence = false;

            ApplyState();
        }

        public void Tick()
        {
            if (_gameSession == null)
            {
                return;
            }

            if (_gameSession.State != _lastState ||
                _gameSession.HasEvidence != _lastHasEvidence)
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
                alertIndicator.SetActive(false);
            }

            if (objectiveText == null || !playing)
            {
                return;
            }

            objectiveText.text = _gameSession.HasEvidence
                ? "Return to extraction."
                : "Find the evidence.";
        }
    }
}