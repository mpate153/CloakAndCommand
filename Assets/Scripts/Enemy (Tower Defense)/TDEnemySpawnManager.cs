using System.Collections;
using UnityEngine;

public class EnemySpawnManager : MonoBehaviour
{
    public GameObject Enemy;
    public Transform[] spawnPts;

    [SerializeField] private float initDelay = 5f;
    [SerializeField] private float spawnDelay = 5f;
    [SerializeField] private int spawnLimit = 0;
    [SerializeField] private bool toggleCount = false;
    [SerializeField] private float functionDelaySeconds = 1f;
    [SerializeField] private bool stopSpawn = false;

    private int spawnCount = 0;
    void Start()
    {
        if(TDEnemyCount.Instance != null)
        {
            TDEnemyCount.Instance.SetTotal(spawnLimit);
        }
        else
        {
            Debug.Log("What?");
        }
        StartCoroutine(InitDelay());
        if (toggleCount)
        {
            StartCoroutine(DisplayCurrCount());
        }
    }

    IEnumerator InitDelay()
    {
        yield return new WaitForSeconds(initDelay);
        StartCoroutine(SpawnEnemies());
    }

    IEnumerator SpawnEnemies()
    {
        Vector3 offset = new(0.5f, 0.5f, 0f);
        while (!stopSpawn)
        {
            yield return new WaitForSeconds(spawnDelay);
            if (spawnCount >= spawnLimit)
            {
                stopSpawn = true;
                continue;
            }


            int randInt = Random.Range(0, spawnPts.Length);
            Transform spawnPt = spawnPts[randInt]; //Array size refers to total number of points present in the scene

            GameObject enemyInstance = Instantiate(Enemy, spawnPt.position + offset, spawnPt.rotation); //offset is used to reduce complexity of EnemyPathing implementation
            enemyInstance.GetComponent<TDEnemyProperties>().SetPath(randInt);
            //Correlate spawn points to the start of the intended path

            TDEnemyCount.Instance.IncrementCount();
            TDEnemyCount.Instance.IncrementSpawnCount();
            spawnCount++;
        }
    }

    //Displays count in "real-time"
    IEnumerator DisplayCurrCount()
    {
        while (true)
        {
            Debug.Log(TDEnemyCount.Instance.GetCount());
            yield return new WaitForSeconds(functionDelaySeconds);
        }
    }
}
