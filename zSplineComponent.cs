using NUnit.Framework;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(zSpline))]
public abstract class zSplineComponent : MonoBehaviour
{
    public Spline Data => GetComponent<zSpline>().Data;

    private void OnEnable()
    {
        Data.OnInvalidated += Spline_OnInvalidated;
    }

    private void Spline_OnInvalidated(object sender, System.EventArgs e)
    {
        Invalidate();
    }

    [ContextMenu("Invalidate")]
    public void Invalidate()
    {
        Data.Recalculate();
        InvalidateInternal();
    }

    protected abstract void InvalidateInternal();

    private void OnDisable()
    {
        Data.OnInvalidated -= Spline_OnInvalidated;
    }
}
