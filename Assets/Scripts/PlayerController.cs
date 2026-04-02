using UnityEngine;
// New Input System
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 90f; // 각도(도) 단위/초
    public float radius = 5f; // 원 반지름
    public Transform planetCenter; // Planet 오브젝트의 Transform
    public Camera mainCamera;
    public WaveManager waveManager;
    public float desiredPlayerY = 5f; // 런 시 플레이어 Y 위치 목표
    public int hp = 1;
    public string playerId = "default";
    private float fixedPlayerY = 0f;

    private float angle = 90f; // 시작 각도(12시 방향)
    private bool warnedWaveManagerMissing = false;

    void Start()
    {
        ApplyPlayerConfig();
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

        // WaveManager 자동 할당
        if (waveManager == null)
        {
            waveManager = FindObjectOfType<WaveManager>();
            if (waveManager != null)
            {
                Debug.Log("WaveManager를 자동으로 할당했습니다: " + waveManager.name);
            }
            else
            {
                // 없으면 자동 생성
                var go = new GameObject("WaveManager");
                waveManager = go.AddComponent<WaveManager>();
                Debug.Log("씬에 WaveManager가 없어 새로 생성하고 자동 할당했습니다.");
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

        // 강제로 Planet 위치 지정(요청대로)
        if (planetCenter != null)
        {
            planetCenter.position = new Vector3(0f, -5.3f, planetCenter.position.z);
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
        }

        // 시작 위치 세팅 (계산된 radius 사용)
        UpdatePosition();
        // 정확한 Y 위치 보정(정수/디자인 요구 시)
        transform.position = new Vector3(0f, desiredPlayerY, transform.position.z);
        transform.up = (transform.position - planetCenter.position).normalized;
        UpdateCameraPosition();
        Debug.Log($"PlayerController 초기화 완료: angle={angle}, radius={radius}");
    }

    void ApplyPlayerConfig()
    {
        var def = GameData.GetPlayer(playerId);
        if (def == null) return;
        if (def.baseMoveSpeed > 0f) moveSpeed = def.baseMoveSpeed;
        if (def.maxHp > 0) hp = def.maxHp;
        if (def.radius > 0f) radius = def.radius;
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
        UpdatePosition();
        UpdateCameraPosition();
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
        if (mainCamera == null) return;
        Vector3 camPos = transform.position;
        camPos.z = -10f;
        mainCamera.transform.position = camPos;
    }

    // On-screen debug info for quick verification during Play
    void OnGUI()
    {
        if (!Application.isPlaying) return;
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.normal.textColor = Color.white;
        style.fontSize = 14;
        string info = $"angle={angle:F1}\nradius={radius:F2}\nplayer={transform.position.x:F2},{transform.position.y:F2}\nplanet={ (planetCenter!=null? (planetCenter.position.x.ToString("F2")+","+planetCenter.position.y.ToString("F2")) : "null") }";
        GUI.Label(new Rect(10, 10, 320, 80), info, style);
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
        Debug.Log($"Player took damage: -{dmg}, hp={hp}");
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
