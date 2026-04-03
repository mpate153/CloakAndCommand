using UnityEngine;

public class FreezeRotation : MonoBehaviour
{
    Vector3 initialPosition;
    Quaternion initialRotation;

    void Start()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    void LateUpdate()
    {
        transform.position = initialPosition;
        transform.rotation = initialRotation;
    }
}
