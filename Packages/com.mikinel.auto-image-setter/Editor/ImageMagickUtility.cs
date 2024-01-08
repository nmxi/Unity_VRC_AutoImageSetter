using ImageMagick;
using UnityEditor;
using UnityEngine;

namespace mikinel.vrc.AutoImageSetter.Editor
{
    public static class ImageMagickUtility
    {
        private static readonly string dllName = "Magick.Native-Q8-x64.dll";
        private static readonly string imageMagickPath = "Packages/com.mikinel.auto-image-setter/ImageMagick/Plugins/";

        public static void InitializeImageMagick()
        {
            //DataPathの1つ上の階層にMagick.Native-Q8-x64.dllがあるかを確認
            var dllPath = $"{Application.dataPath}/../{dllName}";
            var isExist = System.IO.File.Exists(dllPath);

            //存在しなかったら、Magick.Native-Q8-x64.dllをコピー
            if (isExist)
            {
                return;
            }
        
            var sourcePath = $"{imageMagickPath}/{dllName}";
            var destinationPath = $"{Application.dataPath}/../{dllName}";
            System.IO.File.Copy(sourcePath, destinationPath);
            
            Debug.Log("ImageMagick Initialized");
        }
    
        public static Vector2 GetImageSize(string path)
        {
            try
            {
                using var imageFile = new MagickImage(path);
                return new Vector2(imageFile.Width, imageFile.Height);   
            }
            catch(MagickException e)
            {
                Debug.LogError(e.Message);
            
                // ダイアログを表示
                var errorMessage = $"対応していない画像形式です\n{path}\n{e.Message}";
                EditorUtility.DisplayDialog("Error", errorMessage, "OK");

                return new Vector2(0, 0);
            }
        }

        public static void CropImage(string imagePath, int x, int y, int width, int height, bool isAdjustRatio)
        {
            using var magickImage = new MagickImage(imagePath);
            var magickRectangle = new MagickGeometry(x, y, width, height)
            {
                IgnoreAspectRatio = isAdjustRatio
            };

            magickImage.Crop(magickRectangle);
            magickImage.RePage();
            magickImage.Write(imagePath);
            AssetDatabase.Refresh();
        }
    }   
}