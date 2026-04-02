using UnityEngine;

public class TDCameraLegacy : MonoBehaviour
{
    private Vector3 camReset;
    private Vector3 origin;
    private Vector3 difference;
    public float dragSpeed = 0.5f;

    public bool camDragging = false;

    private Vector3 lastMouseCoordinate = Vector3.zero;
    private Vector3 mouseDelta = Vector3.zero;

    void Start()
    {
        camReset = Camera.main.transform.position;
    }

    public void Update()
    {
        //use this to calculate mouse mosition relative to last
        mouseDelta = Input.mousePosition - lastMouseCoordinate; //mouseDelta

    }

    public void LateUpdate()
    {
        if (Input.GetMouseButton(0) && mouseDelta != Vector3.zero)
        {
            difference = (Camera.main.ScreenToViewportPoint(Input.mousePosition)) - Camera.main.transform.position;
            if (camDragging == false)
            {
                camDragging = true;
                origin = Camera.main.ScreenToViewportPoint(Input.mousePosition);
            }
        }
        else
        {
            camDragging = false;
        }
        if(camDragging == true)
        {
            Camera.main.transform.position = origin - difference;
        }

        if (Input.GetMouseButton(1))
        {
            Camera.main.transform.position = camReset;
        }

        lastMouseCoordinate = Input.mousePosition;
    }
}
