using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace vSplines
{
    [Serializable]
    public class Spline
    {
        public Vector3 Start => Segments.First().FirstControlPoint.Position;
        public Vector3 End => Segments.Last().SecondControlPoint.Position;

        public List<SplineSegment> Segments = new List<SplineSegment>();
        public float Length;
        
        public void Recalculate()
        {
            for (int i = 0; i < Segments.Count; i++)
            {
                var segment = Segments[i];
                segment.Recalculate();
                if (i < Segments.Count - 1)
                {
                    Segments[i + 1].FirstControlPoint.Position = segment.SecondControlPoint.Position;
                }
            }
            Length = Segments.Sum(s => s.Length);
        }

        public void DrawGizmos(Color lineColor)
        {
            foreach(var segment in Segments)
            {
                segment.DrawGizmos(lineColor);
            }
        }
    }
}

