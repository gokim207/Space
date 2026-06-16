using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BgmManager : MonoBehaviour
{
    private const string BaseBgmResourcePath = "bgm/base";

    private static BgmManager instance;

    private AudioSource audioSource;
    private AudioClip[] baseClips = Array.Empty<AudioClip>();
    private AudioClip currentClip;
    private string previousSceneName = string.Empty;
    private bool basePlaylistActive;
    private bool applicationSuspended;
    private bool resumeAfterFocus;
    private int pausedTimeSamples;
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
        audioSource.volume = 0.6f;

        LoadBaseClips();

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
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
        bool isBaseMusicScene = isBase || isUpgrade;
        bool cameFromBaseMusicScene = BaseSceneNavigation.IsBaseSceneName(previousSceneName) || previousSceneName == "UpgradeScene";

        if (isBaseMusicScene)
        {
            if (basePlaylistActive && cameFromBaseMusicScene && audioSource != null && audioSource.isPlaying)
            {
                previousSceneName = scene.name;
                return;
            }

            StartBasePlaylistFromFirstTrack();
        }
        else
        {
            StopBasePlaylist();
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
        PlayClip(baseClips[0]);
    }

    void StopBasePlaylist()
    {
        basePlaylistActive = false;
        currentClip = null;
        resumeAfterFocus = false;
        pausedClip = null;
        pausedTimeSamples = 0;
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
        if (clip == null || audioSource == null)
            return;

        resumeAfterFocus = false;
        pausedClip = null;
        pausedTimeSamples = 0;
        currentClip = clip;
        audioSource.clip = clip;
        audioSource.Play();
    }

    void PauseForApplicationLeave()
    {
        applicationSuspended = true;

        if (!basePlaylistActive || audioSource == null || audioSource.clip == null)
            return;

        pausedClip = audioSource.clip;
        pausedTimeSamples = Mathf.Clamp(audioSource.timeSamples, 0, Mathf.Max(0, pausedClip.samples - 1));
        resumeAfterFocus = true;

        if (audioSource.isPlaying)
            audioSource.Pause();
    }

    void ResumeAfterApplicationReturn()
    {
        applicationSuspended = false;

        if (!resumeAfterFocus || !basePlaylistActive || audioSource == null || pausedClip == null)
            return;

        audioSource.clip = pausedClip;
        currentClip = pausedClip;

        if (pausedClip.samples > 0)
            audioSource.timeSamples = Mathf.Clamp(pausedTimeSamples, 0, pausedClip.samples - 1);

        audioSource.Play();
        resumeAfterFocus = false;
        pausedClip = null;
        pausedTimeSamples = 0;
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
