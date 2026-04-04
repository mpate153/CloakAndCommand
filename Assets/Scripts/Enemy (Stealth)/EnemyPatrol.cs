using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Follows waypoints from an <see cref="EnemyPathing"/> route. List order is A* output (reversed);
/// this agent walks from the last index toward index 0. Disable this component (or the behaviour)
/// when something like <see cref="EnemyVision"/> should take over.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyPatrol : MonoBehaviour
{
    private Rigidbody2D myBody;

    [Header("Patrol route")]
    [Tooltip("Assign the GameObject with EnemyPathing (optional: leave empty to use a child named PathFinding).")]
    [SerializeField] private EnemyPathing patrolPath;
    [SerializeField] private bool autoFindPathFindingByName = true;
    [SerializeField] private string pathFindingObjectName = "PathFinding";

    [Header("Movement")]
    [SerializeField] private float speed = 1f;
    [SerializeField] private float reachDistance = 0.05f;
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

    public EnemyPathing PatrolPath => patrolPath;
    public int CurrentWaypointIndex => currWaypointIndex;

    private void Awake()
    {
        myBody = GetComponent<Rigidbody2D>();
        _selfCollider = GetComponent<Collider2D>();
        ResolvePatrolPath();
        ResetWaypointToPatrolStart();

        if (togglePathDist)
            StartCoroutine(DisplayPathDist());
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
        if (patrolPath == null || patrolPath.GetTransformList() == null || patrolPath.GetTransformList().Count == 0)
        {
            currWaypointIndex = 0;
            return;
        }

        currWaypointIndex = patrolPath.GetTransformList().Count - 1;
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
            Destroy(gameObject);
            return;
        }

        Move();
    }

    private void Move()
    {
        if (patrolPath == null || patrolPath.GetTransformList() == null || patrolPath.GetTransformList().Count == 0)
            return;

        if (currWaypointIndex < 0 || currWaypointIndex >= patrolPath.GetTransformList().Count)
            return;

        Transform wp = patrolPath.GetTransformList()[currWaypointIndex];
        if (wp == null)
            return;

        Vector3 targetPos = wp.position;
        Vector3 direction = (targetPos - transform.position).normalized;
        direction = ApplySeparation(direction);
        Vector3 movePosition = transform.position + speed * Time.deltaTime * direction;
        myBody.MovePosition(movePosition);

        if (Vector3.Distance(transform.position, targetPos) < reachDistance)
        {
            currWaypointIndex--;
            if (currWaypointIndex < 0)
            {
                enabled = false;
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
}
