using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class SplineTools
{
    private static Matrix4x4 GetGlobalTRS(this Transform transform)
    {
        return Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
    }

    private static Matrix4x4 GetLocalTRS(this Transform transform)
    {
        return Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale);
    }

    private static Quaternion GetRotation(this Matrix4x4 m)
    {
        return Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
    }

    private static Vector2 xz(this Vector3 obj)
    {
        return new Vector2(obj.x, obj.z);
    }

    public static Vector3 GetTangent(this SplineSegment s, float naturalT)
    {
        var up = s.GetUpVector(naturalT);
        return Vector3.Cross(s.GetNormal(naturalT), up);
    }

    public static Vector3 GetUpVector(this SplineSegment s, float naturalT)
    {
        return Vector3.Lerp(s.FirstControlPoint.UpVector, s.SecondControlPoint.UpVector, naturalT);
    }

    public static Vector3 GetNormal(this SplineSegment s, float naturalT)
    {
        var p0 = s.FirstControlPoint.Position;
        var p1 = s.FirstControlPoint.Position + s.FirstControlPoint.Control;
        var p2 = s.SecondControlPoint.Position + s.SecondControlPoint.Control;
        var p3 = s.SecondControlPoint.Position;

        var a = 3 * Mathf.Pow(1 - naturalT, 2) * (p1 - p0);
        var b = 6 * (1 - naturalT) * naturalT * (p2 - p1);
        var c = 3 * Mathf.Pow(naturalT, 2) * (p3 - p2);
        return a + b + c;
    }

    public static float NaturalToUniformTime(this SplineSegment s, float naturalT)
    {
        for (var i = 1; i < s.Points.Count; i++)
        {
            var thisPoint = s.Points[i];
            var thisNT = thisPoint.NaturalTime;

            if (naturalT > thisNT)
            {
                continue;
            }

            var lastPoint = s.Points[i - 1];
            var lastNT = lastPoint.NaturalTime;

            var frac = naturalT - lastNT;
            var lerp = frac / (thisNT - lastNT);

            return Mathf.Lerp(lastPoint.AccumLength / s.Length, thisPoint.AccumLength / s.Length, lerp);
        }
        return 1;
    }

    public static float UniformToNaturalTime(this SplineSegment s, float uniformT)
    {
        var length = s.Length;
        for (var i = 1; i < s.Points.Count; i++)
        {
            var thisPoint = s.Points[i];
            var thisUt = thisPoint.AccumLength / length;

            if (uniformT > thisUt)
            {
                continue;
            }

            var lastPoint = s.Points[i - 1];
            var lastUt = lastPoint.AccumLength / length;

            var frac = uniformT - lastUt;
            var lerp = frac / (thisUt - lastUt);
            return Mathf.Lerp(lastPoint.NaturalTime, thisPoint.NaturalTime, lerp);
        }
        return 1;
    }

    public static Vector3 GetNaturalPointOnSplineSegment(this SplineSegment s, float naturalT)
    {
        var p0 = s.FirstControlPoint.Position;
        var p1 = s.FirstControlPoint.Position + s.FirstControlPoint.Control;
        var p2 = s.SecondControlPoint.Position + s.SecondControlPoint.Control;
        var p3 = s.SecondControlPoint.Position;

        var t2 = naturalT * naturalT;
        var t3 = naturalT * t2;

        var mt = 1 - naturalT;
        var mt2 = mt * mt;
        var mt3 = mt * mt2;

        return p0 * mt3 + 3 * p1 * mt2 * naturalT + 3 * p2 * mt * t2 + p3 * t3;
    }

    public static Vector3 GetUniformPointOnSplineSegment(this SplineSegment s, float uniformTime)
    {
        var totalDist = s.Length;

        // March forward along the spline
        for (var i = 0; i < s.Points.Count; i++)
        {
            var thisPoint = s.Points[i];
            var percentageThrough = thisPoint.AccumLength / totalDist;

            if (percentageThrough >= uniformTime)
            {
                // We've found our point...
                if (i == 0)
                {
                    return s.Points[i].Position;
                }

                // Interpolate - tricky!
                var lastPoint = s.Points[i - 1];
                var lastPercent = lastPoint.AccumLength / totalDist;
                var percentDelta = (uniformTime - lastPercent) / (percentageThrough - lastPercent);
                return Vector3.Lerp(lastPoint.Position, thisPoint.Position, percentDelta);
            }
        }
        return s.Points[s.Points.Count - 1].Position;
    }

    public static void ApplyMatrix(this SplineSegment spline, Matrix4x4 mat)
    {
        spline.FirstControlPoint.ApplyMatrix(mat);
        spline.SecondControlPoint.ApplyMatrix(mat);
        for (int i = 0; i < spline.Points.Count; i++)
        {
            var splinePoint = spline.Points[i];
            splinePoint.Position = mat.MultiplyPoint3x4(splinePoint.Position);
            spline.Points[i] = splinePoint;
        }
    }

    public static void ApplyMatrix(this SplineSegment.ControlPoint point, Matrix4x4 mat)
    {
        point.Position = mat.MultiplyPoint3x4(point.Position);
        point.Control = mat.MultiplyVector(point.Control);
    }

    /// <summary>
    ///     kind of a complex problem - this is a relatively naive and slow solution that will only find an approximation
    ///     and will NOT work for extremely distorted curves
    /// </summary>
    /// <param name="s"></param>
    /// <param name="worldPosxz"></param>
    /// <returns></returns>
    public static float GetClosestUniformTimeOnSplineSegmentXZ(this SplineSegment s, Vector2 worldPosxz/*, float threshold*/)
    {
        //Profiler.BeginSample("GetClosestUniformTimeOnSplineXZ");
        float bestTime = 0;
        float bestDist = float.MaxValue;
        foreach (var splinePoint in s.Points)
        {
            var sqrDist = (splinePoint.Position.xz() - worldPosxz).sqrMagnitude;
            if (sqrDist < bestDist)
            {
                bestDist = sqrDist;
                bestTime = splinePoint.UniformTime;
            }
        }
        //Profiler.EndSample();
        return bestTime;
    }

    public static Vector3 GetClosestPointOnSpline(this SplineSegment segment, Vector3 worldPos)
    {
        var minDist = float.MaxValue;
        var closestPoint = Vector3.zero;
        foreach (var p in segment.Points)
        {
            var dist = (p.Position - worldPos).sqrMagnitude;
            if (dist < minDist)
            {
                minDist = dist;
                closestPoint = p.Position;
            }
        }
        return closestPoint;
    }

    public static Vector3 GetClosestPointOnSpline(this Spline spline, Vector3 worldPos)
    {
        var minDist = float.MaxValue;
        var closestPoint = Vector3.zero;
        foreach (var segment in spline.Segments)
        {
            var closest = GetClosestPointOnSpline(segment, worldPos);
            var dist = (closest - worldPos).sqrMagnitude;
            if (dist < minDist)
            {
                minDist = dist;
                closestPoint = closest;
            }
        }
        return closestPoint;
    }

    public static SplinePoint GetClosestPointOnSpline(this SplineSegment segment, Ray ray, float step, out float distance)
    {
        distance = float.MaxValue;
        Vector3 closestPoint = default;
        var closestTime = 0f;
        for (var t = 0f; t < 1; t += step)
        {
            var point = segment.GetUniformPointOnSplineSegment(t);
            var dist = (point - GetClosestPointOnRay(ray, point)).sqrMagnitude;
            if (dist < distance)
            {
                distance = dist;
                closestPoint = point;
                closestTime = t;
            }
        }
        return new SplinePoint
        {
            Position = closestPoint,
            NaturalTime = closestTime,
            UniformTime = segment.NaturalToUniformTime(closestTime),
            Normal = segment.GetNormal(closestTime)
        };
    }

    public static Vector3 GetClosestPointOnRay(this Ray ray, Vector3 position)
    {
        // Calculate vector from the ray origin to the position
        Vector3 originToPosition = position - ray.origin;

        // Project the vector onto the ray direction
        float projectionLength = Vector3.Dot(originToPosition, ray.direction);

        // Clamp the projection length to zero or positive, as we are working with a ray (not a line)
        projectionLength = Mathf.Max(projectionLength, 0);

        // Calculate the closest point on the ray
        return ray.origin + ray.direction * projectionLength;
    }

    public static SplinePoint GetClosestPointOnSpline(this Spline spline, Ray ray, float step, out SplineSegment seg)
    {
        var minDist = float.MaxValue;
        SplinePoint closestPoint = default;
        seg = null;
        foreach (var segment in spline.Segments)
        {
            var closest = segment.GetClosestPointOnSpline(ray, step, out var dist);
            if (dist < minDist)
            {
                minDist = dist;
                closestPoint = closest;
                seg = segment;
            }
        }
        return closestPoint;
    }

    public static SplineSegment.ControlPoint GetClosestControlPointOnSpline(this Spline spline, Vector3 position, out SplineSegment outSeg)
    {
        var minDist = float.MaxValue;
        SplineSegment.ControlPoint closestPoint = null;
        outSeg = null;
        foreach (var segment in spline.Segments)
        {
            var firstDist = (segment.FirstControlPoint.Position - position).sqrMagnitude;
            if (firstDist < minDist)
            {
                minDist = firstDist;
                closestPoint = segment.FirstControlPoint;
                outSeg = segment;
            }
            var secondDist = (segment.SecondControlPoint.Position - position).sqrMagnitude;
            if (secondDist < minDist)
            {
                minDist = secondDist;
                closestPoint = segment.SecondControlPoint;
                outSeg = segment;
            }
        }
        return closestPoint;
    }

    public static SplineNode GetClosestSplineNode(this Spline spline, Ray ray)
    {
        var minDist = float.MaxValue;
        SplineNode closestNode = null;
        foreach (var node in spline.Nodes)
        {
            var dist = (node.Position - GetClosestPointOnRay(ray, node.Position)).sqrMagnitude;
            if (dist < minDist)
            {
                minDist = dist;
                closestNode = node;
            }
        }
        return closestNode;
    }

    public static void Simplify(this List<SplinePoint> points, float threshold)
    {
        for (int i = points.Count - 2; i >= 1; i--)
        {
            var lastPoint = points[i - 1];
            var point = points[i];
            var nextPoint = points[i + 1];
            var dot = (Mathf.Abs(Vector3.Dot(lastPoint.Normal, point.Normal)) + Mathf.Abs(Vector3.Dot(nextPoint.Normal, point.Normal))) / 2f;
            if (dot < threshold)
            {
                points.RemoveAt(i);
                continue;
            }
        }
    }

    public static List<SplinePoint> AddCorners(this List<SplinePoint> inPoints, CornerParameters parameters)
    {
        var outPoints = new List<SplinePoint>();
        for (int i = 0; i < inPoints.Count; i++)
        {
            var point1 = inPoints[i];
            if (i < inPoints.Count - 2)
            {
                var point2 = inPoints[i + 1];
                var point3 = inPoints[i + 2];

                var delta1 = point1.Position - point2.Position;
                var delta2 = point2.Position - point3.Position;

                var dot = Mathf.Abs(Vector3.Dot(delta1.normalized, delta2.normalized));
                if ((delta1.magnitude + delta2.magnitude) > parameters.Radius && dot < parameters.Threshold)
                {
                    // Insert before and after
                    var leftPos = point2.Position - point1.Normal.normalized * parameters.Radius;
                    //var leftNorm = Vector3.ClampMagnitude(point1.Normal.normalized * parameters.Radius, delta1.magnitude);
                    var leftNorm = point1.Normal.normalized * parameters.Radius;

                    var rightPos = point2.Position + point3.Normal.normalized * parameters.Radius;
                    //var rightNorm = Vector3.ClampMagnitude(-point3.Normal.normalized * parameters.Radius, delta1.magnitude);
                    var rightNorm = -point3.Normal.normalized * parameters.Radius;

                    /*Debug.DrawLine(leftPos, leftPos + leftNorm, Color.magenta);
                    Debug.DrawLine(leftPos, leftPos + Vector3.up, Color.magenta);
                    Debug.DrawLine(rightPos, rightPos + rightNorm, Color.cyan);
                    Debug.DrawLine(rightPos, rightPos + Vector3.up, Color.cyan);*/

                    var cornerSeg = new SplineSegment
                    {
                        FirstControlPoint = new SplineSegment.ControlPoint
                        {
                            Position = leftPos,
                            Control = leftNorm
                        },
                        SecondControlPoint = new SplineSegment.ControlPoint
                        {
                            Position = rightPos,
                            Control = rightNorm
                        },
                        Resolution = parameters.Resolution,
                    };
                    cornerSeg.Recalculate();

                    // Quick hack to reduce collisions
                    const int lookDist = 10;
                    var bounds = cornerSeg.Bounds;
                    bounds.Expand(parameters.Radius * .5f);
                    for (var j = Mathf.Min(i + lookDist, inPoints.Count - 2); j >= Mathf.Max(1, i - lookDist); j--)
                    {
                        var testP = inPoints[j];
                        if (bounds.Contains(testP.Position))
                        {
                            //Debug.DrawLine(testP.Position, cornerSeg.Bounds.center, Color.red, 6);
                            inPoints.RemoveAt(j);
                            if (outPoints.Count > j)
                            {
                                outPoints.RemoveAt(j);
                            }
                        }
                    }

                    outPoints.AddRange(cornerSeg.Points);
                    i++;
                }
                else
                {
                    outPoints.Add(point1);
                }
            }
            else
            {
                outPoints.Add(point1);
            }
        }
        return outPoints;
    }

    public static void DrawCube(Vector3 origin, Vector3 extents, Quaternion rotation, Color color, float duration)
    {
#if !UNITY_EDITOR
        return;
#else
        var verts = new Vector3[]
        {
            // Top square
            origin + rotation*new Vector3(extents.x, extents.y, extents.z),
            origin + rotation*new Vector3(-extents.x, extents.y, extents.z),
            origin + rotation*new Vector3(extents.x, extents.y, -extents.z),
            origin + rotation*new Vector3(-extents.x, extents.y, -extents.z),

            // Bottom square
            origin + rotation*new Vector3(extents.x, -extents.y, extents.z),
            origin + rotation*new Vector3(-extents.x, -extents.y, extents.z),
            origin + rotation*new Vector3(extents.x, -extents.y, -extents.z),
            origin + rotation*new Vector3(-extents.x, -extents.y, -extents.z),
        };

        // top square
        Debug.DrawLine(verts[0], verts[2], color, duration);
        Debug.DrawLine(verts[1], verts[3], color, duration);
        Debug.DrawLine(verts[1], verts[0], color, duration);
        Debug.DrawLine(verts[2], verts[3], color, duration);

        // bottom square
        Debug.DrawLine(verts[4], verts[6], color, duration);
        Debug.DrawLine(verts[5], verts[7], color, duration);
        Debug.DrawLine(verts[5], verts[4], color, duration);
        Debug.DrawLine(verts[6], verts[7], color, duration);

        // connections
        Debug.DrawLine(verts[0], verts[4], color, duration);
        Debug.DrawLine(verts[1], verts[5], color, duration);
        Debug.DrawLine(verts[2], verts[6], color, duration);
        Debug.DrawLine(verts[3], verts[7], color, duration);
#endif
    }

    public static Vector3 GetClosestPointOnBounds(this Spline spline, Vector3 worldPos)
    {
        var minDist = float.MaxValue;
        var closestPoint = Vector3.zero;
        foreach (var segment in spline.Segments)
        {
            var closest = segment.Bounds.ClosestPoint(worldPos);
            var dist = (closest - worldPos).sqrMagnitude;
            if (dist < minDist)
            {
                minDist = dist;
                closestPoint = closest;
            }
        }
        return closestPoint;
    }

    public static float GetClosestUniformTimeOnSplineSegment(this SplineSegment s, Vector3 worldPos, float threshold)
    {
        // Basically a binary search along a spline
        var rangeT = new Vector2(0, 1);
        while (true)
        {
            var midPointT = Mathf.Lerp(rangeT.x, rangeT.y, .5f);

            var minPos = s.GetUniformPointOnSplineSegment(rangeT.x);
            var midPos = s.GetUniformPointOnSplineSegment(midPointT);
            var maxPos = s.GetUniformPointOnSplineSegment(rangeT.y);

            var firstDist = (minPos - worldPos).magnitude;
            var midDist = (midPos - worldPos).magnitude;
            var secondDist = (maxPos - worldPos).magnitude;

            var firstSegment = firstDist + midDist;
            var secondSegment = midDist + secondDist;

            // The current candidate is good enough
            if (Mathf.Abs(firstDist - secondDist) < threshold)
            {
                return midPointT;
            }

            if (firstSegment < secondSegment)
            {
                rangeT = new Vector2(rangeT.x, midPointT);
            }
            else
            {
                rangeT = new Vector2(midPointT, rangeT.y);
            }
        }
    }

    public static Vector3 GetDistancePointAlongSplineSegment(this SplineSegment s, float distance)
    {
        if (distance >= s.Length)
        {
            return s.SecondControlPoint.Position;
        }
        else if (distance <= 0)
        {
            return s.FirstControlPoint.Position;
        }
        var t = distance / s.Length;
        return s.GetUniformPointOnSplineSegment(t);
    }

    public static Vector3 GetDistancePointAlongSpline(this Spline s, float distance)
    {
        if (distance >= s.Length)
        {
            return s.End;
        }
        else if (distance <= 0)
        {
            return s.Start;
        }
        var lengthAccum = 0f;
        foreach (var segment in s.Segments)
        {
            if (lengthAccum + segment.Length <= distance)
            {
                lengthAccum += segment.Length;
                continue;
            }
            var fracLength = distance - lengthAccum;
            return segment.GetDistancePointAlongSplineSegment(fracLength);
        }
        throw new System.Exception("Finding point on spline failed unexpectedly.");
    }
}