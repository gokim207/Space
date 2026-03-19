using UnityEngine;

public class Enemy : MonoBehaviour
{
    public int maxHP = 1;
    int hp;
    public int oreDrop = 1;
    public int oxygenGain = 1;
    public float moveSpeed = 1f;
    public float contactRadius = 0.5f;
    public Transform target;
    public WaveManager waveManager;
    bool hasDamaged = false;

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
    }

    void Awake()
    {
        hp = maxHP;
    }

    void Update()
    {
        if (target != null)
        {
            Vector3 dir = (target.position - transform.position).normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;
            // fallback proximity-based contact detection
            if (!hasDamaged && target != null)
            {
                float d = Vector3.Distance(transform.position, target.position);
                if (d <= contactRadius)
                {
                    var pc = target.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        Debug.Log($"Enemy proximity hit player: {gameObject.name}");
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

    public void TakeDamage(int dmg)
    {
        hp -= dmg;
        Debug.Log($"Enemy took damage: -{dmg}, hp={hp}");
        // Visual feedback: flash red briefly
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            StopAllCoroutines();
            StartCoroutine(FlashHit(sr));
        }
        if (hp <= 0)
        {
            Die();
        }
    }

    System.Collections.IEnumerator FlashHit(SpriteRenderer sr)
    {
        Color orig = sr.color;
        sr.color = Color.red;
        yield return new WaitForSeconds(0.08f);
        sr.color = orig;
    }

    void Die()
    {
        Debug.Log($"Enemy died: {gameObject.name}");
        // Notify wave manager
        if (waveManager != null)
        {
            waveManager.OnEnemyKilled(oreDrop, oxygenGain);
        }
        Destroy(gameObject);
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
