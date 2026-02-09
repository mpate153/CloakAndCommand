using UnityEngine;
using UnityEngine.Rendering;

public class Bullet : MonoBehaviour
{

    [Header("Attributes")]
    [SerializeField] private float speed = 3f;

    private GameObject bulletTarget;
    private float bulletDamage = 0f;

    private void Update()
    {
        if (bulletTarget != null)
        {
            transform.position = Vector3.MoveTowards(transform.position, bulletTarget.transform.position, speed * Time.deltaTime);
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

    private void OnCollisionEnter2D(Collision2D other)
    {
        EnemyMovement objectHitScript = other.gameObject.GetComponent<EnemyMovement>();
        objectHitScript.TakeDamage(bulletDamage);
        Destroy(gameObject);
    }
}
