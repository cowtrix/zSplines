﻿using UnityEngine;

namespace vSplines
{
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
            var rot = s.GetRotation(naturalT);
            up = rot * up;

            return Vector3.Cross(s.GetDeltaOnSplineSegment(naturalT), up);
        }

        public static Quaternion GetRotation(this SplineSegment s, float naturalT)
        {
            return Quaternion.Euler(Vector3.Lerp(s.FirstControlPoint.Rotation, s.SecondControlPoint.Rotation, naturalT));
        }

        public static Vector3 GetUpVector(this SplineSegment s, float naturalT)
        {
            return Vector3.Lerp(s.FirstControlPoint.UpVector, s.SecondControlPoint.UpVector, naturalT);
        }

        public static Vector3 GetDeltaOnSplineSegment(this SplineSegment s, float naturalT)
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
            point.Rotation = (mat.GetRotation() * Quaternion.Euler(point.Rotation)).eulerAngles;
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
}