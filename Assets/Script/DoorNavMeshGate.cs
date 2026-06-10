using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 关门时用 NavMeshObstacle 挖洞阻挡路径；开门后关闭 Obstacle，使已 Bake 连通的 NavMesh 可供 Agent 通行。
/// 挂在与 Animator 同一扇门对象上（如 porte_ouvrante_salle1）。
/// </summary>
[DisallowMultipleComponent]
public class DoorNavMeshGate : MonoBehaviour
{
    [SerializeField] NavMeshObstacle navMeshObstacle;
    [Tooltip("开门时是否同时禁用门扇上的非 Trigger Collider")]
    [SerializeField] bool disableSolidColliderOnOpen = true;

    bool _opened;

    void Awake()
    {
        if(navMeshObstacle == null)
            navMeshObstacle = GetComponent<NavMeshObstacle>();

        if(navMeshObstacle == null)
        {
            Debug.LogWarning(
                $"[DoorNavMeshGate] No NavMeshObstacle on '{name}'. " +
                "Add Nav Mesh Obstacle (Carve) to block the doorway until opened.");
            return;
        }

        if(!_opened)
            SetClosed();
    }

    /// <summary>开门：停止 carve，可选关闭实体 Collider。</summary>
    public void OpenPassage()
    {
        if(_opened)
            return;

        _opened = true;

        if(navMeshObstacle != null)
            navMeshObstacle.enabled = false;

        if(disableSolidColliderOnOpen)
            DisableSolidCollidersOn(gameObject);
    }

    /// <summary>关门：恢复 carve（用于重置关卡等）。</summary>
    public void SetClosed()
    {
        _opened = false;

        if(navMeshObstacle != null)
        {
            navMeshObstacle.carving = true;
            navMeshObstacle.enabled = true;
        }
    }

    /// <summary>从门根物体打开 NavMesh（可无 DoorNavMeshGate，仅关 Obstacle）。</summary>
    public static void OpenPassageOn(GameObject doorRoot)
    {
        if(doorRoot == null)
            return;

        var gate = doorRoot.GetComponent<DoorNavMeshGate>();
        if(gate != null)
        {
            gate.OpenPassage();
            return;
        }

        var obstacle = doorRoot.GetComponent<NavMeshObstacle>();
        if(obstacle != null)
            obstacle.enabled = false;
    }

    static void DisableSolidCollidersOn(GameObject root)
    {
        foreach(Collider col in root.GetComponents<Collider>())
        {
            if(col != null && !col.isTrigger)
                col.enabled = false;
        }
    }
}
