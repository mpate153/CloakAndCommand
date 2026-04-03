using UnityEngine;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Remembers the last gameplay scene for retries (PlayerPrefs + static cache). Add to an object in each level;
/// on enable it records that scene unless <see cref="levelSceneNameOverride"/> is set.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerTracker : MonoBehaviour
{
    const string PrefsKey = "PlayerLastLevelScene";

    static string _cached;

    [Tooltip("If set, this name is stored instead of this object's scene.")]
    [SerializeField] string levelSceneNameOverride;

    [Tooltip("If true, records again every time this object is enabled.")]
    [SerializeField] bool recordOnEnable = true;

    public static void RecordLevel(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;
        _cached = sceneName;
        PlayerPrefs.SetString(PrefsKey, sceneName);
        PlayerPrefs.Save();
    }

    public static string GetLastLevelScene()
    {
        if (!string.IsNullOrEmpty(_cached))
            return _cached;
        return PlayerPrefs.GetString(PrefsKey, string.Empty);
    }

    void OnEnable()
    {
        if (!recordOnEnable)
            return;
        string n = string.IsNullOrEmpty(levelSceneNameOverride)
            ? gameObject.scene.name
            : levelSceneNameOverride;
        RecordLevel(n);
    }

#if UNITY_EDITOR
    [ContextMenu("Record Current Scene Now")]
    void EditorRecordNow()
    {
        RecordLevel(string.IsNullOrEmpty(levelSceneNameOverride)
            ? EditorSceneManager.GetActiveScene().name
            : levelSceneNameOverride);
    }
#endif
}
