#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[RequireComponent(typeof(Renderer))]
[AddComponentMenu("mikinel/AutoImageSetter/AutoImageSetterTarget")]
public class AutoImageSetterTarget : MonoBehaviour, VRC.SDKBase.IEditorOnly
{
    public bool customRatio = false;
    public Vector2 ratio = Vector2.one;
    
    private const float labelClipFar = 15f;

    public void SetTexture2D(Texture2D texture2D)
    {
        var renderer = GetComponent<Renderer>();
        
        // Undoの記録を開始する
        Undo.RecordObject(renderer.sharedMaterial, "Set Texture2D");

        // Textureを設定する
        renderer.sharedMaterial.mainTexture = texture2D;

        // シーンが変更されたことをUnityエディタに通知する
        EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
    }
    
    [ContextMenu("Create New Material")]
    public void CreateNewMaterial()
    {
        var renderer = GetComponent<Renderer>();
        
        // Undoの記録を開始する
        Undo.RecordObject(renderer, "Create New Material");

        // 現在設定されているMaterialのShaderを取得する
        var shader = renderer.sharedMaterial == null ? Shader.Find("Standard") : renderer.sharedMaterial.shader;

        // Materialを設定する
        var newMaterial = new Material(shader)
        {
            name = $"AutoImageSetterTarget_{gameObject.name}"
        };
        renderer.sharedMaterial = newMaterial;

        // シーンが変更されたことをUnityエディタに通知する
        EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        var position = transform.position + Vector3.up * 0.07f;

        //シーンカメラの取得
        var isActiveSceneCamera = false;
        var sceneCameraPos = Vector3.zero;
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            var sceneViewCamera = sceneView.camera;

            if (sceneViewCamera != null)
            {
                isActiveSceneCamera = true;
                sceneCameraPos = sceneViewCamera.transform.position;
            }
        }
        
        var centerPos = Multiplication(position, transform.localScale) + transform.position;
        if (isActiveSceneCamera && Vector3.Distance(sceneCameraPos, centerPos) < labelClipFar)
        {
            var style = new GUIStyle();
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, new Color(0, 0, 0, 0.5f));
            tex.Apply();
            style.normal.background = tex;
            style.normal.textColor = Color.cyan;
            style.alignment = TextAnchor.MiddleCenter;
        
            var ratioText = $"Ratio: {ratio.x}:{ratio.y}";
            Handles.Label(position, ratioText, style);   
        }
    }
    
    private static Vector3 Multiplication(Vector3 a, Vector3 b)
    {
        return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
    }
}
#endif