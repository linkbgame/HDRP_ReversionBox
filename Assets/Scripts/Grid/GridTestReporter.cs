using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class GridTestReporter : MonoBehaviour
{
    [SerializeField] private bool logOnStart = true;

    private void Start()
    {
        if (!logOnStart)
        {
            return;
        }

        LogGridSnapshot();
    }

    [ContextMenu("Log Grid Snapshot")]
    public void LogGridSnapshot()
    {
        GridBoard board = GridBoard.Instance;
        if (board == null)
        {
            Debug.LogWarning("[GridTestReporter] GridBoard.Instance is null.", this);
            return;
        }

        int wallCount = 0;
        int plateCount = 0;
        int gateCount = 0;
        int occupantCount = 0;

        StringBuilder occupantLines = new StringBuilder();

        foreach (var entry in board.Cells)
        {
            GridCell cell = entry.Value;
            if (cell.IsWall)
            {
                wallCount++;
            }

            if (cell.HasFlag(GridCellFlags.HasPlate))
            {
                plateCount++;
            }

            if (cell.HasFlag(GridCellFlags.HasGate))
            {
                gateCount++;
            }

            if (cell.HasOccupant)
            {
                occupantCount++;
                occupantLines.AppendLine(
                    $"- {cell.occupant.name} @ {entry.Key} ({cell.OccupantType})"
                );
            }
        }

        Debug.Log(
            "[GridTestReporter] Grid snapshot\n" +
            $"Cells: {board.Cells.Count}\n" +
            $"Walls: {wallCount}\n" +
            $"Plates: {plateCount}\n" +
            $"Gates: {gateCount}\n" +
            $"Occupants: {occupantCount}\n" +
            occupantLines,
            this
        );
    }
}
