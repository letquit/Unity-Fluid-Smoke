using System;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class SmokeBlocker : MonoBehaviour
{
    [SerializeField] private JosStamSmokeEmitter _smokeEmitter;
    private BoxCollider2D _collider;

    private void Awake()
    {
        _collider = GetComponent<BoxCollider2D>();
    }

    private void Update()
    {
        Vector2 min = WorldToGrid(_collider.bounds.min);
        Vector2 max = WorldToGrid(_collider.bounds.max);

        int minX = Mathf.Clamp(Mathf.FloorToInt(min.x), 1, _smokeEmitter.GridSize);
        int maxX = Mathf.Clamp(Mathf.FloorToInt(max.x), 1, _smokeEmitter.GridSize);
        int minY = Mathf.Clamp(Mathf.FloorToInt(min.y), 1, _smokeEmitter.GridSize);
        int maxY = Mathf.Clamp(Mathf.FloorToInt(max.y), 1, _smokeEmitter.GridSize);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector2 gridWorldPos = GridToWorld(new Vector2(x, y));
                if (_collider.OverlapPoint(gridWorldPos))
                {
                    _smokeEmitter.SetBlockedCell(x, y);
                }
            }
        }
    }

    private Vector2 WorldToGrid(Vector2 worldPos)
    {
        Vector2 localPos = worldPos - (Vector2)_smokeEmitter.transform.position;
        return new Vector2(
            (localPos.x / _smokeEmitter.CellSize) + (_smokeEmitter.GridSize * 0.5f),
            (localPos.y / _smokeEmitter.CellSize) + (_smokeEmitter.GridSize * 0.5f)
        );
    }
    
    private Vector2 GridToWorld(Vector2 gridPos)
    {
        return _smokeEmitter.transform.position + new Vector3(
            (gridPos.x - (_smokeEmitter.GridSize * 0.5f)) * _smokeEmitter.CellSize,
            (gridPos.y - (_smokeEmitter.GridSize * 0.5f)) * _smokeEmitter.CellSize,
            0
        );
    }
}
