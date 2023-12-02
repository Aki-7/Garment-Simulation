using UnityEditor;
using GarmentSimulator;
using UnityEngine;

[CustomEditor(typeof(GSGarment))]
public class GSGarmentEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var garment = (GSGarment)target;

        serializedObject.Update();

        if (GUILayout.Button("Reset Garment")) {
            garment.ResetPosition();
        }

        EditorGUILayout.LabelField("Blend Shapes", EditorStyles.boldLabel);

        if (garment != null && garment.ready)
        {
            for (int i = 0; i < garment.blendShapeNames.Length; i ++) {
                var name = garment.blendShapeNames[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(name);
                var nextValue = EditorGUILayout.Slider(garment.GetBlendShapeWeight(i), 0, 100);
                if (nextValue != garment.GetBlendShapeWeight(i)) {
                    garment.SetBlendShapeWeight(i, nextValue);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
