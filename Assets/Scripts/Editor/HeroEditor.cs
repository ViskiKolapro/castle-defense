#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Hero))]
public class HeroEditor : Editor
{
    static readonly string[] HiddenEvolutionVisualFields =
    {
        "evolution1ReadySprite", "evolution1AimSprite", "evolution1ReleaseSprite",
        "evolution2ReadySprite", "evolution2AimSprite", "evolution2ProjectilePrefab",
        "victoriaRedEvolution1ReadySprite", "victoriaRedEvolution1AimSprite",
        "victoriaRedEvolution2ReadySprite", "victoriaRedEvolution2AimSprite",
        "victoriaBlueEvolution1ReadySprite", "victoriaBlueEvolution1AimSprite",
        "victoriaBlueEvolution2ReadySprite", "victoriaBlueEvolution2AimSprite"
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, HiddenEvolutionVisualFields);

        var heroName = serializedObject.FindProperty("heroName")?.stringValue ?? "";
        EditorGUILayout.Space(6);

        if (heroName == "Bow Master")
        {
            EditorGUILayout.LabelField("Evolution I Visuals (Bow Master)", EditorStyles.boldLabel);
            Draw("evolution1ReadySprite", "Evolution 1 Ready Sprite");
            Draw("evolution1AimSprite", "Evolution 1 Aim Sprite");
            Draw("evolution1ReleaseSprite", "Evolution 1 Release Sprite");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Evolution II Physical Visuals (Bow Master)", EditorStyles.boldLabel);
            Draw("evolution2ReadySprite", "Evolution 2 Ready Sprite");
            Draw("evolution2AimSprite", "Evolution 2 Aim Sprite");
            Draw("evolution2ProjectilePrefab", "Evolution 2 Projectile Prefab");
        }
        else if (heroName == "Victoria")
        {
            EditorGUILayout.LabelField("Victoria Evolution Visuals", EditorStyles.boldLabel);
            Draw("victoriaRedEvolution1ReadySprite", "Red Elf Evolution I Ready");
            Draw("victoriaRedEvolution1AimSprite", "Red Elf Evolution I Aim");
            Draw("victoriaRedEvolution2ReadySprite", "Red Elf Evolution II Ready");
            Draw("victoriaRedEvolution2AimSprite", "Red Elf Evolution II Aim");
            Draw("victoriaBlueEvolution1ReadySprite", "Blue Elf Evolution I Ready");
            Draw("victoriaBlueEvolution1AimSprite", "Blue Elf Evolution I Aim");
            Draw("victoriaBlueEvolution2ReadySprite", "Blue Elf Evolution II Ready");
            Draw("victoriaBlueEvolution2AimSprite", "Blue Elf Evolution II Aim");
        }

        serializedObject.ApplyModifiedProperties();
    }

    void Draw(string propertyName, string label)
    {
        var p = serializedObject.FindProperty(propertyName);
        if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label));
    }
}
#endif
