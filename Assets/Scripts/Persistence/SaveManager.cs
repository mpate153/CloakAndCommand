using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Saves world positions (and rotation/scale) of objects in a scene to JSON under
/// <see cref="Application.persistentDataPath"/> — one file per scene name, e.g. <c>SceneLayout_StealthConcept.json</c>.
/// Add to a GameObject (e.g. named SaveManager); call <see cref="SaveLayout"/> / <see cref="LoadLayout"/>
/// from buttons or enable auto load/save flags.
/// </summary>
[DisallowMultipleComponent]
public class SaveManager : MonoBehaviour
{
    [Tooltip("If true, applies saved layout when this scene loads (after one frame so spawners can finish).")]
    [SerializeField] bool loadOnStart = true;

    [Tooltip("If true, writes layout when the app quits (editor: stopping play mode).")]
    [SerializeField] bool saveOnApplicationQuit = true;

    [Tooltip("Delay before LoadLayout on start (seconds) so prefabs/instantiation can run first.")]
    [SerializeField] float loadDelaySeconds = 0.1f;

    [Header("Load presentation")]
    [Tooltip("When a layout file exists and loadOnStart runs, black out the screen until LoadLayout finishes so you do not see default poses then a jump.")]
    [SerializeField] bool hideSceneUntilLayoutApplied = true;

    [Tooltip("Unscaled seconds to fade out the blackout after layout is applied. 0 = hide immediately.")]
    [SerializeField] float layoutRevealFadeOutSeconds = 0.12f;

    [Header("Filters")]
    [Tooltip("Skip transforms under any Canvas (UI).")]
    [SerializeField] bool skipUI = true;

    [Tooltip("Skip objects with a Camera.")]
    [SerializeField] bool skipCameras = true;

    [Tooltip("Skip objects with a Light (directional, etc.).")]
    [SerializeField] bool skipLights = true;

    [Header("Pause")]
    [Tooltip("When the pause menu opens, save scene layout and write gamesave.json (settings, etc.).")]
    [SerializeField] bool saveWhenPaused = true;

    [Header("Auto-save (interval)")]
    [Tooltip("0 = off. While playing, save layout + gamesave this often (scaled seconds). Uses one scene walk + disk write each tick — avoid very low values on huge scenes.")]
    [SerializeField] float autoSaveIntervalSeconds = 15f;

    [Header("Debug")]
    [SerializeField] bool logSaveLoad;

    const string FilePrefix = "SceneLayout_";

    /// <summary>Increment when JSON shape changes; still loads older files without gameplay blocks.</summary>
    public const int CurrentSnapshotVersion = 7;

    Coroutine _autoSaveRoutine;
    GameObject _loadCurtainRoot;
    CanvasGroup _loadCurtainGroup;
    Coroutine _curtainFadeRoutine;

    void Awake()
    {
        TryShowLoadCurtainIfNeeded();
    }

    void OnEnable()
    {
        PauseMenu.GamePaused += OnGamePaused;
        if (autoSaveIntervalSeconds > 0f)
            _autoSaveRoutine = StartCoroutine(AutoSaveLoop());
    }

    void OnDisable()
    {
        PauseMenu.GamePaused -= OnGamePaused;
        if (_autoSaveRoutine != null)
        {
            StopCoroutine(_autoSaveRoutine);
            _autoSaveRoutine = null;
        }
        if (_curtainFadeRoutine != null)
        {
            StopCoroutine(_curtainFadeRoutine);
            _curtainFadeRoutine = null;
        }
    }

    void OnGamePaused()
    {
        if (!saveWhenPaused)
            return;
        PersistLayoutAndSettings();
    }

    IEnumerator AutoSaveLoop()
    {
        while (enabled && autoSaveIntervalSeconds > 0f)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, autoSaveIntervalSeconds));
            PersistLayoutAndSettings();
        }
    }

    void PersistLayoutAndSettings()
    {
        SaveLayout();
        SaveGame.PersistSettingsFromRuntime();
    }

    void Start()
    {
        // Awake can run while the scene is still opening; isLoaded may be false there. Cover that case too.
        TryShowLoadCurtainIfNeeded();

        if (loadOnStart && loadDelaySeconds <= 0f)
            LoadLayout();
        else if (loadOnStart)
            Invoke(nameof(LoadLayout), loadDelaySeconds);
    }

    /// <summary>Show blackout before first paint when a layout file exists. Uses only <see cref="Scene.IsValid"/> —
    /// during load, <see cref="Scene.isLoaded"/> is often still false in <see cref="Awake"/>, which prevented the curtain.</summary>
    void TryShowLoadCurtainIfNeeded()
    {
        if (!loadOnStart || !hideSceneUntilLayoutApplied)
            return;
        Scene scene = gameObject.scene;
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.name))
            return;
        if (!File.Exists(GetFilePath(scene.name)))
            return;
        EnsureLoadCurtainActive();
    }

    void OnDestroy()
    {
        if (_loadCurtainRoot != null)
            Destroy(_loadCurtainRoot);
    }

    void OnApplicationQuit()
    {
        if (saveOnApplicationQuit)
            PersistLayoutAndSettings();
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    static void RegisterEditorPlayModeSave()
    {
        EditorApplication.playModeStateChanged -= OnEditorPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnEditorPlayModeStateChanged;
    }

    static void OnEditorPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode)
            return;
        foreach (SaveManager sm in UnityEngine.Object.FindObjectsByType<SaveManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (sm == null || !sm.saveOnApplicationQuit)
                continue;
            sm.PersistLayoutAndSettings();
        }
    }
#endif

    /// <summary>Writes the active scene’s layout to disk.</summary>
    public void SaveLayout()
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        EnsureScenePersistedIdentityOnEnemyRoots(scene);
        EnsurePersistentIdsInScene(scene);

        var list = new List<TransformRecord>();
        foreach (GameObject root in scene.GetRootGameObjects())
            CollectRecursive(root.transform, scene, list);

        var patrols = new List<EnemyPatrolSaveRecord>();
        var brains = new List<EnemyAISaveRecord>();
        foreach (GameObject root in scene.GetRootGameObjects())
            CollectGameplayRecursive(root.transform, scene, patrols, brains);

        SaveSummonedEnemyRecord[] saveSummonedEnemies = BuildSaveSummonedEnemiesSnapshot(scene);

        var snapshot = new SceneLayoutSnapshot
        {
            snapshotVersion = CurrentSnapshotVersion,
            sceneName = scene.name,
            transforms = list.ToArray(),
            enemyPatrols = patrols.ToArray(),
            enemyBrains = brains.ToArray(),
            saveSummonedEnemies = saveSummonedEnemies
        };

        string path = GetFilePath(scene.name);
        try
        {
            File.WriteAllText(path, JsonUtility.ToJson(snapshot, true), Encoding.UTF8);
            if (logSaveLoad)
                Debug.Log($"[SaveManager] Saved {list.Count} transforms, {patrols.Count} patrols, {brains.Count} brains, {saveSummonedEnemies.Length} summoned enemies → {path}", this);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveManager] Save failed: {e.Message}", this);
        }
    }

    /// <summary>Reads the layout file for this scene and applies world pose to matching hierarchy paths.</summary>
    public void LoadLayout()
    {
        try
        {
            Scene scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            string path = GetFilePath(scene.name);
            if (!File.Exists(path))
            {
                if (logSaveLoad)
                    Debug.Log($"[SaveManager] No file for scene '{scene.name}' at {path}", this);
                return;
            }

            SceneLayoutSnapshot snapshot;
            try
            {
                string jsonText = File.ReadAllText(path, Encoding.UTF8);
                if (jsonText.IndexOf("\"saveSummonedEnemies\"", StringComparison.Ordinal) < 0
                    && jsonText.IndexOf("\"watcherSummoned\"", StringComparison.Ordinal) >= 0)
                    jsonText = jsonText.Replace("\"watcherSummoned\"", "\"saveSummonedEnemies\"");
                snapshot = JsonUtility.FromJson<SceneLayoutSnapshot>(jsonText);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Load parse failed: {e.Message}", this);
                return;
            }

            if (snapshot == null || snapshot.transforms == null)
                return;

            SpawnMissingPersistedRoots(scene, snapshot.transforms);
            HashSet<string> idsAppliedInSummonedRestore = RestoreSaveSummonedEnemies(scene, snapshot.saveSummonedEnemies);

            int applied = 0;
            foreach (TransformRecord rec in snapshot.transforms)
            {
                if (string.IsNullOrEmpty(rec.hierarchyPath) && string.IsNullOrEmpty(rec.persistentId))
                    continue;
                Transform t = FindTransformForRecord(scene, rec);
                if (t == null)
                    continue;
                t.position = rec.worldPosition;
                t.rotation = rec.worldRotation;
                t.localScale = rec.localScale;

                if (t.TryGetComponent<SpriteRenderer>(out var sr))
                    ApplySpriteRendererState(rec, sr);

                if (t.TryGetComponent<Rigidbody2D>(out var rb) && rb.bodyType != RigidbodyType2D.Static)
                {
                    rb.position = t.position;
                    rb.rotation = t.eulerAngles.z;
                    bool skipSavedVelocity = !string.IsNullOrEmpty(rec.persistentId)
                        && idsAppliedInSummonedRestore != null
                        && idsAppliedInSummonedRestore.Contains(rec.persistentId);
                    if (rec.hasRigidbody2DState && !skipSavedVelocity)
                    {
                        rb.linearVelocity = rec.rb2dLinearVelocity;
                        rb.angularVelocity = rec.rb2dAngularVelocity;
                    }
                }

                applied++;
            }

            if (snapshot.enemyPatrols != null)
            {
                foreach (EnemyPatrolSaveRecord er in snapshot.enemyPatrols)
                {
                    if (string.IsNullOrEmpty(er.hierarchyPath) && string.IsNullOrEmpty(er.persistentId))
                        continue;
                    if (!string.IsNullOrEmpty(er.persistentId) && idsAppliedInSummonedRestore != null && idsAppliedInSummonedRestore.Contains(er.persistentId))
                        continue;
                    Transform t = FindTransformForEnemyRecord(scene, er.hierarchyPath, er.persistentId);
                    if (t == null || !t.TryGetComponent<EnemyPatrol>(out var ep))
                        continue;
                    SceneEnemySave.ApplyPatrol(ep, er.data);
                }
            }

            if (snapshot.enemyBrains != null)
            {
                foreach (EnemyAISaveRecord er in snapshot.enemyBrains)
                {
                    if (er.data == null || (string.IsNullOrEmpty(er.hierarchyPath) && string.IsNullOrEmpty(er.persistentId)))
                        continue;
                    if (!string.IsNullOrEmpty(er.persistentId) && idsAppliedInSummonedRestore != null && idsAppliedInSummonedRestore.Contains(er.persistentId))
                        continue;
                    Transform t = FindTransformForEnemyRecord(scene, er.hierarchyPath, er.persistentId);
                    if (t == null || !t.TryGetComponent<EnemyAI>(out var ai))
                        continue;
                    SceneEnemySave.ApplyEnemyAI(ai, er.data);
                }
            }

            if (logSaveLoad)
                Debug.Log($"[SaveManager] Applied {applied} / {snapshot.transforms.Length} transforms (v{snapshot.snapshotVersion}) for '{scene.name}'", this);
        }
        finally
        {
            RevealAfterLayoutLoad();
        }
    }

    void EnsureLoadCurtainActive()
    {
        if (_loadCurtainRoot != null)
        {
            _loadCurtainRoot.SetActive(true);
            if (_loadCurtainGroup != null)
                _loadCurtainGroup.alpha = 1f;
            return;
        }

        _loadCurtainRoot = new GameObject("SaveLoadCurtain");
        // Root-level object so parent scale/hierarchy never affects overlay drawing.
        _loadCurtainRoot.transform.SetParent(null, false);
        SceneManager.MoveGameObjectToScene(_loadCurtainRoot, gameObject.scene);
        var canvas = _loadCurtainRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;
        canvas.overrideSorting = true;
        var scaler = _loadCurtainRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        _loadCurtainRoot.AddComponent<GraphicRaycaster>();
        _loadCurtainGroup = _loadCurtainRoot.AddComponent<CanvasGroup>();
        _loadCurtainGroup.alpha = 1f;
        _loadCurtainGroup.blocksRaycasts = true;
        _loadCurtainGroup.interactable = false;

        var imgGo = new GameObject("Black");
        imgGo.transform.SetParent(_loadCurtainRoot.transform, false);
        var rect = imgGo.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = imgGo.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
    }

    void RevealAfterLayoutLoad()
    {
        if (_loadCurtainRoot == null || !_loadCurtainRoot.activeInHierarchy)
            return;
        if (_curtainFadeRoutine != null)
        {
            StopCoroutine(_curtainFadeRoutine);
            _curtainFadeRoutine = null;
        }
        if (layoutRevealFadeOutSeconds <= 0f)
        {
            _loadCurtainRoot.SetActive(false);
            if (_loadCurtainGroup != null)
                _loadCurtainGroup.alpha = 1f;
            return;
        }
        _curtainFadeRoutine = StartCoroutine(FadeOutLoadCurtain());
    }

    IEnumerator FadeOutLoadCurtain()
    {
        float d = Mathf.Max(0.01f, layoutRevealFadeOutSeconds);
        float t = 0f;
        float start = _loadCurtainGroup != null ? _loadCurtainGroup.alpha : 1f;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            if (_loadCurtainGroup != null)
                _loadCurtainGroup.alpha = Mathf.Lerp(start, 0f, Mathf.Clamp01(t / d));
            yield return null;
        }
        if (_loadCurtainGroup != null)
            _loadCurtainGroup.alpha = 0f;
        _loadCurtainRoot.SetActive(false);
        if (_loadCurtainGroup != null)
            _loadCurtainGroup.alpha = 1f;
        _curtainFadeRoutine = null;
    }

    void CollectRecursive(Transform t, Scene scene, List<TransformRecord> list)
    {
        if (t.gameObject.scene != scene)
            return;
        if (ShouldSkip(t))
            return;

        var rec = new TransformRecord
        {
            hierarchyPath = BuildHierarchyPath(t),
            worldPosition = t.position,
            worldRotation = t.rotation,
            localScale = t.localScale
        };

        if (t.TryGetComponent<ScenePersistedIdentity>(out var persist))
        {
            rec.persistentId = persist.PersistentId;
            rec.resourcesSpawnPath = persist.ResourcesSpawnPath;
#if UNITY_EDITOR
            GameObject outerPrefabRoot = PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject) as GameObject;
            if (outerPrefabRoot != null && PrefabUtility.IsPartOfPrefabAsset(outerPrefabRoot))
            {
                string assetPath = AssetDatabase.GetAssetPath(outerPrefabRoot);
                if (!string.IsNullOrEmpty(assetPath))
                    rec.respawnPrefabGuid = AssetDatabase.AssetPathToGUID(assetPath);
            }
#endif
            if (string.IsNullOrEmpty(rec.respawnPrefabGuid))
                rec.respawnPrefabGuid = persist.CachedRespawnPrefabGuid;
        }

        if (t.TryGetComponent<SpriteRenderer>(out var spriteRenderer))
            CaptureSpriteRendererState(spriteRenderer, rec);

        if (t.TryGetComponent<Rigidbody2D>(out var rb) && rb.bodyType != RigidbodyType2D.Static)
        {
            rec.hasRigidbody2DState = true;
            rec.rb2dLinearVelocity = rb.linearVelocity;
            rec.rb2dAngularVelocity = rb.angularVelocity;
        }

        list.Add(rec);

        for (int i = 0; i < t.childCount; i++)
            CollectRecursive(t.GetChild(i), scene, list);
    }

    static void CollectGameplayRecursive(Transform t, Scene scene, List<EnemyPatrolSaveRecord> patrols, List<EnemyAISaveRecord> brains)
    {
        if (t.gameObject.scene != scene)
            return;

        string path = BuildHierarchyPath(t);
        string pid = t.TryGetComponent<ScenePersistedIdentity>(out var sid) ? sid.PersistentId : "";
        if (t.TryGetComponent<EnemyPatrol>(out var ep))
            patrols.Add(new EnemyPatrolSaveRecord { hierarchyPath = path, persistentId = pid, data = SceneEnemySave.CapturePatrol(ep) });
        if (t.TryGetComponent<EnemyAI>(out var ai))
        {
            EnemyAISaveData brainData = SceneEnemySave.CaptureEnemyAI(ai);
            if (brainData != null)
                brains.Add(new EnemyAISaveRecord { hierarchyPath = path, persistentId = pid, data = brainData });
        }

        for (int i = 0; i < t.childCount; i++)
            CollectGameplayRecursive(t.GetChild(i), scene, patrols, brains);
    }

    static void CaptureSpriteRendererState(SpriteRenderer sr, TransformRecord rec)
    {
        rec.hasSpriteFlip = true;
        rec.spriteFlipX = sr.flipX;
        rec.spriteFlipY = sr.flipY;

        rec.hasSpriteExtraVisual = true;
        rec.savedSpriteColor = sr.color;
        rec.spriteSortingOrder = sr.sortingOrder;
        rec.spriteSortingLayerName = sr.sortingLayerName;

        if (sr.sprite == null)
        {
            rec.hasSpriteReference = false;
            rec.spriteName = string.Empty;
            rec.spriteTextureGuid = string.Empty;
            rec.spriteResourcesPath = string.Empty;
            return;
        }

        rec.hasSpriteReference = true;
        rec.spriteName = sr.sprite.name;
#if UNITY_EDITOR
        Texture2D tex = sr.sprite.texture;
        if (tex != null)
        {
            string texPath = AssetDatabase.GetAssetPath(tex);
            if (!string.IsNullOrEmpty(texPath))
            {
                rec.spriteTextureGuid = AssetDatabase.AssetPathToGUID(texPath);
                string rel = TryGetResourcesRelativePath(texPath);
                rec.spriteResourcesPath = rel ?? string.Empty;
            }
            else
            {
                rec.spriteTextureGuid = string.Empty;
                rec.spriteResourcesPath = string.Empty;
            }
        }
        else
        {
            rec.spriteTextureGuid = string.Empty;
            rec.spriteResourcesPath = string.Empty;
        }
#else
        rec.spriteTextureGuid = string.Empty;
        rec.spriteResourcesPath = string.Empty;
#endif
    }

    static void ApplySpriteRendererState(TransformRecord rec, SpriteRenderer sr)
    {
        if (rec.hasSpriteFlip)
        {
            sr.flipX = rec.spriteFlipX;
            sr.flipY = rec.spriteFlipY;
        }

        if (rec.hasSpriteExtraVisual)
        {
            sr.color = rec.savedSpriteColor;
            sr.sortingOrder = rec.spriteSortingOrder;
            if (!string.IsNullOrEmpty(rec.spriteSortingLayerName))
                sr.sortingLayerName = rec.spriteSortingLayerName;
        }

        if (rec.hasSpriteReference)
        {
            Sprite resolved = TryResolveSprite(rec);
            if (resolved != null)
                sr.sprite = resolved;
        }
    }

    static Sprite TryResolveSprite(TransformRecord rec)
    {
        if (!rec.hasSpriteReference || string.IsNullOrEmpty(rec.spriteName))
            return null;
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(rec.spriteTextureGuid))
        {
            string path = AssetDatabase.GUIDToAssetPath(rec.spriteTextureGuid);
            if (!string.IsNullOrEmpty(path))
            {
                foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (o is Sprite sp && sp.name == rec.spriteName)
                        return sp;
                }
            }
        }
#endif
        if (!string.IsNullOrEmpty(rec.spriteResourcesPath))
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(rec.spriteResourcesPath);
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && sprites[i].name == rec.spriteName)
                    return sprites[i];
            }
        }

        return null;
    }

    static string TryGetResourcesRelativePath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;
        const string key = "/Resources/";
        int idx = assetPath.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;
        string rel = assetPath.Substring(idx + key.Length);
        rel = Path.ChangeExtension(rel, null);
        return rel.Replace('\\', '/');
    }

    bool ShouldSkip(Transform t)
    {
        if (skipUI && t.GetComponentInParent<Canvas>(true) != null)
            return true;
        if (skipCameras && t.GetComponent<Camera>() != null)
            return true;
        if (skipLights && t.GetComponent<Light>() != null)
            return true;
        if (t.GetComponent<UnityEngine.EventSystems.EventSystem>() != null)
            return true;
        return false;
    }

    /// <summary>Prefab variants accidentally removed <see cref="ScenePersistedIdentity"/>; ensure every enemy brain can be tracked and respawned.</summary>
    static void EnsureScenePersistedIdentityOnEnemyRoots(Scene scene)
    {
        if (!scene.IsValid())
            return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (EnemyAI ai in root.GetComponentsInChildren<EnemyAI>(true))
            {
                if (ai == null || ai.gameObject.scene != scene)
                    continue;
                if (ai.GetComponent<ScenePersistedIdentity>() == null)
                    ai.gameObject.AddComponent<ScenePersistedIdentity>();
            }
        }
    }

    static void EnsurePersistentIdsInScene(Scene scene)
    {
        if (!scene.IsValid())
            return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (ScenePersistedIdentity c in root.GetComponentsInChildren<ScenePersistedIdentity>(true))
                c.EnsureGeneratedId();
        }
    }

    static ScenePersistedIdentity FindIdentityByPersistentId(Scene scene, string persistentId)
    {
        if (string.IsNullOrEmpty(persistentId))
            return null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (ScenePersistedIdentity c in root.GetComponentsInChildren<ScenePersistedIdentity>(true))
            {
                if (c != null && c.PersistentId == persistentId)
                    return c;
            }
        }
        return null;
    }

    static Transform FindTransformForRecord(Scene scene, TransformRecord rec)
    {
        if (!string.IsNullOrEmpty(rec.persistentId))
        {
            ScenePersistedIdentity id = FindIdentityByPersistentId(scene, rec.persistentId);
            if (id != null)
                return id.transform;
        }
        if (!string.IsNullOrEmpty(rec.hierarchyPath))
            return FindByHierarchyPath(scene, rec.hierarchyPath);
        return null;
    }

    static Transform FindTransformForEnemyRecord(Scene scene, string hierarchyPath, string persistentId)
    {
        if (!string.IsNullOrEmpty(persistentId))
        {
            ScenePersistedIdentity id = FindIdentityByPersistentId(scene, persistentId);
            if (id != null)
                return id.transform;
        }
        if (!string.IsNullOrEmpty(hierarchyPath))
            return FindByHierarchyPath(scene, hierarchyPath);
        return null;
    }

    static Transform FindSpawnParentTransform(Scene scene, string fullHierarchyPath)
    {
        if (string.IsNullOrEmpty(fullHierarchyPath))
            return null;
        int i = fullHierarchyPath.LastIndexOf('/');
        if (i <= 0)
            return null;
        return FindByHierarchyPath(scene, fullHierarchyPath.Substring(0, i));
    }

    void SpawnMissingPersistedRoots(Scene scene, TransformRecord[] records)
    {
        if (records == null)
            return;
        foreach (TransformRecord rec in records)
        {
            if (string.IsNullOrEmpty(rec.persistentId))
                continue;
            if (FindIdentityByPersistentId(scene, rec.persistentId) != null)
                continue;
            GameObject prefab = ResolveSpawnPrefab(rec);
            if (prefab == null)
            {
                if (logSaveLoad)
                    Debug.LogWarning($"[SaveManager] Missing instance id={rec.persistentId} and could not resolve prefab (guid/path). Assign respawn template or Resources path on ScenePersistedIdentity.", this);
                continue;
            }

            Transform parent = FindSpawnParentTransform(scene, rec.hierarchyPath);
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(instance, scene);
            if (parent != null)
                instance.transform.SetParent(parent, true);
            instance.transform.SetPositionAndRotation(rec.worldPosition, rec.worldRotation);
            instance.transform.localScale = rec.localScale;
            ScenePersistedIdentity newId = instance.GetComponent<ScenePersistedIdentity>();
            if (newId == null)
                newId = instance.AddComponent<ScenePersistedIdentity>();
            newId.SetRestoredPersistedId(rec.persistentId);
            if (instance.TryGetComponent<SpriteRenderer>(out SpriteRenderer spawnedSr))
                ApplySpriteRendererState(rec, spawnedSr);
        }
    }

    static SaveSummonedEnemyRecord[] BuildSaveSummonedEnemiesSnapshot(Scene scene)
    {
        if (!scene.IsValid())
            return Array.Empty<SaveSummonedEnemyRecord>();

        var list = new List<SaveSummonedEnemyRecord>();
        foreach (SaveSummonedEnemies link in UnityEngine.Object.FindObjectsByType<SaveSummonedEnemies>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (link == null || link.gameObject.scene != scene)
                continue;
            Transform t = link.transform;
            if (!t.TryGetComponent<EnemyAI>(out EnemyAI ai))
                continue;

            ScenePersistedIdentity id = t.GetComponent<ScenePersistedIdentity>();
            id?.EnsureGeneratedId();

            var rec = new TransformRecord
            {
                hierarchyPath = BuildHierarchyPath(t),
                worldPosition = t.position,
                worldRotation = t.rotation,
                localScale = t.localScale,
                persistentId = id != null ? id.PersistentId : "",
                respawnPrefabGuid = !string.IsNullOrEmpty(link.SavedPrefabGuid) ? link.SavedPrefabGuid : (id != null ? id.CachedRespawnPrefabGuid : ""),
                resourcesSpawnPath = !string.IsNullOrEmpty(link.SavedResourcesPath) ? link.SavedResourcesPath : (id != null ? id.ResourcesSpawnPath : null)
            };

            if (t.TryGetComponent<SpriteRenderer>(out SpriteRenderer sr))
                CaptureSpriteRendererState(sr, rec);

            if (t.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb) && rb.bodyType != RigidbodyType2D.Static)
            {
                rec.hasRigidbody2DState = true;
                rec.rb2dLinearVelocity = rb.linearVelocity;
                rec.rb2dAngularVelocity = rb.angularVelocity;
            }

            EnemyPatrol ep = t.GetComponent<EnemyPatrol>();
            EnemyAISaveData brainData = SceneEnemySave.CaptureEnemyAI(ai);
            list.Add(new SaveSummonedEnemyRecord
            {
                transform = rec,
                hasPatrol = ep != null,
                patrol = ep != null ? SceneEnemySave.CapturePatrol(ep) : null,
                hasBrain = brainData != null,
                brain = brainData
            });
        }

        return list.ToArray();
    }

    /// <returns>Persistent ids for enemies instantiated here — skip duplicate patrol/brain apply in the global loops.</returns>
    HashSet<string> RestoreSaveSummonedEnemies(Scene scene, SaveSummonedEnemyRecord[] records)
    {
        var createdIds = new HashSet<string>();
        if (records == null || records.Length == 0)
            return createdIds;

        foreach (SaveSummonedEnemyRecord w in records)
        {
            TransformRecord rec = w.transform;
            if (rec == null || string.IsNullOrEmpty(rec.persistentId))
                continue;
            if (FindIdentityByPersistentId(scene, rec.persistentId) != null)
                continue;

            GameObject prefab = ResolveSpawnPrefab(rec);
            if (prefab == null)
            {
                if (logSaveLoad)
                    Debug.LogWarning($"[SaveManager] saveSummonedEnemies id={rec.persistentId}: could not resolve prefab (guid={rec.respawnPrefabGuid}, resources={rec.resourcesSpawnPath})", this);
                continue;
            }

            Transform parent = FindSpawnParentTransform(scene, rec.hierarchyPath);
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(instance, scene);
            if (parent != null)
                instance.transform.SetParent(parent, true);
            instance.transform.SetPositionAndRotation(rec.worldPosition, rec.worldRotation);
            instance.transform.localScale = rec.localScale;

            ScenePersistedIdentity newId = instance.GetComponent<ScenePersistedIdentity>();
            if (newId == null)
                newId = instance.AddComponent<ScenePersistedIdentity>();
            newId.SetRestoredPersistedId(rec.persistentId);

            if (instance.TryGetComponent<SpriteRenderer>(out SpriteRenderer sr))
                ApplySpriteRendererState(rec, sr);

            if (instance.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb) && rb.bodyType != RigidbodyType2D.Static)
            {
                rb.position = instance.transform.position;
                rb.rotation = instance.transform.eulerAngles.z;
                // Kinematic MovePosition + saved velocity can fight separation after overlap; clear and let AI/movement drive.
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            if (w.hasPatrol && instance.TryGetComponent<EnemyPatrol>(out EnemyPatrol ep) && w.patrol != null)
                SceneEnemySave.ApplyPatrol(ep, w.patrol);

            if (w.hasBrain && w.brain != null && instance.TryGetComponent<EnemyAI>(out EnemyAI ai))
            {
                SceneEnemySave.ApplyEnemyAI(ai, w.brain);
                instance.GetComponent<EnemyArchetype>()?.WakeFromWatcherSummon();
                if (instance.TryGetComponent<EnemyMovement>(out EnemyMovement mv) && w.brain != null)
                {
                    Vector2 staleSeed = w.brain.lastKnownPos.sqrMagnitude > 1e-6f
                        ? w.brain.lastKnownPos
                        : (Vector2)instance.transform.position;
                    mv.ResetSprinterStaleChase(staleSeed);
                }
            }

            SaveSummonedEnemies.Tag(instance, prefab);
            createdIds.Add(rec.persistentId);
        }

        return createdIds;
    }

    static GameObject ResolveSpawnPrefab(TransformRecord rec)
    {
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(rec.respawnPrefabGuid))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(rec.respawnPrefabGuid);
            if (!string.IsNullOrEmpty(assetPath))
            {
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (go != null)
                    return go;
            }
        }
#endif
        if (!string.IsNullOrEmpty(rec.resourcesSpawnPath))
            return Resources.Load<GameObject>(rec.resourcesSpawnPath);
        return null;
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

    static Transform FindByHierarchyPath(Scene scene, string fullPath)
    {
        string[] parts = fullPath.Split('/');
        if (parts.Length == 0)
            return null;

        Transform current = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != parts[0])
                continue;
            current = root.transform;
            break;
        }

        if (current == null)
            return null;

        for (int i = 1; i < parts.Length; i++)
        {
            Transform next = null;
            for (int c = 0; c < current.childCount; c++)
            {
                Transform child = current.GetChild(c);
                if (child.name == parts[i])
                {
                    next = child;
                    break;
                }
            }
            if (next == null)
                return null;
            current = next;
        }

        return current;
    }

    static string GetFilePath(string sceneName)
    {
        string safe = string.IsNullOrEmpty(sceneName) ? "UnknownScene" : sceneName;
        foreach (char c in Path.GetInvalidFileNameChars())
            safe = safe.Replace(c, '_');
        return Path.Combine(Application.persistentDataPath, FilePrefix + safe + ".json");
    }

    /// <summary>Full path to the JSON file for a scene (for debugging or file browsers).</summary>
    public static string GetSavedLayoutPathForScene(string sceneName) => GetFilePath(sceneName);
}

/// <summary>Patrol fields persisted in scene JSON (written by <see cref="SceneEnemySave"/>).</summary>
[Serializable]
public class EnemyPatrolSaveData
{
    public float health;
    public int currWaypointIndex;
}

/// <summary>EnemyAI private fields persisted in scene JSON (same names as legacy saves).</summary>
[Serializable]
public class EnemyAISaveData
{
    public int state;
    public float suspiciousTimer;
    public float searchTimer;
    public Vector2 lastKnownPos;
    public float attackCooldown;
    public float chaseLoseSightTimer;
    public bool searchFollowedChase;
    public bool hasEnteredChaseSequence;
    public bool watcherFirstChaseHasEnded;
    public bool watcherBulwarksSpawnedThisChase;
    public float watcherCurrentChaseElapsed;
    public bool sprinterFlow;
    public Vector2 sprInvestigateCenter;
    public Vector2 sprDeployTarget;
    public Vector2 sprLeaveTarget;
    public float sprSweepRadius;
    public float sprSweepTheta;
    public float sprOffscreenMargin;
    public float sprinterChaseBlindTimer;
    public bool patrolBehaviourEnabled;
}

/// <summary>Reads/writes <see cref="EnemyAI"/> / <see cref="EnemyPatrol"/> private state for <see cref="SaveManager"/>.</summary>
public static class SceneEnemySave
{
    const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static readonly Type TAI = typeof(EnemyAI);
    static readonly Type TPatrol = typeof(EnemyPatrol);

    static readonly FieldInfo AiState = TAI.GetField("_state", BF);
    static readonly FieldInfo AiSuspicious = TAI.GetField("_suspiciousTimer", BF);
    static readonly FieldInfo AiSearchT = TAI.GetField("_searchTimer", BF);
    static readonly FieldInfo AiLastKnown = TAI.GetField("_lastKnownPos", BF);
    static readonly FieldInfo AiAtkCd = TAI.GetField("_attackCooldown", BF);
    static readonly FieldInfo AiChaseLose = TAI.GetField("_chaseLoseSightTimer", BF);
    static readonly FieldInfo AiSearchFollow = TAI.GetField("_searchFollowedChase", BF);
    static readonly FieldInfo AiHasChaseSeq = TAI.GetField("_hasEnteredChaseSequence", BF);
    static readonly FieldInfo AiWatchFirst = TAI.GetField("_watcherFirstChaseHasEnded", BF);
    static readonly FieldInfo AiWatchBulw = TAI.GetField("_watcherBulwarksSpawnedThisChase", BF);
    static readonly FieldInfo AiWatchElapsed = TAI.GetField("_watcherCurrentChaseElapsed", BF);
    static readonly FieldInfo AiSprFlow = TAI.GetField("_sprinterFlow", BF);
    static readonly FieldInfo AiSprCenter = TAI.GetField("_sprInvestigateCenter", BF);
    static readonly FieldInfo AiSprDep = TAI.GetField("_sprDeployTarget", BF);
    static readonly FieldInfo AiSprLeave = TAI.GetField("_sprLeaveTarget", BF);
    static readonly FieldInfo AiSprRad = TAI.GetField("_sprSweepRadius", BF);
    static readonly FieldInfo AiSprTheta = TAI.GetField("_sprSweepTheta", BF);
    static readonly FieldInfo AiSprMargin = TAI.GetField("_sprOffscreenMargin", BF);
    static readonly FieldInfo AiSprBlind = TAI.GetField("_sprinterChaseBlindTimer", BF);
    static readonly FieldInfo AiPatrolBeh = TAI.GetField("patrolBehaviour", BF);

    static readonly MethodInfo AiStopFlash = TAI.GetMethod("StopFlash", BF);

    static readonly FieldInfo PtHealth = TPatrol.GetField("health", BF);
    static readonly FieldInfo PtIdx = TPatrol.GetField("currWaypointIndex", BF);
    static readonly FieldInfo PtPath = TPatrol.GetField("patrolPath", BF);

    public static EnemyPatrolSaveData CapturePatrol(EnemyPatrol ep)
    {
        var d = new EnemyPatrolSaveData();
        if (ep == null || PtHealth == null || PtIdx == null)
            return d;
        d.health = (float)PtHealth.GetValue(ep);
        d.currWaypointIndex = (int)PtIdx.GetValue(ep);
        return d;
    }

    public static void ApplyPatrol(EnemyPatrol ep, EnemyPatrolSaveData d)
    {
        if (ep == null || d == null || PtHealth == null || PtIdx == null)
            return;
        PtHealth.SetValue(ep, d.health);
        int idx = d.currWaypointIndex;
        if (PtPath != null && PtPath.GetValue(ep) is EnemyPathing path
            && path.GetTransformList() != null && path.GetTransformList().Count > 0)
            idx = Mathf.Clamp(idx, 0, path.GetTransformList().Count - 1);
        PtIdx.SetValue(ep, idx);
    }

    public static EnemyAISaveData CaptureEnemyAI(EnemyAI ai)
    {
        if (ai == null)
            return null;
        if (AiState == null)
        {
            Debug.LogWarning("[SaveManager] EnemyAI save reflection not bound (field rename?).");
            return null;
        }

        bool patrolOn = false;
        if (AiPatrolBeh != null && AiPatrolBeh.GetValue(ai) is MonoBehaviour pb)
            patrolOn = pb.enabled;

        return new EnemyAISaveData
        {
            state = Convert.ToInt32(AiState.GetValue(ai)),
            suspiciousTimer = (float)AiSuspicious.GetValue(ai),
            searchTimer = (float)AiSearchT.GetValue(ai),
            lastKnownPos = (Vector2)AiLastKnown.GetValue(ai),
            attackCooldown = (float)AiAtkCd.GetValue(ai),
            chaseLoseSightTimer = (float)AiChaseLose.GetValue(ai),
            searchFollowedChase = (bool)AiSearchFollow.GetValue(ai),
            hasEnteredChaseSequence = (bool)AiHasChaseSeq.GetValue(ai),
            watcherFirstChaseHasEnded = (bool)AiWatchFirst.GetValue(ai),
            watcherBulwarksSpawnedThisChase = (bool)AiWatchBulw.GetValue(ai),
            watcherCurrentChaseElapsed = (float)AiWatchElapsed.GetValue(ai),
            sprinterFlow = (bool)AiSprFlow.GetValue(ai),
            sprInvestigateCenter = (Vector2)AiSprCenter.GetValue(ai),
            sprDeployTarget = (Vector2)AiSprDep.GetValue(ai),
            sprLeaveTarget = (Vector2)AiSprLeave.GetValue(ai),
            sprSweepRadius = (float)AiSprRad.GetValue(ai),
            sprSweepTheta = (float)AiSprTheta.GetValue(ai),
            sprOffscreenMargin = (float)AiSprMargin.GetValue(ai),
            sprinterChaseBlindTimer = (float)AiSprBlind.GetValue(ai),
            patrolBehaviourEnabled = patrolOn
        };
    }

    public static void ApplyEnemyAI(EnemyAI ai, EnemyAISaveData d)
    {
        if (ai == null || d == null || AiState == null)
            return;
        if (d.state < 0 || d.state > (int)EnemyAI.State.SprinterLeave)
            return;

        AiStopFlash?.Invoke(ai, null);

        AiState.SetValue(ai, Enum.ToObject(typeof(EnemyAI.State), d.state));
        AiSuspicious?.SetValue(ai, d.suspiciousTimer);
        AiSearchT?.SetValue(ai, d.searchTimer);
        AiLastKnown?.SetValue(ai, d.lastKnownPos);
        AiAtkCd?.SetValue(ai, d.attackCooldown);
        AiChaseLose?.SetValue(ai, d.chaseLoseSightTimer);
        AiSearchFollow?.SetValue(ai, d.searchFollowedChase);
        AiHasChaseSeq?.SetValue(ai, d.hasEnteredChaseSequence);
        AiWatchFirst?.SetValue(ai, d.watcherFirstChaseHasEnded);
        AiWatchBulw?.SetValue(ai, d.watcherBulwarksSpawnedThisChase);
        AiWatchElapsed?.SetValue(ai, d.watcherCurrentChaseElapsed);
        AiSprFlow?.SetValue(ai, d.sprinterFlow);
        AiSprCenter?.SetValue(ai, d.sprInvestigateCenter);
        AiSprDep?.SetValue(ai, d.sprDeployTarget);
        AiSprLeave?.SetValue(ai, d.sprLeaveTarget);
        AiSprRad?.SetValue(ai, d.sprSweepRadius);
        AiSprTheta?.SetValue(ai, d.sprSweepTheta);
        AiSprMargin?.SetValue(ai, d.sprOffscreenMargin);
        AiSprBlind?.SetValue(ai, d.sprinterChaseBlindTimer);

        ai.RestoreVisualsAfterSave(d.patrolBehaviourEnabled);
    }
}

[Serializable]
public class SceneLayoutSnapshot
{
    public int snapshotVersion;
    public string sceneName;
    public TransformRecord[] transforms;
    public EnemyPatrolSaveRecord[] enemyPatrols;
    public EnemyAISaveRecord[] enemyBrains;
    /// <summary>Explicit snapshot of every enemy spawned by a watcher (sprinters / bulwarks), with prefab GUID + AI/patrol.</summary>
    public SaveSummonedEnemyRecord[] saveSummonedEnemies;
}

/// <summary>One watcher-spawned enemy, written only for objects with <see cref="SaveSummonedEnemies"/>.</summary>
[Serializable]
public class SaveSummonedEnemyRecord
{
    public TransformRecord transform;
    public bool hasPatrol;
    public EnemyPatrolSaveData patrol;
    public bool hasBrain;
    public EnemyAISaveData brain;
}

[Serializable]
public class TransformRecord
{
    public string hierarchyPath;
    /// <summary>Stable id from <see cref="ScenePersistedIdentity"/>; used when hierarchy names collide or for respawn-on-load.</summary>
    public string persistentId;
    /// <summary>Prefab asset GUID (cached on identity in editor) for re-instantiating missing instances.</summary>
    public string respawnPrefabGuid;
    /// <summary>Optional <see cref="Resources"/> path for player builds, e.g. <c>Stealth/Enemy</c>.</summary>
    public string resourcesSpawnPath;
    public Vector3 worldPosition;
    public Quaternion worldRotation;
    public Vector3 localScale;
    public bool hasSpriteFlip;
    public bool spriteFlipX;
    public bool spriteFlipY;
    public bool hasSpriteReference;
    public string spriteName;
    public string spriteTextureGuid;
    /// <summary>Path passed to <see cref="Resources.LoadAll{T}"/> (inside a Resources folder, no extension). Filled in Editor when the texture lives under Resources.</summary>
    public string spriteResourcesPath;
    public bool hasSpriteExtraVisual;
    public Color savedSpriteColor;
    public int spriteSortingOrder;
    public string spriteSortingLayerName;
    public bool hasRigidbody2DState;
    public Vector2 rb2dLinearVelocity;
    public float rb2dAngularVelocity;
}

[Serializable]
public class EnemyPatrolSaveRecord
{
    public string hierarchyPath;
    public string persistentId;
    public EnemyPatrolSaveData data;
}

[Serializable]
public class EnemyAISaveRecord
{
    public string hierarchyPath;
    public string persistentId;
    public EnemyAISaveData data;
}
