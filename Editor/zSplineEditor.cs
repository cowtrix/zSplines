using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(zSpline))]
public class zSplineEditor : Editor
{
    public zSpline TargetSpline => (zSpline)target;
    public bool IsEditing { get; private set; }
    public SplineSegment SelectedSegment { get; private set; }
    public SplineSegment HoverSegment { get; private set; }
    public SplineNode SelectedNode { get; private set; }

    private bool m_infoExpanded;
    private bool m_debug;

    public override void OnInspectorGUI()
    {
        GUI.color = IsEditing ? Color.green : Color.white;
        if (GUILayout.Button("Edit"))
        {
            IsEditing = !IsEditing;
        }
        GUI.color = Color.white;
        if (GUILayout.Button("Reset Controls"))
        {
            foreach (var segment in TargetSpline.Data.Segments)
            {
                segment.FirstControlPoint.Control = default;
                segment.SecondControlPoint.Control = default;
            }
        }
        m_debug = EditorGUILayout.Toggle("Debug", m_debug);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        m_infoExpanded = EditorGUILayout.Foldout(m_infoExpanded, "Info");
        if (m_infoExpanded)
        {
            EditorGUILayout.LabelField("Total Points", TargetSpline.Data.AllPoints.Count().ToString());
        }
        EditorGUILayout.EndVertical();

        base.OnInspectorGUI();
    }

    void OnSceneGUI()
    {
        if (!TargetSpline.Data.Segments.Contains(SelectedSegment))
        {
            SelectedSegment = null;
        }
        if (!TargetSpline.Data.Nodes.Contains(SelectedNode))
        {
            SelectedNode = null;
        }
        TargetSpline.Data.Recalculate();
        Handles.matrix = TargetSpline.transform.localToWorldMatrix;
        /*foreach (var segment in TargetSpline.Data.Segments)
        {
            Handles.color = Color.white;
            if (segment == SelectedSegment)
            {
                Handles.color = Color.green;
            }
            else if (segment == HoverSegment)
            {
                Handles.color = Color.yellow;
            }
            for (var i = 0; i < segment.Points.Count - 1; i++)
            {
                var point1 = segment.Points[i];
                var point2 = segment.Points[i + 1];
                Handles.DrawLine(point1.Position, point2.Position);
            }
        }*/
        Handles.color = Color.yellow;
        for (int i = 0; i < TargetSpline.Data.AllPoints.Count - 1; i++)
        {
            var point1 = TargetSpline.Data.AllPoints[i];
            var point2 = TargetSpline.Data.AllPoints[i + 1];
            Handles.DrawLine(point1.Position, point2.Position);
        }

        if (m_debug)
        {
            foreach (var point in TargetSpline.Data.AllPoints)
            {
                Handles.DrawWireCube(point.Position, Vector3.one * .1f);
                Handles.DrawLine(point.Position, point.Position + point.Normal);
            }
        }

        Handles.color = Color.white;
        foreach (var node in TargetSpline.Data.Nodes)
        {
            Handles.DrawWireCube(node.Position, Vector3.one * .09f);
        }
        if (SelectedNode != null)
        {
            Handles.color = Color.green;
            Handles.DrawWireCube(SelectedNode.Position, Vector3.one * .1f);
            Handles.DrawLine(SelectedNode.Position, SelectedNode.Position + SelectedNode.UpVector);

            DrawHandles(SelectedNode);
        }
        Handles.color = Color.white;

        if (!IsEditing)
        {
            Tools.hidden = false;
            return;
        }
        Tools.hidden = true;

        var ev = Event.current;
        if (!ev.shift)
        {
            // Select
            var worldRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            var closestPoint = TargetSpline.Data.GetClosestPointOnSpline(worldRay, .01f, out var seg);
            var closestNode = TargetSpline.Data.GetClosestSplineNode(worldRay);

            if (closestNode == SelectedNode)
            {
                var currentIndex = TargetSpline.Data.Nodes.IndexOf(closestNode);
                var leftDist = float.MaxValue;
                var leftSegment = TargetSpline.Data.Segments.ElementAtOrDefault(SelectedNode.LeftSegmentIndex);
                if (leftSegment != null)
                {
                    leftDist = Vector3.Distance(SelectedNode.Position, leftSegment.FirstControlPoint.Position);
                }
                var rightDist = float.MaxValue;
                var rightSegment = TargetSpline.Data.Segments.ElementAtOrDefault(SelectedNode.RightSegmentIndex);
                if (rightSegment != null)
                {
                    rightDist = Vector3.Distance(SelectedNode.Position, rightSegment.SecondControlPoint.Position);
                }
                if (leftDist > rightDist)
                {
                    closestNode = TargetSpline.Data.Nodes.ElementAt(currentIndex + 1);
                }
                else
                {
                    closestNode = TargetSpline.Data.Nodes.ElementAt(currentIndex - 1);
                }
            }

            if (closestNode != null && closestNode != SelectedNode)
            {
                Handles.color = Color.yellow;
                Handles.DrawWireCube(closestNode.Position, Vector3.one * .1f);
                var content = EditorGUIUtility.IconContent("d_scenepicking_pickable_hover");
                content.text = "Select Control Point";
                var closestPointScreenPos = HandleUtility.WorldPointToSizedRect(closestNode.Position, content, EditorStyles.helpBox);
                closestPointScreenPos.position += new Vector2(0, -16);
                GUILayout.BeginArea(closestPointScreenPos);
                GUILayout.Label(content, EditorStyles.helpBox);
                GUILayout.EndArea();

                if (ev.type == EventType.MouseDown && ev.button == 0)
                {
                    SelectedNode = closestNode;
                    SelectedSegment = seg;
                    ev.Use();
                }
            }
            Handles.color = Color.white;
        }
        else
        {
            // Insert Node Preview
            var worldRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            var closestPoint = TargetSpline.Data.GetClosestPointOnSpline(worldRay, .1f, out var seg);
            var content = EditorGUIUtility.IconContent("CreateAddNew");
            content.text = "Add Node";
            var closestPointScreenPos = HandleUtility.WorldPointToSizedRect(closestPoint.Position, content, EditorStyles.helpBox);
            closestPointScreenPos.position += new Vector2(0, -16);
            Handles.DrawWireCube(closestPoint.Position, Vector3.one * .1f);
            GUILayout.BeginArea(closestPointScreenPos);
            GUILayout.Label(content, EditorStyles.helpBox);
            GUILayout.EndArea();
            if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                Undo.RecordObject(TargetSpline, "InsertNode");
                var segmentOffset = TargetSpline.Data.Segments.IndexOf(seg);
                SelectedNode = TargetSpline.Data.InsertNode(segmentOffset + closestPoint.NaturalTime);
                ev.Use();
            }
            return;
        }

        if (ev.keyCode == KeyCode.Delete && SelectedNode != null)
        {
            Undo.RecordObject(TargetSpline, "DeleteNode");
            TargetSpline.Data.Nodes.Remove(SelectedNode);
            SelectedNode = null;
            ev.Use();
        }
    }

    private void DrawHandles(SplineNode node)
    {
        var pos = node.Position;
        var up = node.UpVector.normalized;
        var rot = (node.LeftControl.magnitude > 0 ? Quaternion.LookRotation(node.LeftControl.normalized, up) : Quaternion.identity) * (node.RightControl.magnitude > 0 ? Quaternion.LookRotation(node.RightControl.normalized, up) : Quaternion.identity);
        Undo.RecordObject(TargetSpline, "Manipulate");
        Handles.DrawLine(pos, pos + node.LeftControl);
        Handles.DrawLine(pos, pos + node.RightControl);
        if (Tools.current == Tool.Move)
        {
            node.Position = Handles.DoPositionHandle(pos, rot);
        }
        else if (Tools.current == Tool.Rotate)
        {
            var newRot = Handles.DoRotationHandle(rot, pos);
            node.LeftControl = newRot * Vector3.forward * node.LeftControl.magnitude;
            node.RightControl = newRot * -Vector3.forward * node.RightControl.magnitude;
        }
        else if (Tools.current == Tool.Scale)
        {
            if (node.LeftSegmentIndex >= 0)
            {
                var newMag = Handles.ScaleSlider(node.LeftControl.magnitude, pos, node.LeftControl.normalized, Quaternion.LookRotation(node.LeftControl.normalized, up), 1, 0);
                node.LeftControl = node.LeftControl.normalized * newMag;
            }
            if (node.RightSegmentIndex >= 0)
            {
                var newMag = Handles.ScaleSlider(node.RightControl.magnitude, pos, node.RightControl.normalized, Quaternion.LookRotation(node.RightControl.normalized, up), 1, 0);
                node.RightControl = node.RightControl.normalized * newMag;
            }
        }

        var closestPointScreenPos = HandleUtility.WorldToGUIPoint(node.Position);
        var h = 220;
        closestPointScreenPos += new Vector2(0, -(h / 2) - 5);
        var style = GUI.skin.box;
        GUILayout.BeginArea(new Rect(closestPointScreenPos, new Vector2(150, h)), style);
        EditorGUILayout.BeginVertical(style);
        GUILayout.Label($"#{TargetSpline.Data.Nodes.IndexOf(node)}");
        var curveButton = EditorGUIUtility.IconContent("d_EditCollider");
        curveButton.text = $" Mode";

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(curveButton, GUILayout.MaxWidth(100));
        node.Mode = (eSplineControlMode)EditorGUILayout.EnumPopup(node.Mode);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Position", GUILayout.MaxWidth(100));
        EditorGUILayout.BeginVertical();
        node.Position.x = EditorGUILayout.FloatField(node.Position.x);
        node.Position.y = EditorGUILayout.FloatField(node.Position.y);
        node.Position.z = EditorGUILayout.FloatField(node.Position.z);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();


        EditorGUILayout.EndVertical();

        GUILayout.EndArea();

    }

}