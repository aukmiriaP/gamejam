using UnityEngine;
using UnityEngine.SceneManagement; // 必须引入这个命名空间

public class SceneController : MonoBehaviour
{
    // 定义一个公开的方法，用来绑定按钮点击事件

    // 私有变量，但通过 [SerializeField] 可以在 Inspector 中修改
    [SerializeField] private string targetSceneName = "TutorialLevel";

    public void StartGame()
    {
        if (!TryGetLoadableSceneIdentifier(targetSceneName, out string sceneIdentifier))
        {
            Debug.LogError($"[SceneController] 目标场景未加入 Build Settings 或名称错误: {targetSceneName}");
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneIdentifier);
    }


    // 顺便写个退出游戏的函数，菜单一般都需要
    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("游戏已退出"); // 在编辑器里运行不会真退出，所以打个日志
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
        switch (sceneName)
        {
            case "MainMenu":
                return "Assets/Scenes/MainMenu.unity";
            case "IntroVideo":
                return "Assets/Scenes/IntroVideo.unity";
            case "TutorialLevel":
                return "Assets/Scenes/TutorialLevel.unity";
            case "Level_02":
                return "Assets/Scenes/Level_02.unity";
            default:
                return string.Empty;
        }
    }
}
