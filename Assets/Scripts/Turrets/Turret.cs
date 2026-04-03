using System;
using System.Threading;
using UnityEditor;
using UnityEngine;

public class Turret : MonoBehaviour
{

    [Header("References")]
    [SerializeField] protected LayerMask enemyMask;
    [SerializeField] protected GameObject bulletPrefab;

    [Header("Attributes")]
    [SerializeField] protected float range;
    [SerializeField] protected float damage;
    [SerializeField] protected float attackInterval;
    [SerializeField] protected bool useCharges = false;
    [SerializeField] protected float chargeTime;
    [SerializeField] protected int maxCharges;
    [SerializeField] protected float rotationSpeed = 120f;
    [SerializeField] protected bool useDOT = false;
    [SerializeField] protected float DOTInterval;
    [SerializeField] protected bool pierceBullet = false;

    protected GameObject target;
    protected float attackTimer;
    protected int currentCharges;
    protected float chargeProgress;


    protected virtual void Update()
    {
        findTarget();

        if (target != null)
        {
            RotateToTarget();
        }

        if (attackTimer < attackInterval)
        {
            attackTimer += Time.deltaTime;
        }
        
        if (attackTimer >= attackInterval)
        {
            if (target != null && isFacingTarget()) //the target != null check isn't necessary since isFacingTarget already checks this; i've kept it here for clarity and future proofing, in case isFacingTarget is changed in the future
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
                    loseCharge();
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
                gainCharge();
                chargeProgress = 0;
            }
        }

    }

    protected virtual void attack()
    {
        if (!pierceBullet)
        {
            if(!useDOT)
            {
                GameObject bulletInstance = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
                Bullet bulletScript = bulletInstance.GetComponent<Bullet>();
                bulletScript.SetTarget(target);
                bulletScript.SetDamage(damage);
            }
            else
            {
                GameObject bulletInstance = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
                DOTBullet bulletScipt = bulletInstance.GetComponent<DOTBullet>();
                bulletScipt.SetTarget(target);
                bulletScipt.SetDamage(damage);
                bulletScipt.setDOTInterval(DOTInterval);
            }
        }
        else
        {
            GameObject bulletInstance = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
            Bullet bulletScript = bulletInstance.GetComponent<PierceBullet>();
            bulletScript.SetTarget(target);
            bulletScript.SetDamage(damage);
        }
    }

    protected virtual void gainCharge()
    {
        currentCharges++;
        transform.GetChild(0).GetChild(currentCharges-1).gameObject.SetActive(true);
    }

    protected virtual void loseCharge()
    {
        transform.GetChild(0).GetChild(currentCharges - 1).gameObject.SetActive(false);
        currentCharges--;
    }

    protected virtual void findTarget()
    {
        target = null;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, range, (Vector2)transform.position, 0f, enemyMask);

        if (hits.Length > 0)
        {
            target = hits[0].collider.gameObject;
            TDEnemyProperties targetScript = target.GetComponent<TDEnemyProperties>();
            TDEnemyProperties comparisonScript;

            for (int i = 1; i < hits.Length; i++)
            {
                comparisonScript = hits[i].collider.gameObject.GetComponent<TDEnemyProperties>();
                if (comparisonScript.GetPathDist() < targetScript.GetPathDist())
                {
                    target = hits[i].collider.gameObject;
                    targetScript = comparisonScript;
                }
            }
        }
    }

    protected virtual void RotateToTarget()
    {
        float angle = Mathf.Atan2(target.transform.position.y - transform.position.y, target.transform.position.x - transform.position.x) * Mathf.Rad2Deg - 90f;

        Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    protected virtual bool isFacingTarget()
    {
        if (target == null)
        {
            return false;
        }

        Vector3 directionToTarget = (target.transform.position - transform.position).normalized;
        float dotProduct = Vector3.Dot(transform.up, directionToTarget);

        if (dotProduct > 0.95f)
        {
            return true;
        }
        else return false;
    }

    //The function below shows the tower's range in the editor, but not during gameplay
    //Should be changed at some point

    protected void OnDrawGizmosSelected()
    {
        Handles.color = Color.darkOrange;
        Handles.DrawWireDisc(transform.position, transform.forward, range);
    }
}
