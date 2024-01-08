using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace mikinel.vrc.AutoImageSetter.Editor
{
    public static class AutoImageSetterUtility
    {
        private static readonly string importPath = "Assets/__AutoImageSetter";
        private static readonly string[] imageExtensions = { "png", "jpg", "jpeg" };

        private static readonly string pathSaveKey = "AutoImageSetterUtility_LastPath";

        public static void OpenFilePanelAndImportImageAndSet(AutoImageSetterTarget target)
        {
            var path = OpenFilePanel();
            if (!string.IsNullOrEmpty(path))
            {
                ImportImageAndSet(path, target);
            }
        }
        
        public static string OpenFilePanel()
        {
            // 前回のパスを取得、なければマイドキュメントを指定
            var lastPath = EditorPrefs.GetString(pathSaveKey, System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments));
            var path = EditorUtility.OpenFilePanel("Select Image", lastPath, string.Join(",", imageExtensions));

            if (!string.IsNullOrEmpty(path))
            {
                var directory = Path.GetDirectoryName(path);
                EditorPrefs.SetString(pathSaveKey, directory);
            }

            return path;
        }
        
        public static void ImportImageAndSet(string path, AutoImageSetterTarget target)
        {
            var fileName = Path.GetFileName(path);

            // 拡張子が画像ファイルでなければエラー
            if (!imageExtensions.Any(ext => fileName.EndsWith((string)ext)))
            {
                Debug.LogError("Selected file is not an image.");
                return;
            }

            // __AutoImageSetterフォルダがなければ作成
            if (!AssetDatabase.IsValidFolder(importPath))
            {
                AssetDatabase.CreateFolder("Assets", "__AutoImageSetter");
            }

            var importedFilePath = $"{importPath}/{fileName}";
            File.Copy(path, importedFilePath, overwrite: true);

            AssetDatabase.ImportAsset(importedFilePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            // 画像の比率を取得
            var imageRatio = GetImageRatio(importedFilePath);
            var targetRatio = target.ratio.x / target.ratio.y;
            var targetTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(importedFilePath);
            if (imageRatio != targetRatio)
            {
                // Debug.LogError($"Image ratio is not matched : {imageRatio} != {targetRatio}");

                // 比率が一致しない場合はダイアログを表示
                // ダイアログにはトリミングをするかどうかの選択肢を表示
                var result = EditorUtility.DisplayDialogComplex(
                    "Image Ratio is not matched",
                    $"画像の比率が一致しません\n\n" +
                    $"画像の比率 : {imageRatio}\n" +
                    $"ターゲットの比率 : {targetRatio}",
                    "トリミング", "キャンセル", "そのまま適用");

                switch (result)
                {
                    case 0:
                        // trim
                        ImageCropperWindow.ShowWindow(targetTexture, true, target.ratio.x, target.ratio.y,
                            croppedTexture =>
                            {
                                SetTexture2D(croppedTexture, target);
                                ImageCropperWindow.CloseWindow();
                            }, true);

                        break;

                    case 1:
                        // cancel
                        return;

                    case 2:
                        // ignore
                        SetTexture2D(targetTexture, target);

                        break;
                }
            }
            else
            {
                SetTexture2D(targetTexture, target);
            }
        }

        private static void SetTexture2D(Texture2D texture2D, AutoImageSetterTarget target)
        {
            if (texture2D != null && target != null)
            {
                target.SetTexture2D(texture2D);
            }

            //set dirty
            EditorUtility.SetDirty(target);
        }

        private static float GetImageRatio(string path)
        {
            var size = ImageMagickUtility.GetImageSize(path);
            return size.x / size.y;
        }
    }
}