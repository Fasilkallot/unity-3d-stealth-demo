using UnityEngine;
using UnityEngine.SceneManagement;

namespace EvidenceRun.Presentation.MainMenu
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private string gameplaySceneName = "EvidenceRun";

        private void Awake()
        {
            ShowCursor();
        }

        public void StartMission()
        {
            if (string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                Debug.LogError(
                    $"{nameof(MainMenuController)} requires a gameplay scene name.",
                    this);

                return;
            }
            
            ShowCursor();
            SceneManager.LoadScene(gameplaySceneName);
        }

        private static void ShowCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
