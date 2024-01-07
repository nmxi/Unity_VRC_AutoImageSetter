using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace mikinel.vrc.AutoImageSetter.Editor
{
    [CustomEditor(typeof(AutoImageSetterTarget))]
    public class AutoImageSetterTargetEditor : UnityEditor.Editor
    {
        private bool _lastCustomRatio = false;

        private readonly Dictionary<string, Vector2> COMMON_RATIOS = new()
        {
            { "1:1", new Vector2(1, 1) },
            { "3:2", new Vector2(3, 2) },
            { "2:3", new Vector2(2, 3) },
            { "5:4", new Vector2(5, 4) },
            { "4:5", new Vector2(4, 5) },
            { "4:3", new Vector2(4, 3) },
            { "3:4", new Vector2(3, 4) },
            { "16:9", new Vector2(16, 9) },
            { "9:16", new Vector2(9, 16) },
            { "16:10", new Vector2(16, 10) },
            { "10:16", new Vector2(10, 16) },
        };

        public override void OnInspectorGUI()
        {
            var target = (AutoImageSetterTarget)serializedObject.targetObject;

            EditorGUI.BeginChangeCheck();

            target.customRatio = EditorGUILayout.Toggle("Custom Ratio", target.customRatio);
            if (target.customRatio)
            {
                var newRatio = EditorGUILayout.Vector2Field("Custom Ratio", target.ratio);
                SetRatio(newRatio);

                _lastCustomRatio = true;
            }
            else
            {
                if (_lastCustomRatio)
                {
                    if (!COMMON_RATIOS.TryGetValue($"{target.ratio.x}:{target.ratio.y}", out var ratio))
                    {
                        ratio = COMMON_RATIOS["16:9"];
                    }

                    SetRatio(ratio);

                    _lastCustomRatio = false;
                }

                var options = new string[COMMON_RATIOS.Count];
                COMMON_RATIOS.Keys.CopyTo(options, 0);
                var currentSelection = GetCurrentSelection(target.ratio);
                var selection = EditorGUILayout.Popup("Aspect Ratio", currentSelection, options);

                if (selection >= 0 && selection < options.Length)
                {
                    var key = options[selection];
                    target.ratio = COMMON_RATIOS[key];
                }
            }

            if (GUILayout.Button("Set Image"))
            {
                var path = AutoImageSetterUtility.OpenFilePanel();
                if (!string.IsNullOrEmpty(path))
                {
                    AutoImageSetterUtility.ImportImageAndSet(path, target);
                }
            }

            EditorGUI.EndDisabledGroup();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
            }
        }

        private void SetRatio(Vector2 ratio)
        {
            var target = (AutoImageSetterTarget)serializedObject.targetObject;
            if (target.ratio == ratio)
            {
                return;
            }

            target.ratio = ratio;
            EditorUtility.SetDirty(target);

            Undo.RegisterFullObjectHierarchyUndo(target, "Change Ratio");
        }

        private int GetCurrentSelection(Vector2 ratio)
        {
            foreach (var pair in COMMON_RATIOS)
            {
                if (pair.Value == ratio)
                    return new List<string>(COMMON_RATIOS.Keys).IndexOf(pair.Key);
            }

            return -1; // Custom Ratio
        }
    }
}