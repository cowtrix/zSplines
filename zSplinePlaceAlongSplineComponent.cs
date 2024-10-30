using System.Collections.Generic;
using UnityEngine;

public class zSplinePlaceAlongSplineComponent : zSplineComponent
{
    public enum ePlacementMode
    {
        NaturalTime,
        UniformTime,
        Distance,
    }

    public GameObject Prefab;
    public List<GameObject> Instances;

    public ePlacementMode PlacementMode;
    public float Step = .5f;
    public Vector3 AdditionalRotation;
    public Vector3 AdditionalOffset;
    public float Scale = 1;

    protected override void InvalidateInternal()
    {
        foreach (var instance in Instances)
        {
            DestroyInstance(instance);
        }
        Instances.Clear();
        var targetI = 1f;
        if (PlacementMode == ePlacementMode.Distance)
        {
            targetI = Data.Length;
        }
        foreach (var segment in Data.Segments)
        {
            for (var i = 0f; i <= targetI; i += Step)
            {
                Vector3 position = default;
                Vector3 normal = default;
                switch (PlacementMode)
                {
                    case ePlacementMode.UniformTime:
                        position = segment.GetUniformPointOnSplineSegment(i);
                        normal = segment.GetNormal(segment.UniformToNaturalTime(i));
                        break;
                    case ePlacementMode.NaturalTime:
                        position = segment.GetUniformPointOnSplineSegment(i);
                        normal = segment.GetNormal(i);
                        break;
                    case ePlacementMode.Distance:
                        position = segment.GetUniformPointOnSplineSegment(i / segment.Length);
                        normal = segment.GetNormal(segment.UniformToNaturalTime(i / segment.Length));
                        break;
                }
                var instance = GetNewInstance();
                instance.transform.SetParent(transform, false);
                Instances.Add(instance);
                instance.transform.localPosition = position;
                instance.transform.LookAt(transform.localToWorldMatrix.MultiplyPoint3x4(position + normal));
                instance.transform.localPosition += AdditionalOffset;
                instance.transform.localRotation *= Quaternion.Euler(AdditionalRotation);
                instance.transform.localScale = Vector3.one * Scale;
            }
        }
    }

    private void DestroyInstance(GameObject instance)
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Destroy(instance);
        }
        else
        {
            DestroyImmediate(instance);
        }
#else
        Destroy(instance);
#endif
    }

    private GameObject GetNewInstance()
    {
        GameObject newInstance = null;
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            newInstance = Instantiate(newInstance);
        }
        else
        {
            newInstance = UnityEditor.PrefabUtility.InstantiatePrefab(Prefab) as GameObject;
        }
#else
        newInstance = Instantiate(newInstance);
#endif
        return newInstance;
    }
}
