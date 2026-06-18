using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BgmManager : MonoBehaviour
{
    private const string BaseBgmResourcePath = "bgm/base";
    public const string MusicVolumePrefKey = "setting_music_volume";
    public const string EffectVolumePrefKey = "setting_effect_volume";
    public const float DefaultMusicVolume = 0.5f;
    public const float DefaultEffectVolume = 0.5f;

    private static BgmManager instance;

    private AudioSource audioSource;
    private AudioClip[] baseClips = Array.Empty<AudioClip>();
    private AudioClip currentClip;
    private string previousSceneName = string.Empty;
    private bool basePlaylistActive;
    private bool titleLoopActive;
    private bool applicationSuspended;
    private bool resumeAfterFocus;
    private int pausedTimeSamples;
    private float pausedTimeSeconds;
    private AudioClip pausedClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        EnsureExists();
    }

    public static BgmManager EnsureExists()
    {
        if (instance != null && !instance.Equals(null))
            return instance;

        var go = new GameObject("BgmManager");
        instance = go.AddComponent<BgmManager>();
        DontDestroyOnLoad(go);
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        ApplySavedVolume();

        LoadBaseClips();

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static float MusicVolume => PlayerPrefs.GetFloat(MusicVolumePrefKey, DefaultMusicVolume);
    public static float EffectVolume => PlayerPrefs.GetFloat(EffectVolumePrefKey, DefaultEffectVolume);

    public static void SetMusicVolume(float volume)
    {
        PlayerPrefs.SetFloat(MusicVolumePrefKey, Mathf.Clamp01(volume));
        PlayerPrefs.Save();

        if (instance != null && !instance.Equals(null))
            instance.ApplySavedVolume();
    }

    public static void SetEffectVolume(float volume)
    {
        PlayerPrefs.SetFloat(EffectVolumePrefKey, Mathf.Clamp01(volume));
        PlayerPrefs.Save();
    }

    public static void ResetSoundSettings()
    {
        PlayerPrefs.SetFloat(MusicVolumePrefKey, DefaultMusicVolume);
        PlayerPrefs.SetFloat(EffectVolumePrefKey, DefaultEffectVolume);
        PlayerPrefs.Save();

        if (instance != null && !instance.Equals(null))
            instance.ApplySavedVolume();
    }

    void ApplySavedVolume()
    {
        if (audioSource != null)
            audioSource.volume = MusicVolume;
    }

    void OnDestroy()
    {
        if (instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Update()
    {
        if (!basePlaylistActive || applicationSuspended || resumeAfterFocus)
            return;

        if (audioSource == null || audioSource.isPlaying || baseClips.Length == 0)
            return;

        PlayRandomBaseClip();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool isBase = BaseSceneNavigation.IsBaseSceneName(scene.name);
        bool isUpgrade = scene.name == "UpgradeScene";
        bool isTitle = scene.name == "TitleScene";
        bool isBaseMusicScene = isBase || isUpgrade;
        bool cameFromBaseMusicScene = BaseSceneNavigation.IsBaseSceneName(previousSceneName) || previousSceneName == "UpgradeScene";

        if (isTitle)
        {
            StartTitleLoop();
        }
        else if (isBaseMusicScene)
        {
            if (basePlaylistActive && cameFromBaseMusicScene && (resumeAfterFocus || currentClip != null || (audioSource != null && audioSource.isPlaying)))
            {
                previousSceneName = scene.name;
                return;
            }

            StartBasePlaylistFromFirstTrack();
        }
        else
        {
            StopMusic();
        }

        previousSceneName = scene.name;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            ResumeAfterApplicationReturn();
        else
            PauseForApplicationLeave();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            PauseForApplicationLeave();
        else
            ResumeAfterApplicationReturn();
    }

    void LoadBaseClips()
    {
        baseClips = Resources.LoadAll<AudioClip>(BaseBgmResourcePath)
            .Where(clip => clip != null)
            .OrderBy(GetTrackNumber)
            .ThenBy(clip => clip.name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (baseClips.Length == 0)
            Debug.LogWarning($"BgmManager: Resources/{BaseBgmResourcePath} 에 BGM AudioClip이 없습니다.");
    }

    void StartBasePlaylistFromFirstTrack()
    {
        if (baseClips.Length == 0)
        {
            LoadBaseClips();
            if (baseClips.Length == 0)
                return;
        }

        basePlaylistActive = true;
        titleLoopActive = false;
        PlayClip(baseClips[0]);
    }

    void StartTitleLoop()
    {
        if (titleLoopActive && currentClip != null && audioSource != null && audioSource.isPlaying)
            return;

        if (baseClips.Length == 0)
        {
            LoadBaseClips();
            if (baseClips.Length == 0)
                return;
        }

        AudioClip titleClip = baseClips.FirstOrDefault(clip => GetTrackNumber(clip) == 19);
        if (titleClip == null)
        {
            Debug.LogWarning("BgmManager: 타이틀 BGM 19번을 찾지 못했습니다. Resources/bgm/base/19.* 파일을 확인해 주세요.");
            return;
        }

        basePlaylistActive = false;
        titleLoopActive = true;
        PlayClip(titleClip, true);
    }

    void StopMusic()
    {
        basePlaylistActive = false;
        titleLoopActive = false;
        currentClip = null;
        resumeAfterFocus = false;
        pausedClip = null;
        pausedTimeSamples = 0;
        pausedTimeSeconds = 0f;
        if (audioSource != null)
            audioSource.Stop();
    }

    void PlayRandomBaseClip()
    {
        if (baseClips.Length == 1)
        {
            PlayClip(baseClips[0]);
            return;
        }

        AudioClip nextClip = currentClip;
        int guard = 0;
        while (nextClip == currentClip && guard < 16)
        {
            nextClip = baseClips[UnityEngine.Random.Range(0, baseClips.Length)];
            guard++;
        }

        PlayClip(nextClip);
    }

    void PlayClip(AudioClip clip)
    {
        PlayClip(clip, false);
    }

    void PlayClip(AudioClip clip, bool loop)
    {
        if (clip == null || audioSource == null)
            return;

        resumeAfterFocus = false;
        pausedClip = null;
        pausedTimeSamples = 0;
        pausedTimeSeconds = 0f;
        currentClip = clip;
        audioSource.clip = clip;
        audioSource.loop = loop;
        audioSource.Play();
    }

    void PauseForApplicationLeave()
    {
        applicationSuspended = true;

        if ((!basePlaylistActive && !titleLoopActive) || audioSource == null || audioSource.clip == null)
            return;

        // On some platforms Unity sends both focus and pause callbacks. Keep the first
        // captured position because a second callback can report the paused source at 0.
        if (resumeAfterFocus)
            return;

        pausedClip = audioSource.clip;
        pausedTimeSamples = Mathf.Clamp(audioSource.timeSamples, 0, Mathf.Max(0, pausedClip.samples - 1));
        pausedTimeSeconds = Mathf.Clamp(audioSource.time, 0f, Mathf.Max(0f, pausedClip.length - 0.01f));
        resumeAfterFocus = true;

        if (audioSource.isPlaying)
            audioSource.Pause();
    }

    void ResumeAfterApplicationReturn()
    {
        applicationSuspended = false;

        if (!resumeAfterFocus || (!basePlaylistActive && !titleLoopActive) || audioSource == null || pausedClip == null)
            return;

        audioSource.clip = pausedClip;
        currentClip = pausedClip;

        if (pausedTimeSamples > 0 && pausedClip.samples > 0)
            audioSource.timeSamples = Mathf.Clamp(pausedTimeSamples, 0, pausedClip.samples - 1);
        else if (pausedTimeSeconds > 0f)
            audioSource.time = Mathf.Clamp(pausedTimeSeconds, 0f, Mathf.Max(0f, pausedClip.length - 0.01f));

        audioSource.Play();
        if (pausedTimeSeconds > 0f)
            audioSource.time = Mathf.Clamp(pausedTimeSeconds, 0f, Mathf.Max(0f, pausedClip.length - 0.01f));

        resumeAfterFocus = false;
        pausedClip = null;
        pausedTimeSamples = 0;
        pausedTimeSeconds = 0f;
    }

    static int GetTrackNumber(AudioClip clip)
    {
        if (clip == null || string.IsNullOrWhiteSpace(clip.name))
            return int.MaxValue;

        string name = clip.name.Trim();
        int dotIndex = name.IndexOf('.');
        string numberText = dotIndex >= 0 ? name.Substring(0, dotIndex) : name.Split(' ')[0];
        return int.TryParse(numberText, out int number) ? number : int.MaxValue;
    }
}
