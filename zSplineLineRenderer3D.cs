using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter), typeof(zSpline))]
public class zSplineLineRenderer3D : MonoBehaviour
{
    public Spline Spline => GetComponent<zSpline>().Data;
    public IEnumerable<SplinePoint> Points => Spline.AllPoints;

    public int CircularResolution = 5;
    public bool Loop;
    public float Radius = .2f;
    public AnimationCurve RadiusOverLifetime = AnimationCurve.Linear(0, 1, 1, 1);
    public bool GenerateCollider;

    private Mesh Mesh
    {
        get
        {
            return Filter.sharedMesh;
        }
        set
        {
            Filter.sharedMesh = value;
        }
    }
    private List<int> _triangleBuffer = new List<int>();
    private List<Vector4> _uvBuffer = new List<Vector4>();
    private List<Vector3> _vertexBuffer = new List<Vector3>();

    protected MeshRenderer Renderer => GetComponent<MeshRenderer>();
    protected MeshFilter Filter => GetComponent<MeshFilter>();
    protected MeshCollider Collider => GetComponent<MeshCollider>();
    protected void GetNormalTangent(List<SplinePoint> points, int i, out Vector3 normal, out Vector3 tangent)
    {
        if (points.Count < 2)
        {
            normal = Vector3.zero;
            tangent = Vector3.zero;
            return;
        }

        var position = points[i].Position;
        Vector3 lastPoint, nextPoint;
        if (i == 0)
        {
            lastPoint = Loop ? points[points.Count - 1].Position : position;
            nextPoint = points[1].Position;
        }
        else if (i == points.Count - 1)
        {
            lastPoint = points[points.Count - 2].Position;
            nextPoint = Loop ? points[0].Position : position;
        }
        else
        {
            lastPoint = points[i - 1].Position;
            nextPoint = points[i + 1].Position;
        }

        normal = ((lastPoint - position) + (position - nextPoint)) / 2;
        tangent = Vector3.Max(Vector3.Cross(normal, transform.up), Vector3.Max(Vector3.Cross(normal, transform.right), Vector3.Cross(normal, transform.forward))).normalized;

        normal.Normalize();
        tangent.Normalize();
    }

    [ContextMenu("Rebake")]
    public void RebakeMesh()
    {
        if (Mesh == null)
        {
            Mesh = new Mesh();
        }
        _vertexBuffer.Clear();
        _triangleBuffer.Clear();
        _uvBuffer.Clear();
        Mesh.Clear();

        var points = Points.ToList();
        if (points.Count < 2)
        {
            return;
        }

        if (Loop)
        {
            points.Add(points[0]);
        }

        for (var i = points.Count - 1; i >= 0; i--)
        {
            var position = transform.localToWorldMatrix.MultiplyPoint3x4(points[i].Position);
            var maxPoints = (float)Points.Count();
            var lifetime = ((points.Count - i - 1) / maxPoints);

            Vector3 normal, tangent;
            GetNormalTangent(points, i, out normal, out tangent);
            var anglestep = 360 / CircularResolution;
            for (var step = 0; step < CircularResolution; step++)
            {
                var angle = step * anglestep;
                var radius = Radius * RadiusOverLifetime.Evaluate(lifetime);
                var circlePosition = position + Quaternion.AngleAxis(angle, normal)
                                     * tangent * radius;
                circlePosition = transform.InverseTransformPoint(circlePosition);

                // Add vertex
                _vertexBuffer.Add(circlePosition);
                _uvBuffer.Add(new Vector4((step / (float)(CircularResolution - 1)), lifetime));
                if (i == points.Count - 1)
                {
                    continue;
                }

                // Add tris
                var p1 = _vertexBuffer.Count - 1;
                var p2 = p1 - CircularResolution;
                var p3 = p1 + 1;
                var p4 = p2 + 1;
                if (step == CircularResolution - 1)
                {
                    p3 -= CircularResolution;
                    p4 -= CircularResolution;
                }
                _triangleBuffer.Add(p1);
                _triangleBuffer.Add(p2);
                _triangleBuffer.Add(p3);

                _triangleBuffer.Add(p3);
                _triangleBuffer.Add(p2);
                _triangleBuffer.Add(p4);
            }
        }

        if (Loop)
        {
            points.RemoveAt(points.Count - 1);
        }

        Mesh.SetVertices(_vertexBuffer);
        Mesh.SetTriangles(_triangleBuffer, 0);
        Mesh.SetUVs(0, _uvBuffer);
        Mesh.RecalculateNormals();

        if (GenerateCollider && Collider)
        {
            Collider.sharedMesh = Mesh;
        }
    }
}