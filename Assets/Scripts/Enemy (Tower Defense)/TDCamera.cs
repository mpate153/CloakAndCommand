using UnityEngine;
using UnityEngine.Tilemaps;
using static UnityEngine.RuleTile.TilingRuleOutput;

//pressing a or d will horizintally scroll the camera; pressing middle mouse resets the camera
//add boundaries based on the level limits

public class TDCamera : MonoBehaviour
{
    private Vector3 camReset;
    private float minX = 0;
    private float maxX = 0;
    [SerializeField] private float updateSpeed = 1f;
    [SerializeField] public Tilemap targetTileMap; //used to help determine max/min x

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //Reference camera object in scene
        Camera cam = Camera.main;

        //Define camera reset
        camReset = transform.position;

        //Determine camera boundaries
        if(targetTileMap != null)
        {
            targetTileMap.CompressBounds();
            //1 is added/subtracted to guarantee enemy spawn/despawn isn't visible
            minX = targetTileMap.cellBounds.min.x + (cam.orthographicSize * cam.aspect) + 1; //add camerabounds; ~11.5 units from center
            maxX = targetTileMap.cellBounds.max.x - (cam.orthographicSize * cam.aspect) - 1; //subtract camerabounds
            //Debug.Log("Min X: " + minX + "\nMax X: " + maxX);
        }
        else
        {
            Debug.Log("Couldn't find tilemap");
        }
    }

    // Update is called once per frame
    void LateUpdate()
    {
        //Use clamp to prevent the camera from moveing across the dynamically determined boundaries
        float clampedX = Mathf.Clamp(transform.position.x, minX, maxX);
        float clampedY = Mathf.Clamp(transform.position.y, 0, 0); //Needed for second argument; Always set top 0
        transform.position = new Vector3(clampedX, clampedY, transform.position.z);

        if (Input.GetAxisRaw("Horizontal") != 0)
        {
            //Debug.Log("Input:" + Input.GetAxisRaw("Horizontal"));
            transform.Translate(Time.deltaTime * Input.GetAxisRaw("Horizontal") * updateSpeed * Vector3.right);
        }

        if (Input.GetMouseButton(2)) //middle mouse
        {
            transform.position = camReset;
        }
    }
}
