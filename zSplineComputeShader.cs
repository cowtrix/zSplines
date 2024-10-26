using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(zSpline))]
public class zSplineComputeShader : MonoBehaviour
{
    public MeshFilter MeshFilter => GetComponent<MeshFilter>();
    public Spline Spline => GetComponent<zSpline>().Data;
    [Range(3, 64)]
    public int Sides = 4;
    public AnimationCurve Radius = AnimationCurve.Linear(0, 1, 1, 1);
    public ComputeShader ComputeShader;
    private static ComputeBuffer m_positionInBuffer, m_distanceInBuffer, m_radiusInBuffer, m_upsInBuffer, m_vertexBuffer, m_normalBuffer, m_uvBuffer, m_triangleBuffer;

    public int DebugTriSkip;

    [ContextMenu("Invalidate")]
    public void Invalidate()
    {
        Spline.Recalculate();

        int numPoints = Spline.AllPoints.Count();

        // Initialize compute buffer with points
        m_positionInBuffer = new ComputeBuffer(numPoints, sizeof(float) * 3);
        m_positionInBuffer.SetData(Spline.AllPoints.Select(p => p.Position).ToList());

        m_distanceInBuffer = new ComputeBuffer(numPoints, sizeof(float));
        var distanceIn = new List<float>();
        for (var i = 0; i < Spline.Segments.Count; i++)
        {
            var seg = Spline.Segments[i];
            foreach (var p in seg.Points)
            {
                distanceIn.Add(p.UniformTime);
            }
        }
        m_distanceInBuffer.SetData(distanceIn);

        m_radiusInBuffer = new ComputeBuffer(numPoints, sizeof(float));
        var radiusList = new List<float>();
        for (var i = 0; i < Spline.Segments.Count; i++)
        {
            var seg = Spline.Segments[i];
            foreach (var p in seg.Points)
            {
                var t = (i + p.NaturalTime) / (Spline.Segments.Count);
                radiusList.Add(Radius.Evaluate(t));
            }
        }
        m_radiusInBuffer.SetData(radiusList);

        m_upsInBuffer = new ComputeBuffer(numPoints, sizeof(float) * 3);
        var ups = new List<Vector3>();
        foreach (var seg in Spline.Segments)
        {
            foreach (var p in seg.Points)
            {
                ups.Add(seg.GetUpVector(p.NaturalTime));
            }
        }
        m_upsInBuffer.SetData(ups);

        int numVertices = numPoints * Sides;
        int numTriangles = (numPoints - 1) * Sides * 2 * 3; // 2 triangles per side, 3 indices per triangle

        // Buffers for vertices, normals, uvs, and triangles
        m_vertexBuffer = new ComputeBuffer(numVertices, sizeof(float) * 3); // Vector3
        m_normalBuffer = new ComputeBuffer(numVertices, sizeof(float) * 3); // Vector3
        m_uvBuffer = new ComputeBuffer(numVertices, sizeof(float) * 2); // Vector2
        m_triangleBuffer = new ComputeBuffer(numTriangles, sizeof(int)); // Triangle indices (int)

        // Set compute shader parameters
        ComputeShader.SetInt("numSides", Sides);
        ComputeShader.SetInt("numPoints", numPoints);
        ComputeShader.SetBuffer(0, "positionsIn", m_positionInBuffer);
        ComputeShader.SetBuffer(0, "radiusIn", m_radiusInBuffer);
        ComputeShader.SetBuffer(0, "upsIn", m_upsInBuffer);
        ComputeShader.SetBuffer(0, "vertices", m_vertexBuffer);
        ComputeShader.SetBuffer(0, "normals", m_normalBuffer);
        ComputeShader.SetBuffer(0, "uvs", m_uvBuffer);
        ComputeShader.SetBuffer(0, "triangles", m_triangleBuffer);

        // Dispatch the compute shader
        ComputeShader.Dispatch(0, numPoints, 1, 1);

        // Retrieve data from GPU
        Vector3[] vertices = new Vector3[numVertices];
        Vector3[] normals = new Vector3[numVertices];
        Vector2[] uvs = new Vector2[numVertices];
        int[] triangles = new int[numTriangles];

        m_vertexBuffer.GetData(vertices);
        m_normalBuffer.GetData(normals);
        m_uvBuffer.GetData(uvs);
        m_triangleBuffer.GetData(triangles);

        // Create the mesh
        var mesh = MeshFilter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh();
        }
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles.Take(DebugTriSkip * 3).ToArray();

        MeshFilter.mesh = mesh;

        // Cleanup
        m_positionInBuffer.Release();
        m_radiusInBuffer.Release();
        m_upsInBuffer.Release();
        m_vertexBuffer.Release();
        m_normalBuffer.Release();
        m_uvBuffer.Release();
        m_triangleBuffer.Release();
    }
}