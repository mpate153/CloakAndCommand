using UnityEngine;
using System.Collections.Generic;

public class PierceBullet : Bullet
{
    [SerializeField] private float pierceDuration = 0.5f;
    private float pierceTimer = 0f;
    private bool hasHit = false;
    private List<GameObject> hits = new List<GameObject>();
    Vector3 moveDirection;
    Vector3 lastPosition;

    private void Start()
    {
        lastPosition = transform.position;
    }

    protected override void Update()
    {
        if (!hasHit)
        {
            if (bulletTarget != null)
            {
                lastPosition = transform.position;
                transform.position = Vector3.MoveTowards(transform.position, bulletTarget.transform.position, speed * Time.deltaTime);
                moveDirection = (transform.position - lastPosition).normalized;
            }
            else
            {
                hasHit = true;
            }
        }
        else
        {
            Debug.Log("Piercing");
            transform.position += moveDirection * speed * Time.deltaTime;
            pierceTimer += Time.deltaTime;
            if (pierceTimer >= pierceDuration)
            {
                Destroy(gameObject);
            }
        }
        
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        for (int i = 0; i < hits.Count; i++)
        {
            if (hits[i] == other.gameObject)
            {
                return;
            }
        }

        hits.Add(other.gameObject);
        TDEnemyProperties objectHitScript = other.gameObject.GetComponent<TDEnemyProperties>();
        objectHitScript.TakeDamage(bulletDamage);
        hasHit = true;
    }
}
