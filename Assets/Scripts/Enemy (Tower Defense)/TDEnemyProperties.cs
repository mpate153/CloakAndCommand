using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;
using UnityEngine.Rendering;

public enum enemyType
{
    Watcher,
    Sprinter,
    Bulwark
}

public class TDEnemyProperties: MonoBehaviour
{
    private Rigidbody2D myBody;
    private SpriteRenderer sr;
    [Header("Base Stats")]
    [SerializeField] private float health = 10f;
    [SerializeField] private float speed = 1.0f;
    [SerializeField] private float damage = 2.0f;
    [SerializeField] private float reachDistance = 0.05f;

    [Header("Spawn Rates")]
    [Tooltip("Ensure Watcher > Sprinter > Bulwark and all sum to 1")]
    [SerializeField] private float watcherRate = 0.6f;
    [SerializeField] private float sprinterRate = 0.3f;
    [SerializeField] private float bulwarkRate = 0.1f;
    

    [Header("Watcher Mods")]
    [SerializeField] private float wHealthMod = 1.0f;
    [SerializeField] private float wSpeedMod = 1.0f;
    [SerializeField] private float wDmgMod = 1.0f;
    [SerializeField] private Color wColor = Color.green;

    [Header("Sprinter Mods")]
    [SerializeField] private float sHealthMod = 1.0f;
    [SerializeField] private float sSpeedMod = 1.0f;
    [SerializeField] private float sDmgMod = 1.0f;
    [SerializeField] private Color sColor = Color.blue;

    [Header("Bulwark Mods")]
    [SerializeField] private float bHealthMod = 1.0f;
    [SerializeField] private float bSpeedMod = 1.0f;
    [SerializeField] private float bDmgMod = 1.0f;
    [SerializeField] private Color bColor = Color.red;

    [Header("Pathing")]
    [SerializeField] public GameObject targetPathObject;
    [SerializeField] private bool togglePathDist = false;
    [SerializeField] private bool toggleHealth = false;
    [SerializeField] private float functionDelaySeconds = 2f;

    [SerializeField] public GameObject targetObjectiveObject;
    private int currWaypointIndex = 0;
    private EnemyPathing targetScript;
    private float finalDist;
    private int inspectIndex;

    private bool affectedByDOT = false;
    private float DOTDamage;
    private float DOTInterval;
    private float DOTTimer = 0f;

    void Awake()
    {
        myBody = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();

        //Dunno, create a rand num generator and alter existing stats?
        float genType = Random.value;
        ApplyModifiers(genType);

        if (targetPathObject != null)
        {
            targetScript = targetPathObject.GetComponent<EnemyPathing>();
            if (targetScript != null)
            {
                //Minus 1 is used to stay within the list bounds
                currWaypointIndex = targetScript.GetTransformList().Count - 1; //Due to how A* search works, it back tracks from the end to the start, meaning the waypoints in the list are backwards, hence we start at the end of the list
            }
        }
        else
        {
            //Due to TDEnemySpawnManager Instantiating the enemy object, this will very likely trigger:
            this.enabled = false; //Disable update until correct path data is referenced
        }

        if (togglePathDist)
        {
            StartCoroutine(DisplayPathDist());
        }

        if (toggleHealth)
        {
            StartCoroutine(DisplayCurrHealth());
        }
    }

    IEnumerator DisplayPathDist()
    {
        while (true)
        {
            Debug.Log(GetPathDist());
            yield return new WaitForSeconds(functionDelaySeconds);
        }
    }

    IEnumerator DisplayCurrHealth()
    {
        while (true)
        {
            Debug.Log(health);
            yield return new WaitForSeconds(functionDelaySeconds);
        }
    }

    void Update()
    {
        if (affectedByDOT)
        {
            if (DOTTimer < DOTInterval)
            {
                DOTTimer += Time.deltaTime;
            }

            if (DOTTimer > DOTInterval)
            {
                TakeDamage(DOTDamage);
                DOTTimer = 0;
            }
        }
        if (health <= 0)
        {
            TDEnemyCount.Instance.DecrementCount(); //Will go into negatives if toggleCount disabled
            TDEnemyCount.Instance.IncrementDefeat();
            TDEnemyCount.Instance.CheckVictory();
            Debug.Log("Defeated: " + TDEnemyCount.Instance.GetDefeatCount());
            DestroyedEnemySaveTracker.RegisterKilledEnemyRoot(transform.root);
            Destroy(gameObject);
        }
        Move(); //Move is used since Update cannot be disabled; Enabled set to false stops enemy movement
    }

    //Called from TDEnemySpawnManager, sets path by referencing TDPathManager list (which should already include the generated path(s) in the inspector)
    public void SetPath(int element)
    {
        targetPathObject = TDPathManager.instance.GetPathObject(element);
        targetScript = targetPathObject.GetComponent<EnemyPathing>();
        currWaypointIndex = targetScript.GetTransformList().Count - 1;

        targetObjectiveObject = TDObjectiveManager.instance.GetCounter(element);
        this.enabled = true;
    }

    //Bad implementation
    public void ApplyModifiers(float rand)
    {
        //Watchers
        if(rand <= watcherRate)
        {
            health *= wHealthMod;
            speed *= wSpeedMod;
            damage *= wDmgMod;
            sr.color = wColor;
            return;
        }
        //Sprinters
        else if (rand > watcherRate && rand <= (watcherRate+sprinterRate))
        {
            health *= sHealthMod;
            speed *= sSpeedMod;
            damage *= sDmgMod;
            sr.color = sColor;
            return;
        }
        //Bulwarks
        else if(rand > (watcherRate + sprinterRate))
        {
            health *= bHealthMod;
            speed *= bSpeedMod;
            damage *= bDmgMod;
            sr.color = bColor;
            return;
        }
    }

    private void Move()
    {
        //Set the direction and movePosition towards the "next" element in the list   
        Vector3 direction = (targetScript.GetTransformList()[currWaypointIndex].position - transform.position).normalized;
        Vector3 targetPos = targetScript.GetTransformList()[currWaypointIndex].position;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
        transform.up = direction;

        if (Vector3.Distance(transform.position, targetPos) < reachDistance)
        {
            //Decrease index to properly traverse the intended path
            currWaypointIndex--;

            if (currWaypointIndex < 0)
            {
                enabled = false; //stops moving the object by disabling the Move function
                Debug.Log("EndReached");
                TDEnemyCount.Instance.DecrementCount();
                TDEnemyCount.Instance.CheckVictory();
                targetObjectiveObject.GetComponent<TDObjectiveHealth>().DecrementHealth(damage);
                targetObjectiveObject.GetComponent<TDObjectiveHealth>().CheckHealth();
                DestroyedEnemySaveTracker.RegisterKilledEnemyRoot(transform.root);
                Destroy(gameObject);
            }
        }
    }

    public void TakeDamage(float damage)
    {
        health -= damage;
    }

    public void SufferDOT(float damage, float interval)
    {
        if (!affectedByDOT)
        {
            affectedByDOT = true;
            DOTDamage = damage;
            DOTInterval = interval;
            ParticleSystem ps = gameObject.GetComponent<ParticleSystem>();
            ps.Play();
        }
        else
        {
            if (damage * interval > DOTDamage * DOTInterval)
            {
                DOTDamage = damage;
                DOTInterval = interval;
            }
        }
    }



    public float GetPathDist()
    {
        //reset finalDist and inspectIndex variables
        finalDist = 0;
        inspectIndex = currWaypointIndex;

        if (inspectIndex > 0)
        {
            //Calculate distance between current point and next waypoint
            finalDist += Vector3.Distance(transform.position, targetScript.GetTransformList()[inspectIndex].position);
            //Then find the sum of the remaining unhit checkpoints
            for (int i = inspectIndex; i > 0; i--)
            {
                finalDist += Vector3.Distance(targetScript.GetTransformList()[i].position, targetScript.GetTransformList()[i - 1].position);
            }
        }
        return finalDist;
    }
}