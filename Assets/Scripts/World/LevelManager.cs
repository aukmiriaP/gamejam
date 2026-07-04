using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnchorGame
{
    public class LevelManager : MonoBehaviour
    {
        private const string FallbackNextSceneName = "Level_01";

        [Header("关卡切换")]
        [SerializeField] private string nextSceneName = FallbackNextSceneName;

        private static LevelManager _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        public static void LoadNextLevel()
        {
            Time.timeScale = 1f;

            if (_instance != null && !string.IsNullOrWhiteSpace(_instance.nextSceneName))
            {
                SceneManager.LoadScene(_instance.nextSceneName);
                return;
            }

            int activeBuildIndex = SceneManager.GetActiveScene().buildIndex;
            if (activeBuildIndex >= 0 && activeBuildIndex + 1 < SceneManager.sceneCountInBuildSettings)
            {
                SceneManager.LoadScene(activeBuildIndex + 1);
                return;
            }

            if (Application.CanStreamedLevelBeLoaded(FallbackNextSceneName))
            {
                SceneManager.LoadScene(FallbackNextSceneName);
                return;
            }

            Debug.LogWarning("[LevelManager] 没有可加载的下一关，请检查 Build Settings 或 nextSceneName。");
        }
    }
}
