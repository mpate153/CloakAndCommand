using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-hit death from enemies; loads the game-over scene. Records the current level for retry unless you are already in the game-over scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerHealth : MonoBehaviour
{
    [Tooltip("Scene to load on death (must be in Build Settings).")]
    [SerializeField] string gameOverSceneName = "GameOver";

    bool _dead;

    public bool IsDead => _dead;

    public void DieFromEnemy()
    {
        if (_dead)
            return;
        _dead = true;

        var active = SceneManager.GetActiveScene();
        if (active.IsValid() && !string.IsNullOrEmpty(active.name) && active.name != gameOverSceneName)
            PlayerTracker.RecordLevel(active.name);

        Time.timeScale = 1f;
        SceneManager.LoadScene(gameOverSceneName);
    }
}
