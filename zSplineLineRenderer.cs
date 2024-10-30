using System.Linq;
using UnityEngine;

[RequireComponent(typeof(zSpline))]
[RequireComponent(typeof(LineRenderer))]
[ExecuteAlways]
public class zSplineLineRenderer : zSplineComponent
{
    public LineRenderer LineRenderer => GetComponent<LineRenderer>();

    protected override void InvalidateInternal()
    {
        var points = Data.AllPoints.Select(p => p.Position).ToArray();
        LineRenderer.positionCount = points.Length;
        LineRenderer.SetPositions(points);
    }
}