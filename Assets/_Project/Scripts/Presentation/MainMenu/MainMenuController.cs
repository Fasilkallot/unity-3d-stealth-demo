using UnityEngine;
using UnityEngine.SceneManagement;

namespace EvidenceRun.Presentation.MainMenu
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private string gameplaySceneName = "EvidenceRun";

        public void StartMission()
        {
            if (string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                Debug.LogError(
                    $"{nameof(MainMenuController)} requires a gameplay scene name.",
                    this);

                return;
            }
            
            SceneManager.LoadScene(gameplaySceneName);
        }
    }
}