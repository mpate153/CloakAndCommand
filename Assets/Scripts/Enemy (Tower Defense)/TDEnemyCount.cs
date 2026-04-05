using System.Collections;
using UnityEngine;

public class TDEnemyCount : MonoBehaviour
{
    //Set to static Instance so that scripts referencing these variables don't need to create a local script variable
    //Must include gameObject in scene with this script attachted; No clue if loading scenes breaks this counter
    public static TDEnemyCount Instance { get; set; }

    [SerializeField] private GameObject sceneTransition;

    private int eCount = 0; //Tracks current # of enemies
    private int eSpawned = 0; //Tracks how many spawned
    private int eTotal = 0; //Tracks how many will spawn
    private int eDefeat = 0; //Tracks how many were defeated

    private void Awake()
    {
        //Simpleton stuff
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        //DontDestroyOnLoad(gameObject); //Allows for objects to persist between scenes
    }

    public void IncrementCount() {  eCount++; }
    public void DecrementCount() { eCount--; }
    public void IncrementSpawnCount() {  eSpawned++; }
    public void IncrementDefeat() { eDefeat++; }
    public void SetTotal(int total) { eTotal = total; }
    public int GetCount() { return eCount; }
    public int GetDefeatCount() { return eDefeat; }
    public void CheckVictory()
    {
        //All enemies defeated
        if (eDefeat == eTotal)
        {
            Debug.Log("All enemies cleared");
            //I think this works??
            sceneTransition.GetComponent<SceneNavigator>().GoBackToPreviousScene(); //Should send back
        }
        //All enemies spawned but NOT all defeated
        if (eCount == 0 && eSpawned == eTotal)
        {
            Debug.Log("All enemies managed"); //This message will always print
            sceneTransition.GetComponent<SceneNavigator>().GoBackToPreviousScene();
        }
    }
}
