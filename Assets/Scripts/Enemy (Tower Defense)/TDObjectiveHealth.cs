using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TDObjectiveHealth : MonoBehaviour
{
    
    [SerializeField] private float health = 5f;
    [SerializeField] private TextMeshProUGUI ctrText;

    void Start()
    {
        UpdateDisplay();
    }

    public void DecrementHealth(float dmg) {  health -= dmg; UpdateDisplay();  }
    public float GetHealth() { return health; }
    public void CheckHealth()
    {
        Debug.Log(health);
        if (health <= 0)
        {
            SceneManager.LoadScene("GameOverMenu");
        }
    }

    public void UpdateDisplay()
    {
        ctrText.text = "Health: " + Mathf.Ceil(health).ToString();
    }
}
