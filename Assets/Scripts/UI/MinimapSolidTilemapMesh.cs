using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds a flat-color mesh from occupied <see cref="Tilemap"/> cells on a dedicated layer so the
/// minimap camera can show walls/floor as one color while the main camera still uses normal tiles.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class MinimapSolidTilemapMesh : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] Color solidColor = new Color(0.35f, 0.38f, 0.42f, 1f);

    [Header("Sources")]
    [Tooltip("If empty, every Tilemap in loaded scenes is used. Otherwise only these.")]
    [SerializeField] Tilemap[] tilemapOverrides;

    [Tooltip("If non-zero, only Tilemaps on these layers are meshed.")]
    [SerializeField] LayerMask sourceTilemapLayers = ~0;

    [Header("Output")]
    [Tooltip("Layer for the generated mesh (should be hidden from main gameplay cameras). Default: MINIMAP if present, else this GameObject's layer.")]
    [SerializeField] string outputLayerName = "MINIMAP";

    [Header("Minimap camera")]
    [Tooltip("When true, the minimap camera always includes the output layer.")]
    [SerializeField] bool minimapCameraIncludesOutputLayer = true;

    [Tooltip("When true, removes from the minimap camera every layer that had a meshed Tilemap (so textured tiles are not drawn twice).")]
    [SerializeField] bool hideMeshedTilemapLayersFromMinimap = true;

    [Header("Other cameras")]
    [Tooltip("Strip the output layer from every other camera so the silhouette is not visible in-world.")]
    [SerializeField] bool hideOutputLayerFromOtherCameras = true;

    MeshFilter _meshFilter;
    MeshRenderer _meshRenderer;
    Mesh _mesh;
    int _outputLayer;
    int _layersThatHadTilesMask;

    void Start()
    {
        var cam = GetComponent<Camera>();
        _outputLayer = LayerMask.NameToLayer(outputLayerName);
        if (_outputLayer < 0)
            _outputLayer = gameObject.layer;

        EnsureMeshObjects();
        RebuildMesh();
        ApplyCameraMasks(cam);
    }

    [ContextMenu("Rebuild minimap tile mesh")]
    public void RebuildMesh()
    {
        if (_mesh == null)
        {
            _mesh = new Mesh { name = "MinimapTilemapSilhouette" };
            if (_meshFilter != null)
                _meshFilter.sharedMesh = _mesh;
        }

        var tilemaps = ResolveTilemaps();
        var verts = new List<Vector3>(1024);
        var tris = new List<int>(2048);
        _layersThatHadTilesMask = 0;

        foreach (var tm in tilemaps)
        {
            if (tm == null) continue;
            if (sourceTilemapLayers.value != 0 && ((1 << tm.gameObject.layer) & sourceTilemapLayers.value) == 0)
                continue;

            tm.CompressBounds();
            var bounds = tm.cellBounds;
            bool any = false;
            foreach (var pos in bounds.allPositionsWithin)
            {
                if (!tm.HasTile(pos))
                    continue;
                any = true;
                AddCellQuad(tm, pos, verts, tris);
            }

            if (any)
                _layersThatHadTilesMask |= 1 << tm.gameObject.layer;
        }

        _mesh.Clear();
        if (verts.Count == 0)
        {
            if (_meshRenderer != null)
                _meshRenderer.enabled = false;
            return;
        }

        _mesh.SetVertices(verts);
        _mesh.SetTriangles(tris, 0);
        _mesh.RecalculateBounds();
        _meshRenderer.enabled = true;

        ApplyMaterialColor();

        if (_meshFilter != null)
            _meshFilter.gameObject.layer = _outputLayer;
    }

    void ApplyCameraMasks(Camera minimapCam)
    {
        int bit = 1 << _outputLayer;

        if (minimapCameraIncludesOutputLayer)
            minimapCam.cullingMask |= bit;

        if (hideMeshedTilemapLayersFromMinimap && _layersThatHadTilesMask != 0)
            minimapCam.cullingMask &= ~_layersThatHadTilesMask;

        if (!hideOutputLayerFromOtherCameras)
            return;

        foreach (var c in Camera.allCameras)
        {
            if (c == null || c == minimapCam) continue;
            c.cullingMask &= ~bit;
        }
    }

    void EnsureMeshObjects()
    {
        if (_meshFilter != null)
            return;

        var holder = new GameObject("MinimapTilemapSilhouetteMesh");
        // World-space vertices from Tilemap.CellToWorld — do not parent under the (moving) minimap camera.
        holder.transform.SetParent(null, false);
        holder.transform.position = Vector3.zero;
        holder.transform.rotation = Quaternion.identity;
        holder.transform.localScale = Vector3.one;
        holder.layer = _outputLayer;

        _meshFilter = holder.AddComponent<MeshFilter>();
        _meshRenderer = holder.AddComponent<MeshRenderer>();
        _mesh = new Mesh { name = "MinimapTilemapSilhouette" };
        _meshFilter.sharedMesh = _mesh;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        if (mat.shader == null || !mat.shader.isSupported)
            mat.shader = Shader.Find("Unlit/Color");
        if (mat.shader == null || !mat.shader.isSupported)
            mat.shader = Shader.Find("Sprites/Default");

        mat.color = solidColor;
        _meshRenderer.sharedMaterial = mat;
        _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;
    }

    void ApplyMaterialColor()
    {
        if (_meshRenderer == null || _meshRenderer.sharedMaterial == null)
            return;
        var m = _meshRenderer.sharedMaterial;
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", solidColor);
        else if (m.HasProperty("_Color"))
            m.color = solidColor;
        else
            m.color = solidColor;
    }

    List<Tilemap> ResolveTilemaps()
    {
        if (tilemapOverrides != null && tilemapOverrides.Length > 0)
            return new List<Tilemap>(tilemapOverrides);

        var found = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        return new List<Tilemap>(found);
    }

    static void AddCellQuad(Tilemap tm, Vector3Int pos, List<Vector3> verts, List<int> tris)
    {
        Vector3 bl = tm.CellToWorld(pos);
        Vector3 br = tm.CellToWorld(pos + new Vector3Int(1, 0, 0));
        Vector3 tl = tm.CellToWorld(pos + new Vector3Int(0, 1, 0));
        Vector3 tr = tm.CellToWorld(pos + new Vector3Int(1, 1, 0));

        int i = verts.Count;
        verts.Add(bl);
        verts.Add(br);
        verts.Add(tl);
        verts.Add(tr);

        tris.Add(i);
        tris.Add(i + 1);
        tris.Add(i + 2);
        tris.Add(i + 1);
        tris.Add(i + 3);
        tris.Add(i + 2);
    }

    void OnValidate()
    {
        if (!Application.isPlaying || _meshRenderer == null || _meshRenderer.sharedMaterial == null)
            return;
        ApplyMaterialColor();
    }
}
