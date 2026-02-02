using UnityEngine;
using UnityEditor;
using System.Threading;

public class Turret2 : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private GameObject bulletPrefab;

    [Header("Attributes")]
    [SerializeField] private float range = 3f;
    [SerializeField] private float attackTime = 0.5f;
    [SerializeField] private float chargeTime = 2.5f;
    [SerializeField] private int maxCharges = 6;

    private Transform target;
    private float attackCooldown = 0;
    private int currentCharges = 0;
    private float chargeProgress = 0;

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
        else if (attackCooldown >= attackTime && currentCharges > 0)
        {
            attack();
            attackCooldown = 0;
            currentCharges--;
        }

        if (attackCooldown < attackTime)
        {
            attackCooldown += Time.deltaTime;
        }

        

        if (chargeProgress >= chargeTime)
        {
            currentCharges++;
            chargeProgress = 0;
        }
        else if (currentCharges < maxCharges && chargeProgress < chargeTime)
        {
            chargeProgress += Time.deltaTime;
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
