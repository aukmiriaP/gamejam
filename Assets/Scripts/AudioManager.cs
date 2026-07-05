using UnityEngine;

public class AudioManager : MonoBehaviour
{
    // 静态单例，方便其他任何脚本直接调用
    public static AudioManager Instance;

    [Header("音效播放器")]
    public AudioSource sfxSource;

    private void Awake()
    {
        // 确保场景中只有一个 AudioManager
        if (Instance == null)
        {
            Instance = this;
            // 如果你希望切换场景时音效也不断，可以取消注释下面这行
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 公共播放音效的方法
    /// </summary>
    /// <param name="clip">要播放的音频剪辑</param>
    public void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
        {
            // 使用 PlayOneShot 允许音效重叠播放，子弹连发也不会卡顿
            sfxSource.PlayOneShot(clip);
        }
    }

}
