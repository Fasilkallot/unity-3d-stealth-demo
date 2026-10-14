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
        [SerializeField] private TextMeshProUGUI objectiveLabel;
        [SerializeField] private GameObject gameplayHud;

        [Header("Panels")]
        [SerializeField] private GameObject winPanel;
        [SerializeField] private GameObject losePanel;

        [Header("Alert")]
        [SerializeField] private GameObject alertIndicator;
        [SerializeField] private TextMeshProUGUI alertText;
        [SerializeField] private Image alertAccent;
        [SerializeField] private Color suspiciousColor = new Color32(196, 155, 85, 255);
        [SerializeField] private Color chaseColor = new Color32(183, 106, 92, 255);

        [Header("Evidence feedback")]
        [SerializeField] private GameObject evidenceFeedback;
        [SerializeField] private TextMeshProUGUI evidenceFeedbackText;
        [SerializeField, Min(0f)] private float feedbackDurationSeconds = 4f;

        [Header("Navigation")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private GameSession _gameSession;
        private GameEvents _gameEvents;

        private GameSessionState _lastState;
        private bool _lastHasEvidence;

        private int _suspiciousGuardCount;
        private int _chasingGuardCount;
        private bool _alertStateDirty;
        private bool _feedbackVisible;
        private float _feedbackExpiresAt;

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

            if (_gameSession == gameSession && _gameEvents == gameEvents)
            {
                return;
            }

            Unsubscribe();
            _gameSession = gameSession;
            _gameEvents = gameEvents;

            _suspiciousGuardCount = 0;
            _chasingGuardCount = 0;
            _alertStateDirty = true;
            HideEvidenceFeedback();

            _gameEvents.GuardStateChanged += OnGuardStateChanged;
            _gameEvents.EvidencePicked += OnEvidencePicked;

            ApplySessionState();
            ApplyAlert();
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
                ApplySessionState();
            }

            if (_alertStateDirty)
            {
                ApplyAlert();
            }

            if (_feedbackVisible && Time.unscaledTime >= _feedbackExpiresAt)
            {
                HideEvidenceFeedback();
            }
        }

        // Preserve existing persistent button connections in older scenes.
        public void Restart()
        {
            RestartMission();
        }

        public void RestartMission()
        {
            ShowCursor();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void ReturnToMainMenu()
        {
            if (string.IsNullOrWhiteSpace(mainMenuSceneName))
            {
                Debug.LogError(
                    $"{nameof(GameHUD)} requires a main menu scene name.",
                    this);
                return;
            }

            ShowCursor();
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void OnGuardStateChanged(
            GuardStateChangedEvent eventData)
        {
            if (eventData.PreviousStateId == eventData.CurrentStateId)
            {
                return;
            }

            UpdateAlertCount(eventData.PreviousStateId, -1);
            UpdateAlertCount(eventData.CurrentStateId, 1);
            _alertStateDirty = true;
        }

        private void UpdateAlertCount(int stateId, int change)
        {
            if (stateId == (int)GuardStateId.Suspicious)
            {
                _suspiciousGuardCount = Mathf.Max(0, _suspiciousGuardCount + change);
            }
            else if (stateId == (int)GuardStateId.Chase)
            {
                _chasingGuardCount = Mathf.Max(0, _chasingGuardCount + change);
            }
        }

        private void OnEvidencePicked(EvidencePickedEvent eventData)
        {
            if (_gameSession.State != GameSessionState.Playing)
            {
                return;
            }

            ApplySessionState();
            if (evidenceFeedback == null || feedbackDurationSeconds <= 0f)
            {
                return;
            }

            if (evidenceFeedbackText != null)
            {
                evidenceFeedbackText.text = "Evidence secured. Proceed to extraction.";
            }

            _feedbackVisible = true;
            _feedbackExpiresAt = Time.unscaledTime + feedbackDurationSeconds;
            evidenceFeedback.SetActive(true);
        }

        private void ApplySessionState()
        {
            _lastState = _gameSession.State;
            _lastHasEvidence = _gameSession.HasEvidence;

            bool playing =
                _gameSession.State == GameSessionState.Playing;

            bool won =
                _gameSession.State == GameSessionState.Won;

            bool lost =
                _gameSession.State == GameSessionState.Lost;

            if (gameplayHud != null)
            {
                gameplayHud.SetActive(playing);
            }

            if (winPanel != null)
            {
                winPanel.SetActive(won);
            }

            if (losePanel != null)
            {
                losePanel.SetActive(lost);
            }

            if (objectiveText != null)
            {
                objectiveText.text = !playing ? string.Empty :
                    _lastHasEvidence ? "Proceed to extraction" : "Locate the evidence";
            }

            if (objectiveLabel != null)
            {
                objectiveLabel.text = !playing ? string.Empty :
                    _lastHasEvidence ? "OBJECTIVE 02" : "OBJECTIVE 01";
            }

            if (!playing)
            {
                HideEvidenceFeedback();
            }

            // The existing camera uses LMB dragging, so it needs an unlocked cursor.
            ShowCursor();
            _alertStateDirty = true;
        }

        private void ApplyAlert()
        {
            bool chasing = _chasingGuardCount > 0;
            bool alerted = chasing || _suspiciousGuardCount > 0;
            if (alertIndicator != null)
            {
                alertIndicator.SetActive(
                    _gameSession.State == GameSessionState.Playing && alerted);
            }

            Color color = chasing ? chaseColor : suspiciousColor;
            if (alertText != null)
            {
                alertText.text = chasing ? "GUARD PURSUIT" : "SUSPICIOUS ACTIVITY";
                alertText.color = color;
            }

            if (alertAccent != null)
            {
                alertAccent.color = color;
            }

            _alertStateDirty = false;
        }

        private void HideEvidenceFeedback()
        {
            _feedbackVisible = false;
            if (evidenceFeedback != null)
            {
                evidenceFeedback.SetActive(false);
            }
        }

        private static void ShowCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Unsubscribe()
        {
            if (_gameEvents != null)
            {
                _gameEvents.GuardStateChanged -=
                    OnGuardStateChanged;
                _gameEvents.EvidencePicked -= OnEvidencePicked;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}
