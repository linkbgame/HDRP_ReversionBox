using System;

public enum GridTerrain
{
    Empty,
    Wall
}

[Flags]
public enum GridCellFlags
{
    None = 0,
    HasPlate = 1 << 0,
    HasGate = 1 << 1
}

public enum GridOccupantType
{
    None,
    Player,
    YellowBox,
    BlueBox
}

[Serializable]
public struct GridCell
{
    public GridTerrain terrain;
    public GridCellFlags flags;
    public GridOccupant occupant;

    public bool IsWall => terrain == GridTerrain.Wall;
    public bool HasOccupant => occupant != null;
    public GridOccupantType OccupantType => occupant != null ? occupant.OccupantType : GridOccupantType.None;

    public bool CanAccept(GridOccupant nextOccupant)
    {
        if (IsWall)
        {
            return false;
        }

        return occupant == null || occupant == nextOccupant;
    }

    public bool HasFlag(GridCellFlags flag)
    {
        return (flags & flag) != 0;
    }
}
