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

    //MARKER 能够接收一个新的占有者
    public bool CanAcceptOneNewOccupant(GridOccupant newOccupant)
    {
        if (IsWall)
        {
            return false;
        }

        return occupant == null || occupant == newOccupant;
    }

    //MARKER 检查是否包含某个地板或出口的flag
    public bool HasFlag(GridCellFlags flag)
    {
        return (flags & flag) != 0;
    }
}
