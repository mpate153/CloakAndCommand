using UnityEngine;
using UnityEngine.Rendering;

public class Bullet : MonoBehaviour
{

    [Header("Attributes")]
    [SerializeField] protected float speed = 8f;

    protected GameObject bulletTarget;
    protected float bulletDamage = 0f;

    protected virtual void Update()
    {
        if (bulletTarget != null)
        {
            transform.position = Vector3.MoveTowards(transform.position, bulletTarget.transform.position, speed * Time.deltaTime);
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

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        TDEnemyProperties objectHitScript = other.gameObject.GetComponent<TDEnemyProperties>();
        objectHitScript.TakeDamage(bulletDamage);
        Destroy(gameObject);
    }
}
