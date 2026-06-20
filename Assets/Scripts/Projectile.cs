using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 10f;
    public float lifeTime = 2f;
    private float timer;
    public int damage = 1;
    public float damageMultiplier = 1f;
    // Additional enemies this projectile can pass through after the first hit.
    public int pierceCount = 0;
    public string weaponId;
    public float maxRange;
    int remainingPierce;
    int hitIndex;
    Vector3 spawnPosition;
    Vector3 moveDirection = Vector3.right;
    readonly System.Collections.Generic.HashSet<int> hitEnemyIds = new System.Collections.Generic.HashSet<int>();

    void OnEnable()
    {
        timer = 0f;
        remainingPierce = Mathf.Max(0, pierceCount);
        hitEnemyIds.Clear();
        hitIndex = 0;
        spawnPosition = transform.position;
        LockMoveDirection(transform.right);
    }

    public void SetMoveDirection(Vector3 direction)
    {
        remainingPierce = Mathf.Max(0, pierceCount);
        hitIndex = 0;
        spawnPosition = transform.position;
        LockMoveDirection(direction);
    }

    void LockMoveDirection(Vector3 direction)
    {
        direction.z = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.right;
        moveDirection = direction.normalized;
    }

    void Update()
    {
        transform.position += moveDirection * speed * Time.deltaTime;
        timer += Time.deltaTime;
        if (timer >= lifeTime)
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            int id = enemy.GetInstanceID();
            if (hitEnemyIds.Contains(id)) return;
            hitEnemyIds.Add(id);

            int appliedDamage = WeaponTraitRuntime.ShouldExecute(weaponId, enemy)
                ? enemy.CurrentHP
                : WeaponTraitRuntime.ModifyHitDamage(
                    weaponId,
                    enemy,
                    damage,
                    hitIndex,
                    Vector3.Distance(spawnPosition, transform.position),
                    maxRange);
            enemy.TakeDamage(appliedDamage, this);
            hitIndex++;
            if (remainingPierce <= 0)
            {
                Destroy(gameObject);
                return;
            }

            remainingPierce--;
            return;
        }
        if (other.CompareTag("Boss"))
        {
            // Boss handling later
            Destroy(gameObject);
        }
    }
}
