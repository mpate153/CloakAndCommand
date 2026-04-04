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

    /// <summary>
    /// Finds <see cref="PlayerHealth"/> from any child of the player (vision target, collider root, etc.).
    /// </summary>
    public static PlayerHealth FindForGameplayTransform(Transform anyPlayerPart)
    {
        if (anyPlayerPart == null)
            return null;
        var h = anyPlayerPart.GetComponentInParent<PlayerHealth>();
        if (h != null)
            return h;
        h = anyPlayerPart.GetComponent<PlayerHealth>();
        if (h != null)
            return h;
        h = anyPlayerPart.GetComponentInChildren<PlayerHealth>(true);
        if (h != null)
            return h;
        return anyPlayerPart.root.GetComponentInChildren<PlayerHealth>(true);
    }

    public void DieFromEnemy()
    {
        if (_dead)
            return;
        _dead = true;

        var active = SceneManager.GetActiveScene();
        if (active.IsValid() && !string.IsNullOrEmpty(active.name) && active.name != gameOverSceneName)
            PlayerTracker.RecordLevel(active.name);

        DisableLivingPlayerImmediately();

        Time.timeScale = 1f;
        SceneManager.LoadScene(gameOverSceneName);
    }

    /// <summary>
    /// Stops movement, input, physics, and hits so the player is actually &quot;dead&quot; before the game-over scene loads.
    /// </summary>
    void DisableLivingPlayerImmediately()
    {
        GameObject root = transform.root.gameObject;

        foreach (var c in root.GetComponentsInChildren<PlayerMovement>(true))
            c.enabled = false;
        foreach (var c in root.GetComponentsInChildren<PlayerControls>(true))
            c.enabled = false;
        foreach (var c in root.GetComponentsInChildren<PlayerAttack>(true))
            c.enabled = false;
        foreach (var c in root.GetComponentsInChildren<PlayerLure>(true))
            c.enabled = false;

        foreach (var rb in root.GetComponentsInChildren<Rigidbody2D>(true))
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        foreach (var col in root.GetComponentsInChildren<Collider2D>(true))
            col.enabled = false;
    }
}
