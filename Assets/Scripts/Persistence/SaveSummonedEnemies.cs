using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Marks an enemy instance spawned by a watcher (sprinter reinforcement or bulwark ring) so
/// <see cref="SaveManager"/> writes a dedicated <c>saveSummonedEnemies</c> block with guaranteed prefab GUID + pose + AI.
/// </summary>
[DisallowMultipleComponent]
public class SaveSummonedEnemies : MonoBehaviour
{
    [SerializeField] string savedPrefabGuid;
    [SerializeField] string savedResourcesPath;

    public string SavedPrefabGuid => savedPrefabGuid;
    public string SavedResourcesPath => savedResourcesPath;

    /// <param name="prefabSource">The prefab passed to <see cref="Object.Instantiate"/> (sprinter or bulwark asset).</param>
    public static void Tag(GameObject instance, GameObject prefabSource)
    {
        if (instance == null)
            return;

        SaveSummonedEnemies link = instance.GetComponent<SaveSummonedEnemies>();
        if (link == null)
            link = instance.AddComponent<SaveSummonedEnemies>();

        ScenePersistedIdentity id = instance.GetComponent<ScenePersistedIdentity>();
        if (id == null)
            id = instance.AddComponent<ScenePersistedIdentity>();
        id.EnsureGeneratedId();

#if UNITY_EDITOR
        if (prefabSource != null)
        {
            string assetPath = AssetDatabase.GetAssetPath(prefabSource);
            if (!string.IsNullOrEmpty(assetPath))
                link.savedPrefabGuid = AssetDatabase.AssetPathToGUID(assetPath);
        }
#endif
        if (string.IsNullOrEmpty(link.savedPrefabGuid) && !string.IsNullOrEmpty(id.CachedRespawnPrefabGuid))
            link.savedPrefabGuid = id.CachedRespawnPrefabGuid;

        link.savedResourcesPath = id.ResourcesSpawnPath ?? "";
    }
}
