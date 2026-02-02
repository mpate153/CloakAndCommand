using UnityEngine;
using UnityEngine.Rendering;

public class Bullet : MonoBehaviour
{

    [Header("Attributes")]
    [SerializeField] private float speed = 3f;

    private Transform bulletTarget;

    private void Update()
    {
        if (bulletTarget != null)
        {
            transform.position = Vector3.MoveTowards(transform.position, bulletTarget.position, speed * Time.deltaTime);
        }
    }

    public void SetTarget(Transform target)
    {
        bulletTarget = target;
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        Debug.Log("Bullet Destroyed");
        Destroy(gameObject);
    }
}
