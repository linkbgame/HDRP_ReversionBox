using System;

[Serializable]
public struct GridCoord : IEquatable<GridCoord>
{
    public int x;
    public int z;

    public GridCoord(int x, int z)
    {
        this.x = x;
        this.z = z;
    }

    public static GridCoord zero => new GridCoord(0, 0);

    public static GridCoord operator +(GridCoord a, GridCoord b)
    {
        return new GridCoord(a.x + b.x, a.z + b.z);
    }

    public static GridCoord operator -(GridCoord a, GridCoord b)
    {
        return new GridCoord(a.x - b.x, a.z - b.z);
    }

    public static bool operator ==(GridCoord a, GridCoord b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(GridCoord a, GridCoord b)
    {
        return !a.Equals(b);
    }

    public bool Equals(GridCoord other)
    {
        return x == other.x && z == other.z;
    }

    public override bool Equals(object obj)
    {
        return obj is GridCoord other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (x * 397) ^ z;
        }
    }

    public override string ToString()
    {
        return $"({x}, {z})";
    }
}
