using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public class IntroVideoController : MonoBehaviour
{
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private Button skipButton;
    [SerializeField] private string videoFileName = "Intro_video.mp4";
    [SerializeField] private string nextSceneName = "TutorialLevel";
    [SerializeField] private float fallbackDuration = 15f;
    [SerializeField] private float skipInputDelay = 0.35f;

    private bool _loadingNextScene;
    private float _startTime;

    private void Awake()
    {
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }

        if (skipButton != null)
        {
            skipButton.onClick.AddListener(Skip);
        }
    }

    private void Start()
    {
        _startTime = Time.unscaledTime;

        if (videoPlayer == null)
        {
            LoadNextScene();
            return;
        }

        videoPlayer.url = Path.Combine(Application.streamingAssetsPath, videoFileName);
        videoPlayer.loopPointReached += HandleVideoFinished;
        videoPlayer.errorReceived += HandleVideoError;
        videoPlayer.Play();
    }

    private void Update()
    {
        if (_loadingNextScene) return;

        if (Time.unscaledTime - _startTime >= skipInputDelay && WasSkipInputPressed())
        {
            Skip();
            return;
        }

        if (fallbackDuration > 0f && Time.unscaledTime - _startTime >= fallbackDuration)
        {
            LoadNextScene();
        }
    }

    private static bool WasSkipInputPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
        {
            return true;
        }

        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }

    public void Skip()
    {
        LoadNextScene();
    }

    private void HandleVideoFinished(VideoPlayer source)
    {
        LoadNextScene();
    }

    private void HandleVideoError(VideoPlayer source, string message)
    {
        Debug.LogWarning($"[IntroVideo] 视频播放失败: {message}");
        LoadNextScene();
    }

    private void LoadNextScene()
    {
        if (_loadingNextScene) return;

        _loadingNextScene = true;
        Time.timeScale = 1f;

        if (TryGetLoadableSceneIdentifier(nextSceneName, out string sceneIdentifier))
        {
            SceneManager.LoadScene(sceneIdentifier);
            return;
        }

        Debug.LogError($"[IntroVideo] 目标场景未加入 Build Settings 或名称错误: {nextSceneName}");
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

    private void OnDestroy()
    {
        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(Skip);
        }

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= HandleVideoFinished;
            videoPlayer.errorReceived -= HandleVideoError;
        }
    }
}
