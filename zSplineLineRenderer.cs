using System.Linq;
using UnityEngine;

[RequireComponent(typeof(zSpline))]
[RequireComponent(typeof(LineRenderer))]
[ExecuteAlways]
public class zSplineLineRenderer : MonoBehaviour
{
    public LineRenderer LineRenderer => GetComponent<LineRenderer>();
    public Spline Spline => GetComponent<zSpline>().Data;

    private void OnEnable()
    {
        Spline.OnInvalidated += Spline_OnInvalidated;
    }

    private void Spline_OnInvalidated(object sender, System.EventArgs e)
    {
        Invalidate();
    }

    [ContextMenu("Invalidate")]
    public void Invalidate()
    {
        var points = Spline.AllPoints.Select(p => p.Position).ToArray();
        LineRenderer.positionCount = points.Length;
        LineRenderer.SetPositions(points);
    }

    private void OnDisable()
    {
        Spline.OnInvalidated -= Spline_OnInvalidated;
    }
}