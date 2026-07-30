using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(GridBoard))]
public class GridBootstrapper : MonoBehaviour
{
    [Header("Build")]
    [SerializeField] private bool buildOnStart = true;
    [SerializeField] private bool clearBeforeBuild = true;

    [Header("Layer Names")]
    [SerializeField] private string wallLayerName = "Wall";
    [SerializeField] private string playerLayerName = "Player";
    [SerializeField] private string yellowBoxLayerName = "YellowBox";
    [SerializeField] private string blueBoxLayerName = "BlueBox";

    [Header("Ignored Visual Roots")]
    [SerializeField] private string floorRootName = "PlaneCubes";

    private GridBoard board;
    private readonly HashSet<GridOccupant> registeredOccupants = new HashSet<GridOccupant>();

    private void Awake()
    {
        board = GetComponent<GridBoard>();
    }

    private void Start()
    {
        if (buildOnStart)
        {
            BuildFromScene();
        }
    }

    public void BuildFromScene()
    {
        if (board == null)
        {
            board = GetComponent<GridBoard>();
        }

        if (clearBeforeBuild)
        {
            board.Clear();
        }

        registeredOccupants.Clear();

        RegisterWalls();
        RegisterPlates();
        RegisterGates();
        RegisterPlayers();
        RegisterBoxes();
    }

    private void RegisterWalls()
    {
        int wallLayer = LayerMask.NameToLayer(wallLayerName);
        if (wallLayer < 0)
        {
            Debug.LogError($"GridBootstrapper cannot find layer '{wallLayerName}'.", this);
            return;
        }

        Transform[] transforms = FindObjectsOfType<Transform>();
        foreach (Transform item in transforms)
        {
            GameObject go = item.gameObject;
            if (go.layer != wallLayer || IsIgnoredVisualObject(go))
            {
                continue;
            }

            WarnIfOffGrid(go, "Wall");
            GridCoord coord = board.WorldToGrid(go.transform.position);
            GridCell existing = board.GetCell(coord);
            if (existing.HasOccupant || existing.flags != GridCellFlags.None)
            {
                Debug.LogWarning($"Wall '{go.name}' at {coord} overlaps gameplay data while building Grid.", go);
            }

            board.SetTerrain(coord, GridTerrain.Wall);
        }
    }

    private void RegisterPlates()
    {
        TriggerPlate[] plates = FindObjectsOfType<TriggerPlate>();
        foreach (TriggerPlate plate in plates)
        {
            WarnIfOffGrid(plate.gameObject, "TriggerPlate");
            GridCoord coord = board.WorldToGrid(plate.transform.position);
            WarnIfOnWall(plate.gameObject, coord, "TriggerPlate");
            board.AddFlag(coord, GridCellFlags.HasPlate);
        }
    }

    private void RegisterGates()
    {
        ExitGate[] gates = FindObjectsOfType<ExitGate>();
        foreach (ExitGate gate in gates)
        {
            WarnIfOffGrid(gate.gameObject, "ExitGate");
            GridCoord coord = board.WorldToGrid(gate.transform.position);
            WarnIfOnWall(gate.gameObject, coord, "ExitGate");
            board.AddFlag(coord, GridCellFlags.HasGate);
        }
    }

    private void RegisterPlayers()
    {
        int playerLayer = LayerMask.NameToLayer(playerLayerName);
        PlayerFSM[] players = FindObjectsOfType<PlayerFSM>();
        foreach (PlayerFSM player in players)
        {
            if (playerLayer >= 0 && player.gameObject.layer != playerLayer)
            {
                Debug.LogWarning($"Player '{player.name}' is not on expected layer '{playerLayerName}'.", player);
            }

            RegisterOccupant(player.gameObject, GridOccupantType.Player, "Player");
        }
    }

    private void RegisterBoxes()
    {
        PushableEntity[] boxes = FindObjectsOfType<PushableEntity>();
        foreach (PushableEntity box in boxes)
        {
            GridOccupantType type = ResolveBoxType(box.gameObject);
            RegisterOccupant(box.gameObject, type, "Box");
        }
    }

    private void RegisterOccupant(GameObject go, GridOccupantType type, string label)
    {
        if (type == GridOccupantType.None)
        {
            Debug.LogWarning($"{label} '{go.name}' has no supported Grid occupant type.", go);
            return;
        }

        GridOccupant occupant = go.GetComponent<GridOccupant>();
        if (occupant == null)
        {
            occupant = go.AddComponent<GridOccupant>();
        }

        occupant.Configure(type);
        if (registeredOccupants.Contains(occupant))
        {
            return;
        }

        WarnIfOffGrid(go, label);
        GridCoord coord = board.WorldToGrid(go.transform.position);
        if (!board.TryRegisterOccupant(occupant, coord, out string error))
        {
            Debug.LogError(error, go);
            return;
        }

        registeredOccupants.Add(occupant);
    }

    private GridOccupantType ResolveBoxType(GameObject box)
    {
        int yellowLayer = LayerMask.NameToLayer(yellowBoxLayerName);
        int blueLayer = LayerMask.NameToLayer(blueBoxLayerName);

        if (box.layer == yellowLayer)
        {
            return GridOccupantType.YellowBox;
        }

        if (box.layer == blueLayer)
        {
            return GridOccupantType.BlueBox;
        }

        return GridOccupant.ResolveOccupantTypeFromLayer(box.layer);
    }

    private void WarnIfOffGrid(GameObject go, string label)
    {
        if (!board.IsNearGridCenter(go.transform.position))
        {
            GridCoord coord = board.WorldToGrid(go.transform.position);
            Debug.LogWarning($"{label} '{go.name}' is not close to a grid center. Snapped coord: {coord}.", go);
        }
    }

    private void WarnIfOnWall(GameObject go, GridCoord coord, string label)
    {
        if (board.GetCell(coord).IsWall)
        {
            Debug.LogWarning($"{label} '{go.name}' is placed on a Wall cell at {coord}.", go);
        }
    }

    private bool IsIgnoredVisualObject(GameObject go)
    {
        Transform root = go.transform;
        while (root.parent != null)
        {
            root = root.parent;
        }

        return root.name == floorRootName;
    }
}
