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
    [SerializeField] private float attackTime;
    [SerializeField] private bool useCharges = false;
    [SerializeField] private float chargeTime;
    [SerializeField] private int maxCharges;

    private Transform target;
    private float attackCooldown;
    private int currentCharges;
    private float chargeProgress;


    private void Update()
    {

        if (attackCooldown < attackTime)
        {
            attackCooldown += Time.deltaTime;
        }
        
        if (attackCooldown >= attackTime)
        {
            findTarget();
            if (target != null)
            {
                if (!useCharges)
                {
                    attack();
                    attackCooldown = 0;
                }
                else if (currentCharges > 0)
                {
                    attack();
                    attackCooldown = 0;
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
            Debug.Log("Attacked with bullet");
        }
        else
        {
            Debug.Log("Attacked without bullet");
        }
    }

    private void findTarget()
    {
        target = null;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, range, (Vector2)transform.position, 0f, enemyMask);

        if (hits.Length > 0)
        {
            target = hits[0].transform;
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
