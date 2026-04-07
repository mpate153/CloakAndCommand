using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Walks <see cref="patrolWaypoints"/> in order (0 → 1 → 2 → …) and loops back to the first.
/// Disable when <see cref="EnemyAI"/> takes over (suspicious / search / chase).
/// If the array is empty or all null, falls back to <see cref="EnemyPathing"/> (tilemap A*: end → start, then destroys).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyPatrol : MonoBehaviour
{
    private Rigidbody2D myBody;

    [Header("Patrol waypoints (preferred)")]
    [Tooltip("Order: first element, then second, … then back to first. Use empty GameObjects in the scene.")]
    [SerializeField] private Transform[] patrolWaypoints;

    [Header("Patrol route — tilemap A* (only if waypoints above are empty)")]
    [SerializeField] private EnemyPathing patrolPath;
    [SerializeField] private bool autoFindPathFindingByName = true;
    [SerializeField] private string pathFindingObjectName = "PathFinding";

    [Header("Movement")]
    [SerializeField] private float speed = 1f;
    [Tooltip("How close (XY) the body must get before advancing. Too small + separation/colliders can make the guard orbit forever.")]
    [SerializeField] private float reachDistance = 0.18f;
    [SerializeField] private bool enableSeparation = true;
    [SerializeField] private float separationRadius = 0.7f;
    [SerializeField] private float separationWeight = 1.1f;
    [SerializeField] private LayerMask separationMask = ~0;

    [Header("Health")]
    [SerializeField] private float health = 10f;

    [Header("Debug")]
    [SerializeField] private bool togglePathDist = false;
    [SerializeField] private float functionDelaySeconds = 2f;
    [Header("Scene view — patrol visualization")]
    [SerializeField] private bool showPatrolPathGizmos = true;
    [SerializeField] private Color gizmoPatrolTravelColor = new Color(1f, 0.85f, 0.2f, 0.95f);
    [SerializeField] private Color gizmoStoredPathColor = new Color(0.35f, 0.65f, 1f, 0.35f);
    [SerializeField] private float gizmoWaypointRadius = 0.12f;

    private int currWaypointIndex;
    private float finalDist;
    private int inspectIndex;
    private Collider2D _selfCollider;
    private readonly Collider2D[] _separationHits = new Collider2D[24];

    private bool _useWaypointArray;

    public EnemyPathing PatrolPath => patrolPath;
    public int CurrentWaypointIndex => currWaypointIndex;

    /// <summary>
    /// True when there is a real route to follow (waypoints or generated A* points).
    /// When false, <see cref="EnemyAI"/> should run idle look-around even if this component is enabled.
    /// </summary>
    public bool HasActivePatrolRoute()
    {
        if (!enabled)
            return false;
        if (_useWaypointArray)
            return true;
        return patrolPath != null
            && patrolPath.GetTransformList() != null
            && patrolPath.GetTransformList().Count > 0;
    }

    /// <summary>For save/load clamping: array length or A* list count.</summary>
    public int GetActivePatrolPointCount()
    {
        if (UsesWaypointArray() && patrolWaypoints != null)
            return patrolWaypoints.Length;
        if (patrolPath != null && patrolPath.GetTransformList() != null)
            return patrolPath.GetTransformList().Count;
        return 0;
    }

    private void Awake()
    {
        myBody = GetComponent<Rigidbody2D>();
        _selfCollider = GetComponent<Collider2D>();
        _useWaypointArray = UsesWaypointArray();
        if (!_useWaypointArray)
            ResolvePatrolPath();
        ResetWaypointToPatrolStart();

        if (togglePathDist)
            StartCoroutine(DisplayPathDist());
    }

    private bool UsesWaypointArray()
    {
        if (patrolWaypoints == null || patrolWaypoints.Length == 0)
            return false;
        for (int i = 0; i < patrolWaypoints.Length; i++)
        {
            if (patrolWaypoints[i] != null)
                return true;
        }
        return false;
    }

    private void ResolvePatrolPath()
    {
        if (patrolPath != null)
            return;

        if (autoFindPathFindingByName)
        {
            GameObject targetObject = GameObject.Find(pathFindingObjectName);
            if (targetObject != null)
                patrolPath = targetObject.GetComponent<EnemyPathing>();
        }
    }

    private void ResetWaypointToPatrolStart()
    {
        if (_useWaypointArray)
        {
            currWaypointIndex = FirstValidWaypointIndex(0);
            return;
        }

        if (patrolPath == null || patrolPath.GetTransformList() == null || patrolPath.GetTransformList().Count == 0)
        {
            currWaypointIndex = 0;
            return;
        }

        currWaypointIndex = patrolPath.GetTransformList().Count - 1;
    }

    private int FirstValidWaypointIndex(int startSearch)
    {
        if (patrolWaypoints == null)
            return 0;
        for (int i = startSearch; i < patrolWaypoints.Length; i++)
        {
            if (patrolWaypoints[i] != null)
                return i;
        }
        for (int i = 0; i < startSearch && i < patrolWaypoints.Length; i++)
        {
            if (patrolWaypoints[i] != null)
                return i;
        }
        return 0;
    }

    private int NextWaypointIndexAfter(int current)
    {
        if (patrolWaypoints == null || patrolWaypoints.Length == 0)
            return current;
        for (int i = current + 1; i < patrolWaypoints.Length; i++)
        {
            if (patrolWaypoints[i] != null)
                return i;
        }
        return FirstValidWaypointIndex(0);
    }

    private IEnumerator DisplayPathDist()
    {
        while (true)
        {
            Debug.Log(GetPathDist());
            yield return new WaitForSeconds(functionDelaySeconds);
        }
    }

    private void Update()
    {
        if (health <= 0)
        {
            DestroyedEnemySaveTracker.RegisterKilledEnemyRoot(transform.root);
            Destroy(gameObject);
            return;
        }
    }

    private void FixedUpdate()
    {
        if (health <= 0)
            return;
        Move();
    }

    private void Move()
    {
        if (_useWaypointArray)
            MoveWaypointLoop();
        else
            MovePathing();
    }

    private void MoveWaypointLoop()
    {
        if (patrolWaypoints == null || currWaypointIndex < 0 || currWaypointIndex >= patrolWaypoints.Length)
            return;

        Transform wp = patrolWaypoints[currWaypointIndex];
        if (wp == null)
        {
            currWaypointIndex = NextWaypointIndexAfter(currWaypointIndex);
            return;
        }

        Vector2 pos = myBody.position;
        Vector2 target = wp.position;
        Vector2 toTarget = target - pos;
        float reach = Mathf.Max(0.02f, reachDistance);
        float reachSq = reach * reach;

        Vector2 desiredDir = toTarget.sqrMagnitude > 1e-8f ? toTarget.normalized : Vector2.zero;
        // Near the waypoint, separation can push the body so it never enters the tiny reach radius.
        float relaxSeparationSq = (reach * 4f) * (reach * 4f);
        Vector2 direction = toTarget.sqrMagnitude <= relaxSeparationSq
            ? desiredDir
            : ApplySeparation(desiredDir);

        if (direction.sqrMagnitude < 1e-8f)
            direction = desiredDir;

        Vector2 nextPos = pos + direction * (speed * Time.fixedDeltaTime);
        myBody.MovePosition(nextPos);

        pos = myBody.position;
        toTarget = target - pos;
        if (toTarget.sqrMagnitude <= reachSq)
            currWaypointIndex = NextWaypointIndexAfter(currWaypointIndex);
    }

    private void MovePathing()
    {
        if (patrolPath == null || patrolPath.GetTransformList() == null || patrolPath.GetTransformList().Count == 0)
            return;

        if (currWaypointIndex < 0 || currWaypointIndex >= patrolPath.GetTransformList().Count)
            return;

        Transform wp = patrolPath.GetTransformList()[currWaypointIndex];
        if (wp == null)
            return;

        Vector2 pos = myBody.position;
        Vector2 target = wp.position;
        Vector2 toTarget = target - pos;
        float reach = Mathf.Max(0.02f, reachDistance);
        float reachSq = reach * reach;

        Vector2 desiredDir = toTarget.sqrMagnitude > 1e-8f ? toTarget.normalized : Vector2.zero;
        float relaxSeparationSq = (reach * 4f) * (reach * 4f);
        Vector2 direction = toTarget.sqrMagnitude <= relaxSeparationSq
            ? desiredDir
            : ApplySeparation(desiredDir);
        if (direction.sqrMagnitude < 1e-8f)
            direction = desiredDir;

        myBody.MovePosition(pos + direction * (speed * Time.fixedDeltaTime));

        pos = myBody.position;
        toTarget = target - pos;
        if (toTarget.sqrMagnitude <= reachSq)
        {
            currWaypointIndex--;
            if (currWaypointIndex < 0)
            {
                enabled = false;
                DestroyedEnemySaveTracker.RegisterKilledEnemyRoot(transform.root);
                Destroy(gameObject);
            }
        }
    }

    public void TakeDamage(float damage)
    {
        health -= damage;
    }

    public float GetPathDist()
    {
        finalDist = 0;
        inspectIndex = currWaypointIndex;

        if (_useWaypointArray && patrolWaypoints != null && inspectIndex >= 0 && inspectIndex < patrolWaypoints.Length
            && patrolWaypoints[inspectIndex] != null)
        {
            finalDist = Vector3.Distance(transform.position, patrolWaypoints[inspectIndex].position);
            int cur = inspectIndex;
            do
            {
                int next = NextWaypointIndexAfter(cur);
                Transform a = patrolWaypoints[cur];
                Transform b = patrolWaypoints[next];
                if (a != null && b != null)
                    finalDist += Vector3.Distance(a.position, b.position);
                cur = next;
            } while (cur != inspectIndex);

            return finalDist;
        }

        if (patrolPath == null || patrolPath.GetTransformList() == null || inspectIndex < 0)
            return 0f;

        if (inspectIndex > 0)
        {
            finalDist += Vector3.Distance(transform.position, patrolPath.GetTransformList()[inspectIndex].position);
            for (int i = inspectIndex; i > 0; i--)
            {
                Vector3 a = patrolPath.GetTransformList()[i].position;
                Vector3 b = patrolPath.GetTransformList()[i - 1].position;
                finalDist += Vector3.Distance(a, b);
            }
        }

        return finalDist;
    }

    int OverlapSeparation(Vector2 position, float radius)
    {
        var filter = new ContactFilter2D();
        filter.SetLayerMask(separationMask);
        filter.useTriggers = Physics2D.queriesHitTriggers;
        return Physics2D.OverlapCircle(position, radius, filter, _separationHits);
    }

    private Vector2 ApplySeparation(Vector2 desiredDir)
    {
        if (!enableSeparation || desiredDir.sqrMagnitude < 1e-6f)
            return desiredDir;

        float radius = Mathf.Max(0.05f, separationRadius);
        int count = OverlapSeparation(myBody.position, radius);
        if (count <= 0)
            return desiredDir;

        Vector2 away = Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            var col = _separationHits[i];
            if (col == null) continue;
            if (_selfCollider != null && col == _selfCollider) continue;

            var rb = col.attachedRigidbody;
            if (rb == null || rb == myBody) continue;

            Vector2 nearest = col.ClosestPoint(myBody.position);
            Vector2 toOther = nearest - myBody.position;
            float d = toOther.magnitude;
            if (d < 0.0001f) continue;

            float w = 1f - Mathf.Clamp01(d / radius);
            away -= (toOther / d) * w;
        }

        if (away.sqrMagnitude < 1e-6f)
            return desiredDir;

        Vector2 combined = desiredDir + away * Mathf.Max(0f, separationWeight);
        return combined.sqrMagnitude > 1e-6f ? combined.normalized : desiredDir;
    }

    private void OnDrawGizmos()
    {
        if (!showPatrolPathGizmos)
            return;

        if (patrolWaypoints != null && HasAnyWaypoint(patrolWaypoints))
        {
            DrawWaypointArrayGizmos(patrolWaypoints);
            return;
        }

        EnemyPathing path = patrolPath;
        if (path == null && autoFindPathFindingByName)
        {
            GameObject go = GameObject.Find(pathFindingObjectName);
            if (go != null)
                path = go.GetComponent<EnemyPathing>();
        }

        if (path == null || path.GetTransformList() == null || path.GetTransformList().Count == 0)
            return;

        IReadOnlyList<Transform> pts = path.GetTransformList();

        Gizmos.color = gizmoStoredPathColor;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            if (pts[i] == null || pts[i + 1] == null)
                continue;
            Gizmos.DrawLine(pts[i].position, pts[i + 1].position);
        }

        Gizmos.color = gizmoPatrolTravelColor;
        for (int i = pts.Count - 1; i > 0; i--)
        {
            if (pts[i] == null || pts[i - 1] == null)
                continue;
            Gizmos.DrawLine(pts[i].position, pts[i - 1].position);
        }

        for (int i = 0; i < pts.Count; i++)
        {
            if (pts[i] == null)
                continue;
            Gizmos.color = i == 0
                ? new Color(0.3f, 1f, 0.5f, 0.9f)
                : (i == pts.Count - 1 ? new Color(1f, 0.4f, 0.3f, 0.9f) : gizmoPatrolTravelColor);
            Gizmos.DrawWireSphere(pts[i].position, gizmoWaypointRadius);
        }

        if (Application.isPlaying && enabled)
        {
            int idx = Mathf.Clamp(currWaypointIndex, 0, pts.Count - 1);
            if (pts[idx] != null)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(pts[idx].position, gizmoWaypointRadius * 1.35f);
            }
        }
    }

    private static bool HasAnyWaypoint(Transform[] arr)
    {
        if (arr == null)
            return false;
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] != null)
                return true;
        }
        return false;
    }

    private void DrawWaypointArrayGizmos(Transform[] pts)
    {
        int first = -1, last = -1;
        for (int i = 0; i < pts.Length; i++)
        {
            if (pts[i] == null)
                continue;
            if (first < 0)
                first = i;
            last = i;
        }

        Gizmos.color = gizmoStoredPathColor;
        Transform prev = null;
        for (int i = 0; i < pts.Length; i++)
        {
            if (pts[i] == null)
                continue;
            if (prev != null)
                Gizmos.DrawLine(prev.position, pts[i].position);
            prev = pts[i];
        }
        if (first >= 0 && last >= 0 && first != last && pts[first] != null && pts[last] != null)
            Gizmos.DrawLine(pts[last].position, pts[first].position);

        for (int i = 0; i < pts.Length; i++)
        {
            if (pts[i] == null)
                continue;
            Gizmos.color = i == first
                ? new Color(0.3f, 1f, 0.5f, 0.9f)
                : (i == last ? new Color(1f, 0.4f, 0.3f, 0.9f) : gizmoPatrolTravelColor);
            Gizmos.DrawWireSphere(pts[i].position, gizmoWaypointRadius);
        }

        if (Application.isPlaying && enabled && currWaypointIndex >= 0 && currWaypointIndex < pts.Length && pts[currWaypointIndex] != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(pts[currWaypointIndex].position, gizmoWaypointRadius * 1.35f);
        }
    }
}
