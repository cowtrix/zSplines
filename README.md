# zSplines
Spline library for Unity

<img width="745" alt="RoadNetwork_Pathfinding" src="https://user-images.githubusercontent.com/5094696/191903089-88754c7f-0fa8-4160-8894-1531441d2b56.PNG">

Contains two object classes, `Spline` and `SplineSegment`. A `Spline` is a collection of `SplineSegments` which connect into one continous path.

## Create a new spline and draw it as a Gizmos:

<img width="738" alt="zSpline" src="https://user-images.githubusercontent.com/5094696/191904312-44a02648-ec0a-465a-9292-156c1eebbd37.PNG">

```
void OnDrawGizmos()
{
  var spline = new Spline();
  spline.Segments.Add(new SplineSegment // We add a new segment
  {
    FirstControlPoint = new SplineSegment.ControlPoint
    {
      Position = new Vector3(-10, 0, -10),
      Control = new Vector3(10, 0, 0),      // Determines the normal of the spline point
    },
    SecondControlPoint = new SplineSegment.ControlPoint
    {
      Position = new Vector3(10, 0, 10),
      Control = new Vector3(0, 0, -10),
    },
    Resolution = .25f,  // Determines how many points are calculated on the spline path
  });
  spline.Recalculate();
  spline.DrawGizmos(Color.white);
}
```

## Get a point along a spline via distance

<img width="660" alt="zSpline_Point" src="https://user-images.githubusercontent.com/5094696/191904964-cd2dc56a-3bd3-4e78-8e19-d8c8bd384bff.PNG">

```
var distance = 4.5f;
var point = spline.GetDistancePointAlongSpline(distance);
```
