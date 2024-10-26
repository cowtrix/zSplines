using System;
using UnityEngine;

[Serializable]
public struct SplinePoint
{
    public float AccumLength;
    public Vector3 Position;
    public Vector3 Normal;
    public float NaturalTime;
    public float UniformTime;

    public SplinePoint(Vector3 pos, Vector3 normal, float naturalTime, float accumLength)
    {
        Position = pos;
        Normal = normal;
        NaturalTime = naturalTime;
        AccumLength = accumLength;
        UniformTime = -1;
    }

    public override string ToString() => $"{Position} ({NaturalTime})";
}