using UnityEngine;

public class TurretMultihit : Turret
{
    [SerializeField] private int bulletCount = 3;

    private GameObject[] targetList;

    private void Start()
    {
        targetList = new GameObject[bulletCount];

    }

    protected override void Update()
    {
        findTarget();

        if (targetList[0] != null)
        {
            RotateToTarget();
        }

        if (attackTimer < attackInterval)
        {
            attackTimer += Time.deltaTime;
        }

        if (attackTimer >= attackInterval)
        {
            if (targetList[0] != null && isFacingTarget()) //the targetList[0] != null check isn't necessary since isFacingTarget already checks this; i've kept it here for clarity and future proofing, in case isFacingTarget is changed in the future
            {
                attack();
                attackTimer = 0;
            }
        }
    }

    protected override void attack()
    {
        for (int i = 0; i < targetList.Length; i++)
        {
            if (targetList[i] != null)
            {
                GameObject bulletInstance = Instantiate(bulletPrefab, bulletSpawnPoint.transform.position, Quaternion.identity);
                Bullet bulletScript = bulletInstance.GetComponent<Bullet>();
                bulletScript.SetTarget(targetList[i]);
                bulletScript.SetDamage(damage);
            }
        }
    }

    protected override void findTarget()
    {
        for (int i = 0; i < bulletCount; i++)
        {
            targetList[i] = null;
        }

        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, range, (Vector2)transform.position, 0f, enemyMask);

        if (hits.Length > 0)
        {
            int j;
            TDEnemyProperties[] targetScripts = new TDEnemyProperties[bulletCount];

            for (j = 0; j < Mathf.Min(bulletCount, hits.Length); j++)
            {
                targetList[j] = hits[j].collider.gameObject;
                targetScripts[j] = targetList[j].GetComponent<TDEnemyProperties>();
            }

            TDEnemyProperties comparisonScript;

            while (j < hits.Length)
            {
                comparisonScript = hits[j].collider.gameObject.GetComponent<TDEnemyProperties>();
                for (int k = 0; k < targetList.Length; k++)
                {
                    if (comparisonScript.GetPathDist() < targetScripts[k].GetPathDist())
                    {
                        targetList[k] = hits[j].collider.gameObject;
                        targetScripts[k] = comparisonScript;
                    }
                }
            }
        }
    }

    protected override void RotateToTarget()
    {
        float angle = Mathf.Atan2(targetList[0].transform.position.y - transform.position.y, targetList[0].transform.position.x - transform.position.x) * Mathf.Rad2Deg - 90f;

        Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    protected override bool isFacingTarget()
    {
        if (targetList[0] == null)
        {
            return false;
        }

        Vector3 directionToTarget = (targetList[0].transform.position - transform.position).normalized;
        float dotProduct = Vector3.Dot(transform.up, directionToTarget);

        if (dotProduct > 0.95f)
        {
            return true;
        }
        else return false;
    }
}
