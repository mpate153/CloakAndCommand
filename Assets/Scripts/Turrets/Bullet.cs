using UnityEngine;
using UnityEngine.Rendering;

public class Bullet : MonoBehaviour
{

    [Header("Attributes")]
    [SerializeField] protected float bulletSpeed = 8f;

    protected GameObject bulletTarget;
    protected float bulletDamage = 0f;

    protected virtual void Update()
    {
        if (bulletTarget != null)
        {
            transform.position = Vector3.MoveTowards(transform.position, bulletTarget.transform.position, bulletSpeed * Time.deltaTime);
            RotateToTarget();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetTarget(GameObject target)
    {
        bulletTarget = target;
    }

    public void SetDamage(float damage)
    {
        bulletDamage = damage;
    }

    public void SetSpeed(float speed)
    {
        bulletSpeed = speed;
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        TDEnemyProperties objectHitScript = other.gameObject.GetComponent<TDEnemyProperties>();
        objectHitScript.TakeDamage(bulletDamage);
        Destroy(gameObject);
    }

    protected virtual void RotateToTarget()
    {
        float angle = Mathf.Atan2(bulletTarget.transform.position.y - transform.position.y, bulletTarget.transform.position.x - transform.position.x) * Mathf.Rad2Deg - 90f;

        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
