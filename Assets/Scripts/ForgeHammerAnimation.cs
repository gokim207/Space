using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ForgeHammerAnimation : MonoBehaviour
{
    const string ResourcePath = "animate/hammerAttack";
    const int FirstFrame = 0;
    const int LastFrame = 9;

    public float frameInterval = 0.06f;

    static ForgeHammerAnimation instance;

    Image targetImage;
    Sprite[] frames;
    Coroutine playRoutine;

    void Awake()
    {
        instance = this;
        targetImage = GetComponent<Image>();
        LoadFrames();

        if (targetImage != null && frames.Length > 0)
            targetImage.sprite = frames[0];
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public static void Play()
    {
        EnsureInstalled();
        instance?.PlayAnimation();
    }

    void PlayAnimation()
    {
        if (targetImage == null || frames == null || frames.Length == 0)
            return;

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(PlayFrames());
    }

    IEnumerator PlayFrames()
    {
        for (int i = 0; i < frames.Length; i++)
        {
            targetImage.sprite = frames[i];
            yield return new WaitForSecondsRealtime(frameInterval);
        }

        targetImage.sprite = frames[0];
        playRoutine = null;
    }

    void LoadFrames()
    {
        Sprite[] loaded = Resources.LoadAll<Sprite>(ResourcePath);
        var indexedFrames = new List<(int index, Sprite sprite)>();

        for (int i = 0; i < loaded.Length; i++)
        {
            Sprite sprite = loaded[i];
            if (sprite == null || !TryGetFrameIndex(sprite.name, out int index))
                continue;
            if (index < FirstFrame || index > LastFrame)
                continue;

            indexedFrames.Add((index, sprite));
        }

        indexedFrames.Sort((a, b) => a.index.CompareTo(b.index));
        frames = new Sprite[indexedFrames.Count];
        for (int i = 0; i < indexedFrames.Count; i++)
            frames[i] = indexedFrames[i].sprite;
    }

    static bool TryGetFrameIndex(string spriteName, out int index)
    {
        index = -1;
        const string prefix = "hammerAttack_";
        if (string.IsNullOrEmpty(spriteName) || !spriteName.StartsWith(prefix))
            return false;

        return int.TryParse(spriteName.Substring(prefix.Length), out index);
    }

    static void EnsureInstalled()
    {
        if (instance != null)
            return;
        if (SceneManager.GetActiveScene().name != "UpgradeScene")
            return;

        GameObject centerPanel = FindInActiveScene("centerPanel");
        if (centerPanel == null)
            return;

        instance = centerPanel.GetComponent<ForgeHammerAnimation>();
        if (instance == null)
            instance = centerPanel.AddComponent<ForgeHammerAnimation>();
    }

    static GameObject FindInActiveScene(string targetName)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindRecursive(roots[i].transform, targetName);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    static Transform FindRecursive(Transform root, string targetName)
    {
        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindRecursive(root.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }
}
