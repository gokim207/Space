using UnityEngine;
// New Input System
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 90f; // 각도(도) 단위/초
    public float radius = 5f; // 원 반지름
    public Transform planetCenter; // Planet 오브젝트의 Transform
    public Camera mainCamera;
    public WaveManager waveManager;
    public float desiredPlayerY = 5f; // 런 시 플레이어 Y 위치 목표
    [Header("Camera")]
    public bool controlCamera = false;
    public bool centerCameraOnPlanet = false;
    public bool overrideCameraSize = false;
    public float cameraOrthographicSize = 8f;
    public float cameraZ = -10f;
    [Header("Layout")]
    public bool overrideSceneLayout = false;
    public int hp = 1;
    public string playerId = "default";
    [Header("Directional Sprites")]
    public Sprite leftSprite;
    public Sprite rightSprite;
    public Sprite[] leftIdleSprites;
    public Sprite[] rightIdleSprites;
    public Sprite[] leftWalkSprites;
    public Sprite[] rightWalkSprites;
    public float idleFrameRate = 4f;
    public float walkFrameRate = 8f;
    private float fixedPlayerY = 0f;
    public float surfacePadding = 0.0f;

    private float angle = 90f; // 시작 각도(12시 방향)
    private bool warnedWaveManagerMissing = false;
    private SpriteRenderer spriteRenderer;
    private bool facingRight = true;
    private float idleFrameTimer = 0f;
    private int currentIdleFrame = -1;
    private float walkFrameTimer = 0f;
    private int currentWalkFrame = -1;
    public bool IsFacingRight => facingRight;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        LoadDirectionalSpritesFromResources();
        ApplyPlayerConfig();
        try
        {
            gameObject.tag = "Player";
        }
        catch (System.Exception)
        {
            Debug.LogWarning("Tag 'Player' is not defined in Project Settings -> Tags and Layers. Skipping tag assignment.");
        }
        // 자동 할당: Planet 이름으로 찾기
        if (planetCenter == null)
        {
            var p = GameObject.Find("Planet");
            if (p != null) planetCenter = p.transform;
        }
        if (planetCenter == null)
        {
            Debug.LogError("planetCenter를 PlayerController에 할당하세요! (Hierarchy에 'Planet' 이름으로 오브젝트가 있는지 확인)");
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        if (mainCamera == null)
        {
            Debug.LogError("Main Camera를 찾을 수 없습니다.");
        }
        else if (controlCamera)
        {
            mainCamera.orthographic = true;
            if (overrideCameraSize && cameraOrthographicSize > 0f)
                mainCamera.orthographicSize = cameraOrthographicSize;
        }

        // WaveManager 자동 할당
        if (waveManager == null)
        {
            waveManager = FindObjectOfType<WaveManager>();
            if (waveManager != null)
            {
            }
            else
            {
                // 없으면 자동 생성
                var go = new GameObject("WaveManager");
                waveManager = go.AddComponent<WaveManager>();
            }
        }

        // Rigidbody2D가 있으면 물리 설정을 Kinematic으로 고정 (transform 제어와 충돌 방지)
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }

        // 실수로 Planet의 자식으로 넣었다면 분리
        if (transform.parent != null && transform.parent == planetCenter)
        {
            transform.SetParent(null);
            Debug.Log("Player가 Planet의 자식이어서 분리했습니다.");
        }

        // 씬에서 직접 배치한 레이아웃을 우선 사용
        if (planetCenter != null && !overrideSceneLayout)
        {
            Vector3 fromPlanet = transform.position - planetCenter.position;
            if (fromPlanet.sqrMagnitude > 0.0001f)
            {
                radius = fromPlanet.magnitude;
                angle = Mathf.Atan2(fromPlanet.y, fromPlanet.x) * Mathf.Rad2Deg;
            }
            desiredPlayerY = transform.position.y;
        }
        else if (planetCenter != null)
        {
            planetCenter.position = new Vector3(0f, -5.3f, planetCenter.position.z);
            float planetSurfaceRadius = 0f;
            var cc = planetCenter.GetComponent<CircleCollider2D>();
            if (cc != null)
                planetSurfaceRadius = Mathf.Abs(cc.radius) * planetCenter.lossyScale.x;
            else
            {
                var sr = planetCenter.GetComponent<SpriteRenderer>();
                if (sr != null)
                    planetSurfaceRadius = Mathf.Max(sr.bounds.extents.x, sr.bounds.extents.y);
            }
            if (planetSurfaceRadius <= 0f) planetSurfaceRadius = 1f;

            float playerRadius = 0.5f;
            var pr = GetComponent<SpriteRenderer>();
            if (pr != null)
                playerRadius = Mathf.Max(pr.bounds.extents.x, pr.bounds.extents.y);
            var bc = GetComponent<BoxCollider2D>();
            if (bc != null)
                playerRadius = Mathf.Max(playerRadius, Mathf.Max(bc.bounds.extents.x, bc.bounds.extents.y));
            var pc = GetComponent<CircleCollider2D>();
            if (pc != null)
                playerRadius = Mathf.Max(playerRadius, pc.radius * transform.lossyScale.x);

            float minOrbitRadius = planetSurfaceRadius + playerRadius + surfacePadding;
            if (fixedPlayerY != 0f)
            {
                desiredPlayerY = fixedPlayerY;
                radius = Mathf.Abs(desiredPlayerY - planetCenter.position.y);
            }
            else if (radius > 0f)
            {
                desiredPlayerY = planetCenter.position.y + radius;
            }
            else
            {
                radius = Mathf.Abs(desiredPlayerY - planetCenter.position.y);
            }
            if (radius < minOrbitRadius)
                radius = minOrbitRadius;
            desiredPlayerY = planetCenter.position.y + radius;
        }

        // 시작 위치 세팅 (overrideSceneLayout를 사용할 때만 강제 보정)
        if (overrideSceneLayout)
        {
            UpdatePosition();
            transform.position = new Vector3(0f, desiredPlayerY, transform.position.z);
        }
        if (planetCenter != null)
            transform.up = (transform.position - planetCenter.position).normalized;
        ApplyFacingSprite(force: true);
        UpdateCameraPosition();
    }

    void ApplyPlayerConfig()
    {
        var def = GameData.GetPlayer(playerId);
        if (def == null) return;
        if (def.baseMoveSpeed > 0f) moveSpeed = def.baseMoveSpeed;
        if (def.maxHp > 0) hp = def.maxHp;
        // radius here is orbit radius, not player size; ignore tiny values to avoid starting inside planet
        if (def.radius > 1f) radius = def.radius;
        if (def.fixedY != 0f) fixedPlayerY = def.fixedY;
    }

    void Update()
    {
        if (waveManager == null)
        {
            if (!warnedWaveManagerMissing)
            {
                Debug.LogWarning("WaveManager가 PlayerController에 할당되지 않았거나 null입니다. Update가 실행되지 않습니다.");
                warnedWaveManagerMissing = true;
            }
            return;
        }

        if (waveManager.CurrentState != WaveManager.GameState.Run)
            return;

        float move = 0f;
        bool inputLogged = false;
        // Prefer new Input System if available
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                move -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                move += 1f;
            if (Mathf.Abs(move) > 0.01f)
            {
                inputLogged = true;
            }
        }
        if (!inputLogged && Gamepad.current != null)
        {
            var stick = Gamepad.current.leftStick.ReadValue();
            if (Mathf.Abs(stick.x) > 0.01f)
            {
                move = stick.x;
                inputLogged = true;
            }
        }

        // Fallback to legacy Input if new system not providing input
        if (!inputLogged)
        {
            try
            {
                move = Input.GetAxisRaw("Horizontal"); // may throw if legacy input disabled
                if (Mathf.Abs(move) > 0.01f)
                {
                }
            }
            catch (System.Exception)
            {
                // Ignore - active input handling likely set to Input System package only
            }
        }

        angle += -move * moveSpeed * Time.deltaTime; // 좌우 반전(시계/반시계)
        angle = angle % 360f;
        UpdateFacing(move);
        UpdatePosition();
        UpdateCameraPosition();
    }

    void UpdateFacing(float move)
    {
        bool isMoving = Mathf.Abs(move) > 0.01f;
        if (move > 0.01f)
            facingRight = true;
        else if (move < -0.01f)
            facingRight = false;

        ApplyMovementSprite(isMoving);
    }

    void LoadDirectionalSpritesFromResources()
    {
        if (leftSprite == null) leftSprite = LoadFirstSprite("character/left");
        if (rightSprite == null) rightSprite = LoadFirstSprite("character/right");
        if (leftIdleSprites == null || leftIdleSprites.Length == 0)
            leftIdleSprites = LoadSpriteFrames("animate/idle_left");
        if (rightIdleSprites == null || rightIdleSprites.Length == 0)
            rightIdleSprites = LoadSpriteFrames("animate/idle_right");
        if (leftWalkSprites == null || leftWalkSprites.Length == 0)
            leftWalkSprites = LoadSpriteFrames("animate/left_walk");
        if (rightWalkSprites == null || rightWalkSprites.Length == 0)
            rightWalkSprites = LoadSpriteFrames("animate/right_walk");
    }

    Sprite LoadFirstSprite(string resourcePath)
    {
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null) return sprite;

        Sprite[] frames = LoadSpriteFrames(resourcePath);
        return frames != null && frames.Length > 0 ? frames[0] : null;
    }

    Sprite[] LoadSpriteFrames(string resourcePath)
    {
        Sprite[] frames = Resources.LoadAll<Sprite>(resourcePath);
        if (frames == null || frames.Length == 0) return frames;
        System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
        return frames;
    }

    void ApplyMovementSprite(bool isMoving)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null) return;

        if (!isMoving)
        {
            walkFrameTimer = 0f;
            currentWalkFrame = -1;
            ApplyIdleSprite();
            return;
        }

        idleFrameTimer = 0f;
        currentIdleFrame = -1;
        Sprite[] frames = facingRight ? rightWalkSprites : leftWalkSprites;
        if (frames == null || frames.Length == 0)
        {
            ApplyFacingSprite();
            return;
        }

        walkFrameTimer += Time.deltaTime;
        int nextFrame = Mathf.FloorToInt(walkFrameTimer * Mathf.Max(1f, walkFrameRate)) % frames.Length;
        if (nextFrame != currentWalkFrame || spriteRenderer.sprite != frames[nextFrame])
        {
            currentWalkFrame = nextFrame;
            spriteRenderer.sprite = frames[nextFrame];
        }
    }

    void ApplyIdleSprite()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null) return;

        Sprite[] frames = facingRight ? rightIdleSprites : leftIdleSprites;
        if (frames == null || frames.Length == 0)
        {
            ApplyFacingSprite();
            return;
        }

        idleFrameTimer += Time.deltaTime;
        int nextFrame = Mathf.FloorToInt(idleFrameTimer * Mathf.Max(1f, idleFrameRate)) % frames.Length;
        if (nextFrame != currentIdleFrame || spriteRenderer.sprite != frames[nextFrame])
        {
            currentIdleFrame = nextFrame;
            spriteRenderer.sprite = frames[nextFrame];
        }
    }

    void ApplyFacingSprite(bool force = false)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null) return;

        Sprite target = facingRight ? rightSprite : leftSprite;
        if (target != null)
        {
            if (!force && spriteRenderer.sprite == target) return;
            spriteRenderer.sprite = target;
        }
        else
        {
            spriteRenderer.flipX = !facingRight;
        }
    }

    void UpdatePosition()
    {
        if (planetCenter == null) return;
        float rad = angle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius;
        transform.position = planetCenter.position + offset;
        // 항상 위쪽(12시 방향) 바라보게
        transform.up = (transform.position - planetCenter.position).normalized;
    }

    void UpdateCameraPosition()
    {
        if (mainCamera == null || !controlCamera) return;
        Vector3 camPos = centerCameraOnPlanet && planetCenter != null
            ? planetCenter.position
            : transform.position;
        camPos.z = cameraZ;
        mainCamera.transform.position = camPos;
    }


    // Scene view visualization: orbit circle and line to player
    void OnDrawGizmos()
    {
        if (planetCenter == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(planetCenter.position, radius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(planetCenter.position, transform.position);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, 0.05f);
    }

    // 충돌 처리(적/보스와 부딪히면 즉사)
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (waveManager == null || waveManager.CurrentState != WaveManager.GameState.Run)
            return;
        if (collision.gameObject.CompareTag("Enemy") || collision.gameObject.CompareTag("Boss"))
        {
            // damage player by 1
            TakeDamage(1);
        }
    }

    public void TakeDamage(int dmg)
    {
        hp -= dmg;
        if (hp <= 0)
        {
            // trigger end run sequence via WaveManager
            if (waveManager != null)
                waveManager.TriggerEndRunSequence(this);
            else
                Debug.LogWarning("Player hp <=0 but WaveManager is null.");
        }
    }
}
