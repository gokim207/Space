using UnityEngine;

public class Enemy : MonoBehaviour
{
    public int maxHP = 1;
    int hp;
    public int oreDrop = 1;
    public int oxygenGain = 1;
    public float moveSpeed = 1f;
    public Transform target;
    public WaveManager waveManager;

    void Start()
    {
        hp = maxHP;
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

    void Update()
    {
        if (target != null)
        {
            Vector3 dir = (target.position - transform.position).normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;
        }
    }

    public void TakeDamage(int dmg)
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
            // damage player => immediate run end per design
            if (waveManager != null)
                waveManager.EndRun();
        }
    }
}
