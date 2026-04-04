#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor helpers for <see cref="SaveManager"/> / <see cref="ScenePersistedIdentity"/>.
/// </summary>
static class PersistenceSceneTools
{
    const string MenuRoot = "Tools/Persistence/";

    [MenuItem(MenuRoot + "Add Scene Persisted Identity To Selection", false, 10)]
    static void AddIdentityToSelection()
    {
        Undo.SetCurrentGroupName("Add ScenePersistedIdentity");
        int group = Undo.GetCurrentGroup();
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go == null) continue;
            if (go.GetComponent<ScenePersistedIdentity>() != null)
                continue;
            Undo.AddComponent<ScenePersistedIdentity>(go);
        }
        Undo.CollapseUndoOperations(group);
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go == null) continue;
            var id = go.GetComponent<ScenePersistedIdentity>();
            if (id != null)
                id.EnsureGeneratedId();
        }
    }

    [MenuItem(MenuRoot + "Add Scene Persisted Identity To Selection", true)]
    static bool ValidateAddIdentity() => Selection.gameObjects.Length > 0;
}
#endif
