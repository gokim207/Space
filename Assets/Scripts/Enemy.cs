using UnityEngine;

public class Enemy : MonoBehaviour
{
    public int maxHP = 1;
    float hp;
    public int oreDrop = 1;
    public float oxygenGain = 0f; // base oxygen gain; skills add extra
    public string oreId = "stone";
    public float moveSpeed = 1f;
    public float contactRadius = 0.5f;
    public Transform target;
    public WaveManager waveManager;
    bool hasDamaged = false;
    private Color baseColor = Color.white;
    public float CurrentHP => hp;
    public float HealthRatio => maxHP > 0 ? Mathf.Clamp01((float)hp / maxHP) : 0f;
    public bool IsBoss
    {
        get
        {
            try { return CompareTag("Boss"); }
            catch (UnityException) { return false; }
        }
    }

    void Start()
    {
        // Attempt to set tag to 'Enemy'. If the tag isn't defined in Project Settings this will throw at runtime,
        // so guard with try/catch to avoid crashing the game during playtesting.
        try
        {
            gameObject.tag = "Enemy";
        }
        catch (System.Exception)
        {
            Debug.LogWarning("Tag 'Enemy' is not defined in Project Settings -> Tags and Layers. Skipping tag assignment.");
        }
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
    }

    void Awake()
    {
        hp = maxHP;
    }

    public void ApplyStats(int newMaxHp, float newMoveSpeed)
    {
        maxHP = newMaxHp;
        hp = maxHP;
        moveSpeed = newMoveSpeed;
    }

    void Update()
    {
        if (target == null)
        {
            var tagged = GameObject.FindWithTag("Player");
            if (tagged != null) target = tagged.transform;
            if (target == null)
            {
                var pc = FindObjectOfType<PlayerController>();
                if (pc != null) target = pc.transform;
            }
        }
        if (moveSpeed <= 0f) moveSpeed = 1f;
        if (target != null)
        {
            transform.position = Vector3.MoveTowards(transform.position, target.position, moveSpeed * Time.deltaTime);
            // fallback proximity-based contact detection
            if (!hasDamaged && target != null)
            {
                float d = Vector3.Distance(transform.position, target.position);
                if (d <= contactRadius)
                {
                    var pc = target.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        pc.TakeDamage(1);
                    }
                    else if (waveManager != null)
                    {
                        waveManager.EndRun();
                    }
                    hasDamaged = true;
                    Destroy(gameObject);
                }
            }
        }
    }

    public void TakeDamage(float dmg, Projectile sourceProjectile = null)
    {
        hp -= dmg;
        // Visual feedback: flash red briefly
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            StopAllCoroutines();
            StartCoroutine(FlashHit(sr));
        }
        if (hp <= 0)
        {
            Die(sourceProjectile);
        }
    }

    System.Collections.IEnumerator FlashHit(SpriteRenderer sr)
    {
        Color orig = baseColor;
        sr.color = Color.red;
        yield return new WaitForSeconds(0.08f);
        sr.color = orig;
    }

    public void SetBaseColor(Color c)
    {
        baseColor = c;
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = c;
    }

    void Die(Projectile sourceProjectile)
    {
        // Notify wave manager
        if (waveManager != null)
        {
            waveManager.OnEnemyKilled(oreDrop, oxygenGain, oreId, sourceProjectile);
        }
        Destroy(gameObject);
    }


    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            var pc = other.GetComponent<PlayerController>();
            if (pc != null) pc.TakeDamage(1);
            else if (waveManager != null) waveManager.EndRun();
            hasDamaged = true;
            Destroy(gameObject);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            var pc = collision.gameObject.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.TakeDamage(1);
            }
            else
            {
                // fallback: call EndRun immediately
                if (waveManager != null)
                    waveManager.EndRun();
            }
        }
    }
}
