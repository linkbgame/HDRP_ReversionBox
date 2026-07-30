using UnityEngine;

[DisallowMultipleComponent]
public class GridOccupant : MonoBehaviour
{
    [SerializeField] private GridOccupantType occupantType = GridOccupantType.None;
    [SerializeField] private GridCoord currentCoord;

    public GridOccupantType OccupantType => occupantType;
    public GridCoord CurrentCoord => currentCoord;

    public void Configure(GridOccupantType type)
    {
        occupantType = type;
    }

    public void Initialize(GridBoard board, GridCoord coord)
    {
        currentCoord = coord;
    }

    public void SetCoord(GridCoord coord)
    {
        currentCoord = coord;
    }

    private void Reset()
    {
        occupantType = ResolveOccupantTypeFromLayer(gameObject.layer);
    }

    private void OnValidate()
    {
        if (occupantType == GridOccupantType.None)
        {
            occupantType = ResolveOccupantTypeFromLayer(gameObject.layer);
        }
    }

    public static GridOccupantType ResolveOccupantTypeFromLayer(int layer)
    {
        string layerName = LayerMask.LayerToName(layer);
        switch (layerName)
        {
            case "Player":
                return GridOccupantType.Player;
            case "YellowBox":
                return GridOccupantType.YellowBox;
            case "BlueBox":
                return GridOccupantType.BlueBox;
            default:
                return GridOccupantType.None;
        }
    }
}
