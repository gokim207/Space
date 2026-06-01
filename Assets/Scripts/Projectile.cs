using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 10f;
    public float lifeTime = 2f;
    private float timer;
    public int damage = 1;
    public float damageMultiplier = 1f;

    void Update()
    {
        transform.position += transform.right * speed * Time.deltaTime;
        timer += Time.deltaTime;
        if (timer >= lifeTime)
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }
        if (other.CompareTag("Boss"))
        {
            // Boss handling later
            Destroy(gameObject);
        }
    }
}
