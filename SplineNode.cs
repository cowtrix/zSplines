using System;
using UnityEngine;

[Serializable]
public class SplineNode
{
    public Vector3 Position;
    public Vector3 UpVector = new Vector3(0, 1, 0);
    public Vector3 LeftControl, RightControl;
    public int LeftSegmentIndex = -1;
    public int RightSegmentIndex = -1;
    public eSplineControlMode Mode;
    public int LastHash;

    public override bool Equals(object obj)
    {
        return obj is SplineNode node &&
               Position.Equals(node.Position) &&
               UpVector.Equals(node.UpVector) &&
               LeftControl.Equals(node.LeftControl) &&
               RightControl.Equals(node.RightControl) &&
               LeftSegmentIndex == node.LeftSegmentIndex &&
               RightSegmentIndex == node.RightSegmentIndex &&
               Mode == node.Mode;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Position, UpVector, LeftControl, RightControl, LeftSegmentIndex, RightSegmentIndex, Mode);
    }
}
