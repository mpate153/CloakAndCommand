using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Place a trigger <see cref="Collider2D"/> (is trigger) on this object. When the player enters, loads the target scene
/// via <see cref="SceneNavigator.LoadScene"/> so back-navigation history is correct.
/// </summary>
[DisallowMultipleComponent]
public sealed class SceneTransitionTrigger : MonoBehaviour
{
    [Tooltip("If unset, uses first SceneNavigator in loaded scenes.")]
    [SerializeField] SceneNavigator sceneNavigator;

    [Tooltip("Must match a scene in File → Build Settings.")]
    [SerializeField] string targetSceneName;

    [SerializeField] string playerTag = "Player";

    [Tooltip("Also accept root colliders with PlayerMovement (if tag missing).")]
    [SerializeField] bool acceptPlayerMovementIfUntagged = true;

    [Tooltip("If true, stores target scene as last level (see PlayerTracker) before loading.")]
    [SerializeField] bool recordTargetLevelOnEnter;

    [SerializeField] UnityEvent onPlayerEntered;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        onPlayerEntered?.Invoke();

        if (recordTargetLevelOnEnter && !string.IsNullOrEmpty(targetSceneName))
            PlayerTracker.RecordLevel(targetSceneName);

        if (string.IsNullOrEmpty(targetSceneName))
            return;

        SceneNavigator nav = sceneNavigator != null
            ? sceneNavigator
            : FindFirstObjectByType<SceneNavigator>();

        if (nav != null)
        {
            nav.LoadScene(targetSceneName);
            return;
        }

        Debug.LogWarning(
            "[SceneTransitionTrigger] No SceneNavigator in scene; loading without navigation history. Add SceneNavigator (e.g. on Main Camera or a bootstrap object).",
            this);
        SceneManager.LoadScene(targetSceneName);
    }

    bool IsPlayer(Collider2D other)
    {
        if (other == null) return false;
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag))
            return true;
        return acceptPlayerMovementIfUntagged &&
               other.GetComponentInParent<PlayerMovement>() != null;
    }
}
