using UnityEngine;

public class AudioManager : MonoBehaviour
{
    // 静态单例，方便其他任何脚本直接调用
    public static AudioManager Instance;

    [Header("音效播放器")]
    public AudioSource sfxSource;

    [Header("背景音乐")]
    public AudioSource bgmSource;
    public AudioClip backgroundMusic;
    public bool playBgmOnAwake = true;
    [Range(0f, 1f)] public float bgmVolume = 0.6f;

    private void Awake()
    {
        // 确保场景中只有一个 AudioManager
        if (Instance == null)
        {
            Instance = this;
            // 如果你希望切换场景时音效也不断，可以取消注释下面这行
            // DontDestroyOnLoad(gameObject);
            SetupBgmSource();

            if (playBgmOnAwake && backgroundMusic != null)
            {
                PlayBGM(backgroundMusic);
            }
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

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;

        SetupBgmSource();
        if (bgmSource == null) return;

        if (bgmSource.clip == clip && bgmSource.isPlaying)
        {
            return;
        }

        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.volume = bgmVolume;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        if (bgmSource != null)
        {
            bgmSource.Stop();
        }
    }

    private void SetupBgmSource()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
        }

        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;
        bgmSource.volume = bgmVolume;
    }

}
