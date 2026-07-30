using System;
using UnityEngine;

[Serializable]
public class GridWorldConverter
{
    [SerializeField] private Vector3 origin = Vector3.zero;
    [SerializeField] private float cellSize = 1f;

    public Vector3 Origin
    {
        get => origin;
        set => origin = value;
    }

    public float CellSize
    {
        get => cellSize;
        set => cellSize = Mathf.Max(0.001f, value);
    }

    public GridCoord WorldToGrid(Vector3 worldPosition)
    {
        return new GridCoord(
            Mathf.RoundToInt((worldPosition.x - origin.x) / CellSize),
            Mathf.RoundToInt((worldPosition.z - origin.z) / CellSize)
        );
    }

    public Vector3 GridToWorld(GridCoord coord, float centerY)
    {
        return new Vector3(
            origin.x + coord.x * CellSize,
            centerY,
            origin.z + coord.z * CellSize
        );
    }

    public bool IsNearGridCenter(Vector3 worldPosition, float tolerance)
    {
        GridCoord coord = WorldToGrid(worldPosition);
        Vector3 snapped = GridToWorld(coord, worldPosition.y);
        float maxDelta = Mathf.Max(
            Mathf.Abs(snapped.x - worldPosition.x),
            Mathf.Abs(snapped.z - worldPosition.z)
        );
        return maxDelta <= tolerance;
    }
}
