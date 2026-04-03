using UnityEngine;

public class TDObjectiveHealth : MonoBehaviour
{
    public static TDObjectiveHealth Instance { get; set; }
    [SerializeField] private float health = 5f;
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

    public void DecrementHealth(float dmg) {  health -= dmg; }
    public float GetHealth() { return health; }
    public void CheckHealth()
    {
        Debug.Log(health);
        if (health <= 0)
        {
            Debug.Log("Game Over");
            Debug.Break();
        }
    }
}
