using System;
using UnityEngine;

/// <summary>
/// 烟雾阻挡器组件，用于在烟雾网格中标识被阻挡的单元格
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class SmokeBlocker : MonoBehaviour
{
    /// <summary>
    /// 关联的烟雾发射器组件
    /// </summary>
    [SerializeField] private JosStamSmokeEmitter _smokeEmitter;
    /// <summary>
    /// 组件的碰撞体
    /// </summary>
    private BoxCollider2D _collider;

    /// <summary>
    /// 初始化组件，在Awake时获取BoxCollider2D组件
    /// </summary>
    private void Awake()
    {
        _collider = GetComponent<BoxCollider2D>();
    }

    /// <summary>
    /// 每帧更新，检测与烟雾网格的重叠区域并标记阻挡单元格
    /// </summary>
    private void Update()
    {
        // 将碰撞体边界从世界坐标转换为网格坐标
        Vector2 min = WorldToGrid(_collider.bounds.min);
        Vector2 max = WorldToGrid(_collider.bounds.max);

        // 计算网格范围并限制在有效范围内
        int minX = Mathf.Clamp(Mathf.FloorToInt(min.x), 1, _smokeEmitter.GridSize);
        int maxX = Mathf.Clamp(Mathf.FloorToInt(max.x), 1, _smokeEmitter.GridSize);
        int minY = Mathf.Clamp(Mathf.FloorToInt(min.y), 1, _smokeEmitter.GridSize);
        int maxY = Mathf.Clamp(Mathf.FloorToInt(max.y), 1, _smokeEmitter.GridSize);

        // 遍历网格范围内的所有单元格
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector2 gridWorldPos = GridToWorld(new Vector2(x, y));
                // 检查网格位置是否与碰撞体重叠
                if (_collider.OverlapPoint(gridWorldPos))
                {
                    // 标记该单元格为阻挡状态
                    _smokeEmitter.SetBlockedCell(x, y);
                }
            }
        }
    }

    /// <summary>
    /// 将世界坐标转换为网格坐标
    /// </summary>
    /// <param name="worldPos">世界坐标位置</param>
    /// <returns>对应的网格坐标</returns>
    private Vector2 WorldToGrid(Vector2 worldPos)
    {
        // 网格坐标 = (世界坐标 - 发射器位置) / 单元格大小 + 网格中心偏移
        Vector2 localPos = worldPos - (Vector2)_smokeEmitter.transform.position;
        return new Vector2(
            (localPos.x / _smokeEmitter.CellSize) + (_smokeEmitter.GridSize * 0.5f),
            (localPos.y / _smokeEmitter.CellSize) + (_smokeEmitter.GridSize * 0.5f)
        );
    }
    
    /// <summary>
    /// 将网格坐标转换为世界坐标
    /// </summary>
    /// <param name="gridPos">网格坐标位置</param>
    /// <returns>对应的世界坐标</returns>
    private Vector2 GridToWorld(Vector2 gridPos)
    {
        // 世界坐标 = 发射器位置 + (网格坐标 - 网格中心偏移) × 单元格大小
        return _smokeEmitter.transform.position + new Vector3(
            (gridPos.x - (_smokeEmitter.GridSize * 0.5f)) * _smokeEmitter.CellSize,
            (gridPos.y - (_smokeEmitter.GridSize * 0.5f)) * _smokeEmitter.CellSize,
            0
        );
    }
}
