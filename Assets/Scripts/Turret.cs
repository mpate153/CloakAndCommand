using UnityEngine;
using UnityEditor;
using System.Threading;

public class Turret : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private GameObject bulletPrefab;

    [Header("Attributes")]
    [SerializeField] private float range = 3f;
    [SerializeField] private float attackTime = 1.5f;

    private Transform target;
    private float attackCooldown;

    private void Update()
    {
        if (target == null)
        {
            findTarget();
        }
        else if (Vector2.Distance(target.position, transform.position) > range)
        {
            findTarget();
        }
        else if (attackCooldown >= attackTime)
        {
            attack();
            attackCooldown = 0;
        }

        if (attackCooldown < attackTime)
        {
            attackCooldown += Time.deltaTime;
        }
    }

    private void attack()
    {
        GameObject bulletInstance = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
        Bullet bulletScript = bulletInstance.GetComponent<Bullet>();
        bulletScript.SetTarget(target);
        Debug.Log("Attacked");
    }

    private void findTarget()
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, range, (Vector2)transform.position, 0f, enemyMask);

        if (hits.Length > 0)
        {
            target = hits[0].transform;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Handles.color = Color.darkOrange;
        Handles.DrawWireDisc(transform.position, transform.forward, range);
    }

}
