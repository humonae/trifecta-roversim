using System.IO;
using UnityEditor;
using UnityEngine;

namespace Training.EditorTools
{
    // Creates the goal marker's material as an asset in a Resources folder, so builds include it
    // (and the transparent URP Unlit shader variant it needs). GoalMarkers picks it up by name.
    public static class GoalMarkerMaterialMenu
    {
        private const string Folder = "Assets/Resources";
        private static readonly string AssetPath = $"{Folder}/{GoalMarkers.ResourcesMaterialName}.mat";

        [MenuItem("Tools/RoverSim/Create Goal Marker Material")]
        public static void Create()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Goal Marker", "The URP Unlit shader wasn't found. Is the project using URP?", "OK");
                return;
            }

            if (!Directory.Exists(Folder))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetPath);
            bool isNew = material == null;
            if (isNew)
                material = new Material(shader);
            else
                material.shader = shader;

            GoalMarkers.MakeTransparent(material);
            material.SetColor("_BaseColor", Color.white);

            if (isNew)
                AssetDatabase.CreateAsset(material, AssetPath);
            else
                EditorUtility.SetDirty(material);

            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(material);
            Debug.Log($"[GoalMarkers] {(isNew ? "Created" : "Updated")} {AssetPath}.");
        }
    }
}
