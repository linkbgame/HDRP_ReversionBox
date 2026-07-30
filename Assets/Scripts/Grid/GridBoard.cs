using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GridBoard : MonoBehaviour
{
    public static GridBoard Instance { get; private set; }

    [Header("Coordinate Settings")]
    [SerializeField] private GridWorldConverter converter = new GridWorldConverter();
    [SerializeField] private float floorY = -0.5f;

    [Header("Validation")]
    [SerializeField] private float gridSnapTolerance = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private float gizmoYOffset = 0.03f;

    private readonly Dictionary<GridCoord, GridCell> cells = new Dictionary<GridCoord, GridCell>();

    public GridWorldConverter Converter => converter;
    public float FloorY => floorY;
    public float GridSnapTolerance => gridSnapTolerance;
    public IReadOnlyDictionary<GridCoord, GridCell> Cells => cells;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Multiple GridBoard instances found. Keeping '{Instance.name}', ignoring '{name}'.", this);
            return;
        }

        Instance = this;
    }

    public void Clear()
    {
        cells.Clear();
    }

    public GridCoord WorldToGrid(Vector3 worldPosition)
    {
        return converter.WorldToGrid(worldPosition);
    }

    public Vector3 GridToWorld(GridCoord coord, float centerY)
    {
        return converter.GridToWorld(coord, centerY);
    }

    public bool IsNearGridCenter(Vector3 worldPosition)
    {
        return converter.IsNearGridCenter(worldPosition, gridSnapTolerance);
    }

    public GridCell GetCell(GridCoord coord)
    {
        cells.TryGetValue(coord, out GridCell cell);
        return cell;
    }

    public bool TryGetCell(GridCoord coord, out GridCell cell)
    {
        return cells.TryGetValue(coord, out cell);
    }

    public void SetTerrain(GridCoord coord, GridTerrain terrain)
    {
        GridCell cell = GetCell(coord);
        cell.terrain = terrain;
        cells[coord] = cell;
    }

    public void AddFlag(GridCoord coord, GridCellFlags flag)
    {
        GridCell cell = GetCell(coord);
        cell.flags |= flag;
        cells[coord] = cell;
    }

    public bool TryRegisterOccupant(GridOccupant occupant, GridCoord coord, out string error)
    {
        error = null;
        GridCell cell = GetCell(coord);

        if (cell.IsWall)
        {
            error = $"Cannot register {occupant.name} at {coord}: cell is Wall.";
            return false;
        }

        if (cell.occupant != null && cell.occupant != occupant)
        {
            error = $"Cannot register {occupant.name} at {coord}: occupied by {cell.occupant.name}.";
            return false;
        }

        cell.occupant = occupant;
        cells[coord] = cell;
        occupant.Initialize(this, coord);
        return true;
    }

    public bool CanOccupy(GridCoord coord, GridOccupant occupant)
    {
        GridCell cell = GetCell(coord);
        return cell.CanAccept(occupant);
    }

    public bool TryMoveOccupant(GridOccupant occupant, GridCoord targetCoord, out string error)
    {
        error = null;
        GridCoord sourceCoord = occupant.CurrentCoord;

        if (!cells.TryGetValue(sourceCoord, out GridCell sourceCell) || sourceCell.occupant != occupant)
        {
            error = $"Cannot move {occupant.name}: source cell {sourceCoord} is not registered.";
            return false;
        }

        GridCell targetCell = GetCell(targetCoord);
        if (!targetCell.CanAccept(occupant))
        {
            error = $"Cannot move {occupant.name} to {targetCoord}: cell is blocked.";
            return false;
        }

        sourceCell.occupant = null;
        targetCell.occupant = occupant;
        cells[sourceCoord] = sourceCell;
        cells[targetCoord] = targetCell;
        occupant.SetCoord(targetCoord);
        return true;
    }

    public bool HasFlag(GridCoord coord, GridCellFlags flag)
    {
        return GetCell(coord).HasFlag(flag);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || cells == null)
        {
            return;
        }

        foreach (KeyValuePair<GridCoord, GridCell> entry in cells)
        {
            DrawCellGizmo(entry.Key, entry.Value);
        }
    }

    private void DrawCellGizmo(GridCoord coord, GridCell cell)
    {
        Vector3 center = GridToWorld(coord, floorY + gizmoYOffset);
        Vector3 size = new Vector3(converter.CellSize * 0.9f, 0.02f, converter.CellSize * 0.9f);

        if (cell.IsWall)
        {
            Gizmos.color = Color.red;
        }
        else if (cell.HasOccupant)
        {
            Gizmos.color = Color.yellow;
        }
        else if (cell.flags != GridCellFlags.None)
        {
            Gizmos.color = Color.cyan;
        }
        else
        {
            Gizmos.color = Color.gray;
        }

        Gizmos.DrawWireCube(center, size);
    }
}
