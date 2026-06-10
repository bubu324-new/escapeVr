using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Inspector-friendly NavMeshAgent destination/path debugger.
/// Use "Calculate Path (No Move)" to match VREscaper partition reachability (same sample radius + CalculatePath).
/// </summary>
public class NavMeshAgentPathDebug : MonoBehaviour
{
    [SerializeField] NavMeshAgent agent;
    [SerializeField] Transform target;
    [Header("Path display")]
    [SerializeField] bool drawPathGizmos = true;
    [SerializeField] bool drawRuntimePath = true;
    [SerializeField] Color pathColor = Color.yellow;
    [SerializeField] Color projectedTargetColor = Color.cyan;
    [SerializeField] float lineDuration = 0f;
    [SerializeField] float sampleRadius = 0.5f;
    [Header("Play mode")]
    [Tooltip("Each frame while playing, refresh SetDestination to target (after NavMesh projection).")]
    [SerializeField] bool followTargetWhilePlaying;

    NavMeshPath _lastCalculatedPath;
    Vector3 _lastProjectedTarget;
    bool _hasProjectedTarget;

    [ContextMenu("Set Destination To Target")]
    public void SetDestinationToTarget()
    {
        if(agent == null || target == null)
        {
            Debug.LogWarning("[NavMeshAgentPathDebug] Assign both agent and target first.");
            return;
        }

        if(!EnsureAgentOnNavMesh())
            return;

        if(!TryProject(target.position, out Vector3 targetOnNav))
        {
            Debug.LogWarning(
                $"[NavMeshAgentPathDebug] Target cannot be projected to NavMesh (radius={sampleRadius}).");
            _hasProjectedTarget = false;
            return;
        }

        _lastProjectedTarget = targetOnNav;
        _hasProjectedTarget = true;

        bool ok = agent.SetDestination(targetOnNav);
        Debug.Log(
            $"[NavMeshAgentPathDebug] SetDestination -> {ok}, target={target.position}, targetOnNav={targetOnNav}, " +
            $"pathStatus={agent.pathStatus}, corners={agent.path.corners.Length}");
    }

    [ContextMenu("Calculate Path (No Move)")]
    public void CalculatePathOnly()
    {
        if(agent == null || target == null)
        {
            Debug.LogWarning("[NavMeshAgentPathDebug] Assign both agent and target first.");
            return;
        }

        if(!TryResolvePathStart(out Vector3 startOnNav))
        {
            Debug.LogWarning(
                $"[NavMeshAgentPathDebug] Path start cannot be resolved on NavMesh (radius={sampleRadius}).");
            return;
        }

        if(!TryProject(target.position, out Vector3 targetOnNav))
        {
            Debug.LogWarning(
                $"[NavMeshAgentPathDebug] Target cannot be projected to NavMesh (radius={sampleRadius}).");
            _hasProjectedTarget = false;
            return;
        }

        _lastProjectedTarget = targetOnNav;
        _hasProjectedTarget = true;

        float distance = Vector3.Distance(startOnNav, target.position);
        float sampleOffset = Vector3.Distance(target.position, targetOnNav);

        var path = new NavMeshPath();
        bool found = NavMesh.CalculatePath(startOnNav, targetOnNav, NavMesh.AllAreas, path);
        _lastCalculatedPath = path;
        Debug.Log(
            $"[NavMeshAgentPathDebug] CalculatePath (VREscaper-aligned) found={found}, " +
            $"pathStatus={path.status}, distance={distance:F2}m, sampleOffset={sampleOffset:F2}m, " +
            $"from={startOnNav}, toHit={targetOnNav}, corners={path.corners.Length}");
    }

    void Update()
    {
        if(!Application.isPlaying)
            return;

        if(followTargetWhilePlaying && agent != null && target != null)
            SetDestinationToTarget();

        if(drawRuntimePath)
            DrawPath(GetPathCorners(), pathColor);
    }

    void OnDrawGizmos()
    {
        if(!drawPathGizmos)
            return;

        DrawPath(GetPathCorners(), pathColor);

        if(target != null && _hasProjectedTarget)
        {
            Gizmos.color = projectedTargetColor;
            Gizmos.DrawSphere(_lastProjectedTarget, 0.15f);
            Gizmos.DrawLine(target.position, _lastProjectedTarget);
        }
    }

    Vector3[] GetPathCorners()
    {
        if(agent != null && agent.hasPath && agent.path.corners != null && agent.path.corners.Length >= 2)
            return agent.path.corners;

        if(_lastCalculatedPath?.corners != null && _lastCalculatedPath.corners.Length >= 2)
            return _lastCalculatedPath.corners;

        return null;
    }

    void DrawPath(Vector3[] corners, Color color)
    {
        if(corners == null || corners.Length < 2)
            return;

        for(int i = 1; i < corners.Length; i++)
            Debug.DrawLine(corners[i - 1], corners[i], color, lineDuration);

        Debug.DrawLine(corners[0], corners[0] + Vector3.up * 0.3f, color, lineDuration);
        Debug.DrawLine(corners[corners.Length - 1], corners[corners.Length - 1] + Vector3.up * 0.5f, color, lineDuration);
    }

    bool TryResolvePathStart(out Vector3 startOnNav)
    {
        startOnNav = agent.transform.position;
        if(agent.isOnNavMesh)
        {
            if(TryProject(agent.transform.position, out startOnNav))
                return true;
            return true;
        }

        return TryProject(agent.transform.position, out startOnNav);
    }

    bool EnsureAgentOnNavMesh()
    {
        if(agent == null)
            return false;
        if(agent.isOnNavMesh)
            return true;

        if(!TryProject(agent.transform.position, out Vector3 agentOnNav))
        {
            Debug.LogWarning(
                $"[NavMeshAgentPathDebug] Agent is not on NavMesh and cannot be projected (radius={sampleRadius}).");
            return false;
        }

        bool warped = agent.Warp(agentOnNav);
        if(!warped || !agent.isOnNavMesh)
        {
            Debug.LogWarning("[NavMeshAgentPathDebug] Failed to warp agent onto NavMesh.");
            return false;
        }

        return true;
    }

    bool TryProject(Vector3 worldPos, out Vector3 navPos)
    {
        navPos = worldPos;
        if(!NavMesh.SamplePosition(worldPos, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            return false;
        navPos = hit.position;
        return true;
    }
}
