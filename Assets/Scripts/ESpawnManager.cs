using System.Collections;
using UnityEngine;

public class ESpawnManager : MonoBehaviour
{
    public GameObject Enemy;
    public Transform[] spawnPts;

    [SerializeField]
    private float spawnDelay = 5f;
    [SerializeField]
    private int spawnLimit = 0;
    [SerializeField]
    private bool stopSpawn = false;

    private int spawnCount = 0;

    void Start()
    {
        StartCoroutine(SpawnEnemies());
    }

    IEnumerator SpawnEnemies()
    {
        while (!stopSpawn)
        {
            yield return new WaitForSeconds(spawnDelay);

            int randInt = Random.Range(0, spawnPts.Length);
            Transform spawnPt = spawnPts[randInt]; //Array size refers to total number of points present in the scene

            Instantiate(Enemy, spawnPt.position, spawnPt.rotation);
            spawnCount++;

            if(spawnCount >= spawnLimit)
            {
                stopSpawn = true;
            }
        }
    }
}
