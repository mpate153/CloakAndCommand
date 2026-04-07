using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Remembers enemy roots removed during play so <see cref="SaveManager"/> can write them to JSON and
/// destroy matching scene instances on load (otherwise killed enemies respawn from the scene file).
/// </summary>
public static class DestroyedEnemySaveTracker
{
    static readonly Dictionary<string, HashSet<string>> IdsByScene =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    static readonly Dictionary<string, HashSet<string>> PathsByScene =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

    /// <summary>Call immediately before <c>Destroy</c> on an enemy root (or any transform whose root should stay dead).</summary>
    public static void RegisterKilledEnemyRoot(Transform root)
    {
        if (root == null)
            return;
        GameObject go = root.gameObject;
        if (!go.scene.IsValid() || string.IsNullOrEmpty(go.scene.name))
            return;

        string scene = go.scene.name;
        Transform t = root.root;

        if (t.TryGetComponent<ScenePersistedIdentity>(out var id) && !string.IsNullOrEmpty(id.PersistentId))
        {
            EnsureSet(IdsByScene, scene).Add(id.PersistentId);
            return;
        }

        EnsureSet(PathsByScene, scene).Add(BuildHierarchyPath(t));
    }

    public static void ClearScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;
        IdsByScene.Remove(sceneName);
        PathsByScene.Remove(sceneName);
    }

    public static void ReplaceFromSnapshot(string sceneName, string[] persistentIds, string[] hierarchyPaths)
    {
        ClearScene(sceneName);
        HashSet<string> ids = EnsureSet(IdsByScene, sceneName);
        if (persistentIds != null)
        {
            for (int i = 0; i < persistentIds.Length; i++)
            {
                if (!string.IsNullOrEmpty(persistentIds[i]))
                    ids.Add(persistentIds[i]);
            }
        }
        HashSet<string> paths = EnsureSet(PathsByScene, sceneName);
        if (hierarchyPaths != null)
        {
            for (int i = 0; i < hierarchyPaths.Length; i++)
            {
                if (!string.IsNullOrEmpty(hierarchyPaths[i]))
                    paths.Add(hierarchyPaths[i]);
            }
        }
    }

    public static bool IsPersistentIdMarkedDestroyed(string sceneName, string persistentId)
    {
        if (string.IsNullOrEmpty(sceneName) || string.IsNullOrEmpty(persistentId))
            return false;
        return IdsByScene.TryGetValue(sceneName, out var set) && set.Contains(persistentId);
    }

    public static void CopyToArrays(string sceneName, out string[] ids, out string[] paths)
    {
        ids = IdsByScene.TryGetValue(sceneName, out var iset) && iset.Count > 0
            ? ToSortedArray(iset)
            : Array.Empty<string>();
        paths = PathsByScene.TryGetValue(sceneName, out var pset) && pset.Count > 0
            ? ToSortedArray(pset)
            : Array.Empty<string>();
    }

    static HashSet<string> EnsureSet(Dictionary<string, HashSet<string>> dict, string scene)
    {
        if (!dict.TryGetValue(scene, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            dict[scene] = set;
        }
        return set;
    }

    static string[] ToSortedArray(HashSet<string> set)
    {
        var arr = new string[set.Count];
        set.CopyTo(arr);
        Array.Sort(arr, StringComparer.Ordinal);
        return arr;
    }

    static string BuildHierarchyPath(Transform t)
    {
        var stack = new Stack<string>();
        Transform cur = t;
        while (cur != null)
        {
            stack.Push(cur.name);
            cur = cur.parent;
        }
        return string.Join("/", stack);
    }
}
