using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BasePlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 400f;
    public bool lockY = true;
    public float minX = -800f;
    public float maxX = 800f;

    [Header("Animation")]
    public float walkFrameRate = 8f;
    public float idleFrameRate = 3f;
    public string rightWalkPath = "animate/right_walk";
    public string leftWalkPath = "animate/left_walk";
    public string idleRightPath = "animate/idle_right";
    public string idleLeftPath = "animate/idle_left";

    private SpriteRenderer spriteRenderer;
    private Image uiImage;
    private RectTransform rectTransform;
    private Sprite[] rightWalkFrames;
    private Sprite[] leftWalkFrames;
    private Sprite[] idleRightFrames;
    private Sprite[] idleLeftFrames;
    private float fixedY;
    private float fixedAnchoredY;
    private float animTimer;
    private int frameIndex;
    private int facing = 1;
    private bool wasMoving;

    private static bool hasReturnPoint;
    private static Vector2 returnAnchoredPosition;
    private static Vector3 returnWorldPosition;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        uiImage = GetComponent<Image>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (uiImage == null && spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        rightWalkFrames = LoadFrames(rightWalkPath);
        leftWalkFrames = LoadFrames(leftWalkPath);
        idleRightFrames = LoadFrames(idleRightPath);
        idleLeftFrames = LoadFrames(idleLeftPath);

        if (GetCurrentSprite() == null)
            SetFirstAvailableIdle();
    }

    void Start()
    {
        RestoreReturnPointIfNeeded();
        fixedY = transform.position.y;
        if (rectTransform != null)
            fixedAnchoredY = rectTransform.anchoredPosition.y;
    }

    public void SaveReturnPoint()
    {
        hasReturnPoint = true;
        returnWorldPosition = transform.position;
        if (rectTransform != null)
            returnAnchoredPosition = rectTransform.anchoredPosition;
    }

    void RestoreReturnPointIfNeeded()
    {
        if (!hasReturnPoint)
            return;

        if (rectTransform != null)
            rectTransform.anchoredPosition = returnAnchoredPosition;
        else
            transform.position = returnWorldPosition;
    }

    void Update()
    {
        float inputX = ReadHorizontalInput();
        Move(inputX);
        UpdateAnimation(inputX);
    }

    float ReadHorizontalInput()
    {
        float input = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                input -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                input += 1f;
        }
        else
        {
            input = Input.GetAxisRaw("Horizontal");
        }

        return Mathf.Clamp(input, -1f, 1f);
    }

    void Move(float inputX)
    {
        if (rectTransform != null && uiImage != null)
        {
            var anchoredPos = rectTransform.anchoredPosition;
            anchoredPos.x = Mathf.Clamp(anchoredPos.x + inputX * moveSpeed * Time.deltaTime, minX, maxX);
            if (lockY)
                anchoredPos.y = fixedAnchoredY;
            rectTransform.anchoredPosition = anchoredPos;
            return;
        }

        var pos = transform.position;
        pos.x = Mathf.Clamp(pos.x + inputX * moveSpeed * Time.deltaTime, minX, maxX);
        if (lockY)
            pos.y = fixedY;
        transform.position = pos;
    }

    void UpdateAnimation(float inputX)
    {
        bool moving = Mathf.Abs(inputX) > 0.01f;
        if (moving)
        {
            int newFacing = inputX > 0f ? 1 : -1;
            if (newFacing != facing || !wasMoving)
                ResetAnimation();
            facing = newFacing;
        }
        else if (wasMoving)
        {
            ResetAnimation();
        }

        wasMoving = moving;
        Sprite[] frames = GetCurrentFrames(moving);
        if (frames == null || frames.Length == 0)
            return;

        float frameRate = moving ? walkFrameRate : idleFrameRate;
        animTimer += Time.deltaTime;
        if (animTimer >= 1f / Mathf.Max(0.01f, frameRate))
        {
            animTimer = 0f;
            frameIndex = (frameIndex + 1) % frames.Length;
        }

        SetSprite(frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)]);
    }

    Sprite[] GetCurrentFrames(bool moving)
    {
        if (moving)
            return facing >= 0 ? rightWalkFrames : leftWalkFrames;
        return facing >= 0 ? idleRightFrames : idleLeftFrames;
    }

    void ResetAnimation()
    {
        animTimer = 0f;
        frameIndex = 0;
    }

    void SetFirstAvailableIdle()
    {
        Sprite[] frames = idleRightFrames != null && idleRightFrames.Length > 0 ? idleRightFrames : rightWalkFrames;
        if (frames != null && frames.Length > 0)
            SetSprite(frames[0]);
    }

    Sprite GetCurrentSprite()
    {
        if (uiImage != null)
            return uiImage.sprite;
        return spriteRenderer != null ? spriteRenderer.sprite : null;
    }

    void SetSprite(Sprite sprite)
    {
        if (sprite == null)
            return;

        if (uiImage != null)
            uiImage.sprite = sprite;
        else if (spriteRenderer != null)
            spriteRenderer.sprite = sprite;
    }

    Sprite[] LoadFrames(string path)
    {
        var frames = Resources.LoadAll<Sprite>(path);
        if (frames == null || frames.Length == 0)
            return frames;

        var list = new List<Sprite>(frames);
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list.ToArray();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallForInitialScene()
    {
        TryInstall(SceneManager.GetActiveScene());
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryInstall(scene);
    }

    static void TryInstall(Scene scene)
    {
        if (!BaseSceneNavigation.IsBaseSceneName(scene.name))
            return;

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var playerTransform = FindChildRecursive(roots[i].transform, "Player");
            if (playerTransform == null)
                continue;

            if (playerTransform.GetComponent<BasePlayerController>() == null)
                playerTransform.gameObject.AddComponent<BasePlayerController>();
            return;
        }
    }

    static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var result = FindChildRecursive(root.GetChild(i), targetName);
            if (result != null)
                return result;
        }

        return null;
    }
}
