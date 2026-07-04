using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class QuitGameHandler : MonoBehaviour
{
    /// <summary>
    /// 供 UI 按钮调用的退出游戏方法
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("正在退出游戏...");

#if UNITY_EDITOR
        // 如果在 Unity 编辑器中运行，则停止播放模式
        EditorApplication.isPlaying = false;
#else
        // 如果是打包后的独立运行版本，则关闭应用程序
        Application.Quit();
#endif
    }
}