using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

public class EPathing : MonoBehaviour
{

    public GameObject[] targets;
    private Rigidbody2D myBody;
    [SerializeField]
    private float speed = 1.0f;
    [SerializeField]
    private float playerDetectRange = 5;

    private Transform currTarget;


    void Start()
    {
        myBody = GetComponent<Rigidbody2D>();

        //Attempt to find each target that is active in the scene
        int count = 0;
        foreach (GameObject i in targets)
        {
            targets[count] = GameObject.Find(i.name);   
            count++;
        }
    }


    void Update()
    {
        currTarget = UpdateTarget();
        
        Vector3 direction = (currTarget.transform.position - transform.position).normalized;
        Vector3 movePosition = transform.position + speed * Time.deltaTime * direction;
        myBody.MovePosition(movePosition);
    }

    private Transform UpdateTarget()
    {
        float dist;

        foreach(GameObject i in targets)
        {
            if (i.name != "Player") continue;
            dist = Vector3.Distance(myBody.position, i.transform.position);
            if (dist < playerDetectRange) return i.transform;
        }

        return GameObject.FindGameObjectWithTag("TowerOrigin").transform;
    }
}
