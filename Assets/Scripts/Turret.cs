using UnityEngine;
using UnityEditor;
using System.Threading;

public class Turret : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private GameObject bulletPrefab;

    [Header("Attributes")]
    [SerializeField] private bool spawnBullet = true;
    [SerializeField] private float range;
    [SerializeField] private float damage;
    [SerializeField] private float attackInterval;
    [SerializeField] private bool useCharges = false;
    [SerializeField] private float chargeTime;
    [SerializeField] private int maxCharges;

    private GameObject target;
    private float attackTimer;
    private int currentCharges;
    private float chargeProgress;


    private void Update()
    {

        if (attackTimer < attackInterval)
        {
            attackTimer += Time.deltaTime;
        }
        
        if (attackTimer >= attackInterval)
        {
            findTarget();
            if (target != null)
            {
                if (!useCharges)
                {
                    attack();
                    attackTimer = 0;
                }
                else if (currentCharges > 0)
                {
                    attack();
                    attackTimer = 0;
                    currentCharges--;
                }
            }
        }

        if (useCharges)
        {
            if (currentCharges < maxCharges && chargeProgress < chargeTime)
            {
                chargeProgress += Time.deltaTime;
            }

            if (chargeProgress >= chargeTime)
            {
                currentCharges++;
                chargeProgress = 0;
            }
        }

    }

    private void attack()
    {
        if (spawnBullet)
        {
            GameObject bulletInstance = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
            Bullet bulletScript = bulletInstance.GetComponent<Bullet>();
            bulletScript.SetTarget(target);
            bulletScript.SetDamage(damage);
        }
        else
        {
            EnemyMovement targetScript = target.GetComponent<EnemyMovement>();
            targetScript.TakeDamage(damage);
        }
    }

    private void findTarget()
    {

        target = null;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, range, (Vector2)transform.position, 0f, enemyMask);

        if (hits.Length > 0)
        {
            target = hits[0].collider.gameObject;
            EnemyMovement targetScript = target.GetComponent<EnemyMovement>();
            EnemyMovement comparisonScript;

            for (int i = 1; i < hits.Length; i++)
            {
                comparisonScript = hits[i].collider.gameObject.GetComponent<EnemyMovement>();
                if (comparisonScript.GetPathDist() > targetScript.GetPathDist())
                {
                    target = hits[i].collider.gameObject;
                    targetScript = comparisonScript;
                }
            }
        }
    }

    //The function below shows the tower's range in the editor, but not during gameplay
    //Should be changed at some point

    private void OnDrawGizmosSelected()
    {
        Handles.color = Color.darkOrange;
        Handles.DrawWireDisc(transform.position, transform.forward, range);
    }

}
