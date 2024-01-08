using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace mikinel.vrc.AutoImageSetter.Editor
{
    public class AutoImageSetterTargetExplorer : EditorWindow
    {
        private Vector2 scrollPosition;
        private List<AutoImageSetterTarget> targets;

        private static Texture2D _customGreenTexture;

        private static Texture2D GetCustomGreenTexture()
        {
            if (_customGreenTexture == null)
            {
                _customGreenTexture = CreateTexture2D(new Color(0.22f, 0.35f, 0f));
            }

            return _customGreenTexture;
        }

        [MenuItem("MikinelTools/AutoImageSetterTargetExplorer")]
        public static void ShowWindow()
        {
            GetWindow<AutoImageSetterTargetExplorer>("AutoImageSetterTargetExplorer");
        }

        private void OnFocus()
        {
            RefreshTargets();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField($"Targets: {targets?.Count ?? 0}");

            if (GUILayout.Button("Refresh List"))
            {
                RefreshTargets();
            }

            EditorGUILayout.EndHorizontal();

            if (targets == null)
            {
                EditorGUILayout.HelpBox("No AutoImageSetterTargets found.", MessageType.Info);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var target in targets)
            {
                if (target != null)
                {
                    var elementStyle = new GUIStyle(GUI.skin.box);
                    var isSelecting = Selection.activeGameObject == target.gameObject;
                    elementStyle.normal.background =
                        isSelecting ? GetCustomGreenTexture() : elementStyle.normal.background;

                    EditorGUILayout.BeginHorizontal(elementStyle);

                    //Focus
                    if (GUILayout.Button("Select", GUILayout.Width(50)))
                    {
                        SelectAndFocus(target.gameObject);
                    }

                    EditorGUILayout.LabelField(target.name);

                    //SetImage
                    if (GUILayout.Button("SetImageToNewMat", GUILayout.Width(140)))
                    {
                        target.CreateNewMaterial();
                        AutoImageSetterUtility.OpenFilePanelAndImportImageAndSet(target);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void RefreshTargets()
        {
            targets = new List<AutoImageSetterTarget>(FindObjectsOfType<AutoImageSetterTarget>());
        }

        private static void SelectAndFocus(GameObject gameObject)
        {
            Selection.activeObject = gameObject;

            if (SceneView.sceneViews.Count < 1)
                return;

            var sceneView = (SceneView)SceneView.sceneViews[0];
            sceneView.LookAt(gameObject.transform.position, sceneView.rotation, 1f);
        }

        private static Texture2D CreateTexture2D(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();

            return texture;
        }
    }
}