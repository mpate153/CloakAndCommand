using UnityEngine;

public class Drag_and_Drop : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private GameObject turretPrefab;

    private Vector3 startPosition;

    private bool draggable = true;


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
        Collider2D hitcollider = Physics2D.OverlapPoint(transform.position, LayerMask.GetMask("TowerBase"));
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
    
    public Vector3 GetMousePosition()
    {
        Vector3 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        pos.z = 0f;
        return pos;
    }
}
