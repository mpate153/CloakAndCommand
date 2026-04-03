using UnityEngine;

public class UIElement : MonoBehaviour
{
    //Commented code is old code that is no longer being used
    //It has not been deleted in case its needed again

    [Header("References")]
    [SerializeField] private GameObject turretPrefab;

    //private Vector3 startPosition;

    //private bool draggable = true;

    private void Update()
    {
        transform.position = GetMousePosition();
    }

    private void OnMouseDown()
    {
        Collider2D hitcollider = Physics2D.OverlapPoint(transform.position, LayerMask.GetMask("TurretBase"));
        if (hitcollider != null && hitcollider.CompareTag("Placeable"))
        {
            hitcollider.gameObject.tag = "Occupied";
            Instantiate(turretPrefab, hitcollider.transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }

    public Vector3 GetMousePosition()
    {
        Vector3 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        pos.z = 0f;
        return pos;
    }

    /*
    private void OnMouseDown()
    {
        if (draggable)
        {
            startPosition = transform.position;
            transform.position = GetMousePosition();
        }

    }

    private void OnMouseDrag()
    {
        if (draggable)
        {
            transform.position = GetMousePosition();
        }
    }

    private void OnMouseUp()
    {
        Collider2D hitcollider = Physics2D.OverlapPoint(transform.position, LayerMask.GetMask("TurretBase"));
        if (hitcollider != null && hitcollider.CompareTag("Placeable"))
        {
            hitcollider.gameObject.tag = "Occupied";
            Instantiate(turretPrefab, hitcollider.transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
        else
        {
            transform.position = startPosition;
        }
    }
    */
}
