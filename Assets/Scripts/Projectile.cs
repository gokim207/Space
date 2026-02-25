using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 10f;
    public float lifeTime = 2f;
    private float timer;
    public int damage = 1;

    void Update()
    {
        transform.Translate(Vector2.right * speed * Time.deltaTime);
        timer += Time.deltaTime;
        if (timer >= lifeTime)
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            Debug.Log($"Projectile hit enemy: {enemy.gameObject.name}");
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
