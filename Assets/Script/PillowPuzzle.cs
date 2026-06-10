using UnityEngine;

public class PillowPuzzle : MonoBehaviour
{
    // 在Inspector中指定要显示的纸条对象
    public GameObject noteObject;

    // 枕头的初始位置
    private Vector3 initialPosition;

    // 触发显示纸条所需的移动距离阈值
    public float revealDistance = 0.3f; // 单位是米，可以根据你的场景大小调整

    // 一个标记，确保纸条只被显示一次
    private bool isNoteRevealed = false;

    void Start()
    {
        // 1. 检查纸条对象是否已设置，如果没有则给出警告
        if (noteObject == null)
        {
            Debug.LogError("请在Inspector中指定Note Object!");
            return;
        }

        // 2. 游戏开始时，确保纸条是隐藏的
        noteObject.SetActive(false);

        // 3. 记录枕头的初始位置
        initialPosition = transform.position;
    }

    void Update()
    {
        // 4. 如果纸条已经被显示，则不再执行任何操作
        if (isNoteRevealed)
        {
            return;
        }

        // 5. 计算枕头当前位置与初始位置的距离
        float distanceFromStart = Vector3.Distance(transform.position, initialPosition);

        // 6. 如果距离超过了我们设定的阈值
        if (distanceFromStart > revealDistance)
        {
            // 显示纸条
            RevealNote();
        }
    }

    private void RevealNote()
    {
        // 激活纸条对象，让它在场景中可见
        noteObject.SetActive(true);

        // 更新标记，防止Update函数重复调用
        isNoteRevealed = true;

        // 打印一条日志，方便调试
        Debug.Log("纸条已被发现!");

        // 可选：为了性能，可以在纸条显示后禁用此脚本
        // this.enabled = false;
    }
}