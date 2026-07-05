using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnchorGame
{
    public class LevelManager : MonoBehaviour
    {
        private const string MainMenuSceneName = "MainMenu";
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string TutorialSceneName = "TutorialLevel";
        private const string Level02SceneName = "Level_02";
        private const string Level02ScenePath = "Assets/Scenes/Level_02.unity";

        [Header("关卡切换")]
        [SerializeField] private string nextSceneName = Level02SceneName;

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

            string nextScene = GetNextSceneName();
            if (TryGetLoadableSceneIdentifier(nextScene, out string sceneIdentifier))
            {
                SceneManager.LoadScene(sceneIdentifier);
                return;
            }

            Debug.LogWarning($"[LevelManager] 没有可加载的下一关，请检查 Build Settings 或 nextSceneName。目标场景: {nextScene}");
        }

        public static void LoadMainMenu()
        {
            Time.timeScale = 1f;

            if (TryGetLoadableSceneIdentifier(MainMenuSceneName, out string sceneIdentifier))
            {
                SceneManager.LoadScene(sceneIdentifier);
                return;
            }

            Debug.LogWarning($"[LevelManager] 找不到主界面场景，请检查 Build Settings: {MainMenuSceneName}");
        }

        public static bool HasNextLevel()
        {
            string nextScene = GetNextSceneName();
            return TryGetLoadableSceneIdentifier(nextScene, out _);
        }

        public static string GetCompletionWindowTitle()
        {
            return SceneManager.GetActiveScene().name == TutorialSceneName ? "教学关完成" : "关卡完成";
        }

        public static string GetCompletionMessage()
        {
            return HasNextLevel() ? "恭喜已完成教学，是否进入下一关？" : "恭喜已完成本关！";
        }

        private static string GetNextSceneName()
        {
            string activeSceneName = SceneManager.GetActiveScene().name;
            if (activeSceneName == TutorialSceneName)
            {
                return Level02SceneName;
            }

            if (activeSceneName == Level02SceneName)
            {
                return string.Empty;
            }

            return _instance != null ? _instance.nextSceneName : string.Empty;
        }

        private static bool TryGetLoadableSceneIdentifier(string sceneName, out string sceneIdentifier)
        {
            sceneIdentifier = string.Empty;
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                sceneIdentifier = sceneName;
                return true;
            }

            string scenePath = GetKnownScenePath(sceneName);
            if (!string.IsNullOrWhiteSpace(scenePath) && SceneUtility.GetBuildIndexByScenePath(scenePath) >= 0)
            {
                sceneIdentifier = scenePath;
                return true;
            }

            return false;
        }

        private static string GetKnownScenePath(string sceneName)
        {
            if (sceneName == MainMenuSceneName)
            {
                return MainMenuScenePath;
            }

            if (sceneName == Level02SceneName)
            {
                return Level02ScenePath;
            }

            return string.Empty;
        }
    }
}
