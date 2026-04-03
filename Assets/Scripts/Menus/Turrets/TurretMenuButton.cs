using UnityEngine;

public class TurretMenuButton : MonoBehaviour
{
    [SerializeField] private GameObject turretUIPrefab;

    public void spawnUITurret()
    {
        Instantiate(turretUIPrefab, GetMousePosition(), Quaternion.identity);
    }

    private Vector3 GetMousePosition()
    {
        Vector3 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        pos.z = 0f;
        return pos;
    }
}
