using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum eSplineControlMode
{
    Straight,
    Flat,
}

[Serializable]
public struct CornerParameters
{
    public float Radius;
    public float Resolution;
    [Range(0, 1)]
    public float Threshold;
    public int LastHash;

    public override bool Equals(object obj)
    {
        return obj is CornerParameters parameters &&
               Radius == parameters.Radius &&
               Resolution == parameters.Resolution &&
               Threshold == parameters.Threshold;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Radius, Resolution, Threshold);
    }
}

[Serializable]
public class Spline
{
    public Vector3 Start => Nodes.First().Position;
    public Vector3 End => Nodes.Last().Position;

    public List<SplineNode> Nodes = new List<SplineNode>();
    public List<SplineSegment> Segments = new List<SplineSegment>();
    public float Resolution = 1;
    public float Length;
    public event EventHandler OnInvalidated;
    [Range(float.Epsilon, 1)]
    public float SimplifyThreshold = 1;
    public CornerParameters Corner;
    public bool Loop;

    public List<SplinePoint> AllPoints = new List<SplinePoint>();

    public void Recalculate()
    {
        var anyDirty = false;

        if (!Nodes.Any())
        {
            Nodes = new List<SplineNode>()
                {
                    new SplineNode
                    {
                        Position = default(Vector3),
                    },
                    new SplineNode
                    {
                        Position = Vector3.forward * 10,
                    }
                };
        }
        foreach (var seg in Segments)
        {
            seg.Resolution = Resolution;
        }
        var expectedSegmentCount = Nodes.Count - (Loop ? 0 : 1);
        for (var i = 0; i < Nodes.Count; i++)
        {
            var node = Nodes[i];

            node.LeftSegmentIndex = i - 1;
            node.RightSegmentIndex = i;


            SplineSegment leftNeighbour = null;
            SplineSegment rightNeighbour = null;
            if (node.LeftSegmentIndex >= 0 && node.LeftSegmentIndex < expectedSegmentCount)
            {
                leftNeighbour = Segments.ElementAtOrDefault(node.LeftSegmentIndex);
                if (leftNeighbour == null)
                {
                    leftNeighbour = new SplineSegment();
                    Segments.Add(leftNeighbour);
                }
            }
            else
            {
                node.LeftSegmentIndex = -1;
                node.LeftControl = default;
            }
            if (node.RightSegmentIndex >= 0 && node.RightSegmentIndex < expectedSegmentCount)
            {
                rightNeighbour = Segments.ElementAtOrDefault(node.RightSegmentIndex);
                if (rightNeighbour == null)
                {
                    rightNeighbour = new SplineSegment { Resolution = Resolution };
                    Segments.Add(rightNeighbour);
                }
            }
            else
            {
                node.RightSegmentIndex = -1;
                node.RightControl = default;
            }


            if (Loop)
            {
                if (node.LeftSegmentIndex < 0)
                {
                    node.LeftSegmentIndex = Nodes.Count;
                }
                if (node.RightSegmentIndex > Nodes.Count)
                {
                    node.RightSegmentIndex = 0;
                }
            }

            // Do controls
            if (node.Mode == eSplineControlMode.Straight || leftNeighbour == null || rightNeighbour == null)
            {
                node.LeftControl = leftNeighbour != null ? (leftNeighbour.FirstControlPoint.Position - node.Position) / 2f : default;
                node.RightControl = rightNeighbour != null ? (rightNeighbour.SecondControlPoint.Position - node.Position) / 2f : default;
            }
            else if (node.Mode == eSplineControlMode.Flat)
            {
                // We know both neighbours aren't null here
                var lastNode = Nodes[i - 1];
                var nextNode = Nodes[i - 1];
                var tangent = (((lastNode.Position - node.Position) + (nextNode.Position - node.Position)) / 2f).normalized;
                node.LeftControl = tangent.normalized * node.LeftControl.magnitude;
                node.RightControl = -tangent.normalized * node.RightControl.magnitude;
                //node.LeftControl = Vector3.Cross(tangent.normalized, node.UpVector) * node.LeftControl.magnitude;
                //node.RightControl = Vector3.Cross(-tangent.normalized, node.UpVector) * node.RightControl.magnitude;
            }

            // Check for changes and invalidate neighbours
            if (node.LastHash != node.GetHashCode())
            {
                anyDirty = true;
                if (leftNeighbour != null)
                {
                    leftNeighbour.SecondControlPoint.Position = node.Position;
                    leftNeighbour.SecondControlPoint.Control = node.LeftControl;
                    leftNeighbour.Recalculate();
                }

                if (rightNeighbour != null)
                {
                    rightNeighbour.FirstControlPoint.Position = node.Position;
                    rightNeighbour.FirstControlPoint.Control = node.RightControl;
                    rightNeighbour.Recalculate();
                }
            }
            node.LastHash = node.GetHashCode();
        }

        if (Corner.LastHash != Corner.GetHashCode())
        {
            anyDirty = true;
            Corner.LastHash = Corner.GetHashCode();
        }

        if (!anyDirty)
        {
            return;
        }

        // Populate points
        AllPoints.Clear();
        for (var i = 0; i < Segments.Count; i++)
        {
            var segment = Segments[i];
            for (var j = 0; j < segment.Points.Count; j++)
            {
                if (j == segment.Points.Count - 1 && i < Segments.Count - 1)
                {
                    continue;
                }
                var point = segment.Points[j];
                AllPoints.Add(point);
            }
        }
        if (Corner.Radius != 0)
        {
            AllPoints = AllPoints.AddCorners(Corner);
        }
        if(SimplifyThreshold < 1)
        {
            AllPoints.Simplify(SimplifyThreshold);
        }
        if (expectedSegmentCount < Segments.Count)
        {
            Segments.RemoveRange(Nodes.Count - expectedSegmentCount, Segments.Count - expectedSegmentCount);
            anyDirty = true;
        }
        if (anyDirty)
        {
            Length = Segments.Sum(s => s.Length);
            OnInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    public void DrawGizmos(Color lineColor, bool drawControls = false)
    {
        foreach (var segment in Segments)
        {
            segment.DrawGizmos(lineColor);
        }
    }

    public SplineNode InsertNode(float naturalTime)
    {
        int segIndex = (int)Math.Floor(naturalTime);
        naturalTime = naturalTime % 1;
        var seg = Segments.ElementAtOrDefault(segIndex);
        if (seg == null)
        {
            throw new ArgumentException("Invalid insertion index");
        }
        var node = Nodes.SingleOrDefault(n => n.LeftSegmentIndex == segIndex);
        if (node == null)
        {
            throw new ArgumentException("Couldn't find node");
        }
        var nodeIndex = Nodes.IndexOf(node);
        var newNode = new SplineNode { Position = seg.GetNaturalPointOnSplineSegment(naturalTime), LeftSegmentIndex = nodeIndex - 1, RightSegmentIndex = nodeIndex + 1, UpVector = Vector3.up };
        Nodes.Insert(nodeIndex, newNode);
        Nodes[nodeIndex - 1].RightSegmentIndex = segIndex;
        Nodes[nodeIndex + 1].LeftSegmentIndex = segIndex;
        Recalculate();
        return newNode;
    }
}
