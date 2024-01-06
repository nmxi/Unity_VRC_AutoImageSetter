using System;
using ImageMagick;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace mikinel.vrc.AutoImageSetter.Editor
{
    public class ImageCropperWindow : EditorWindow
    {
        [SerializeField] private VisualTreeAsset _imageCropperUxml;

        // 編集する画像
        public Texture2D targetImage;
        private Texture2D _lastTargetImage;

        // 編集設定
        public bool hideSettings = false; // 設定項目を非表示にするかどうか
        public bool isAdjustRatio = true; // アスペクト比を保持するかどうか
        public float cropRatioWidth = 1;
        public float cropRatioHeight = 1;

        // 一時的に保持する変数
        private Vector2 _currentRawImageSize; // 現在の画像サイズ
        private Rect _currentWindowRect; // 現在のウィンドウサイズ
        private Rect _lastWindowRect;
        private Rect _currentDrawRect; // 現在の画像の描画領域
        private float _drawTextureAdjustX; // 画像の描画領域の余白サイズ
        private Vector2 _initialMousePosition; // 範囲選択の開始位置
        private Vector2 _lastMousePosition; // 選択範囲の移動開始時のマウス位置
        private Vector2 _scrollPosition; // ScrollView のスクロール位置
        private Rect _selectionRect; // 範囲選択の矩形領域
        private float _imageScale = 1f; // 画像のスケール管理用変数
        private float _fitScale = 1f; // 画像をウィンドウにフィットさせるためのスケール
        private bool _imageSelected = false; // 編集する画像が選択されたかどうかを追跡するフラグ
        private bool _imageUpdated = false; // 画像が更新されたかどうかを追跡するフラグ
        private bool _isSelecting; // 範囲選択中かどうか
        private bool _isMovingSelection = false; // 選択範囲を移動中かどうか

        // コールバック
        private Action<Texture2D> onCropImage;

        // 固定値
        private static float MINIMUM_IMAGE_SCALE = 0.01f;
        private static float MAXIMUM_IMAGE_SCALE = 3f;
        private static float IMAGE_SCALE_STEP = 0.05f;
        private static readonly Color GRID_COLOR = new(0.5f, 0.5f, 0.5f, 0.25f);
        private static readonly Color SELECTION_AREA_GRID_COLOR = new(1f, 0.92f, 0f, 0.5f);
        private static readonly Color SELECTION_AREA_TEXT_COLOR = new(1f, 1f, 1f, 1f);
        private static readonly Color SELECTION_AREA_TEXT_BG_COLOR = new(0.5f, 0.5f, 0.5f, 0.7f);

        private Label _currentImageSizeLabel;
        private Button _centerSelectionButton;
        private Button _maximizeSelectionButton;
        private Button _cropImageButton;
        private Button _fitScaleButton;
        private Button _zoomInButton;
        private Button _zoomOutButton;

        [MenuItem("MikinelTools/Image Cropper")]
        public static void ShowWindow()
        {
            ImageMagickUtility.InitializeImageMagick();
            GetWindow<ImageCropperWindow>("Image Cropper", true);

            // ウィンドウのサイズを設定
            var window = GetWindow<ImageCropperWindow>("Image Cropper");

            window.minSize = new Vector2(810, 720);

            window.DrawGUI();
        }

        public static void ShowWindow(Texture2D targetImage, bool isAdjustRatio, float cropRatioWidth,
            float cropRatioHeight,
            Action<Texture2D> onCropImage, bool hideSettings = false)
        {
            if (targetImage == null)
            {
                Debug.LogError("targetImage is null.");
                return;
            }

            ImageMagickUtility.InitializeImageMagick();
            GetWindow<ImageCropperWindow>("Image Cropper", true);

            // ウィンドウのサイズを設定
            var window = GetWindow<ImageCropperWindow>("Image Cropper");

            window.minSize = new Vector2(810, 620);

            window.targetImage = targetImage;
            window.isAdjustRatio = isAdjustRatio;
            window.cropRatioWidth = cropRatioWidth;
            window.cropRatioHeight = cropRatioHeight;
            window.onCropImage = onCropImage;

            window.hideSettings = hideSettings;

            window.DrawGUI();
        }

        public static void CloseWindow()
        {
            var window = GetWindow<ImageCropperWindow>("Image Cropper");
            window.Close();
        }

        private void DrawGUI()
        {
            _imageCropperUxml.CloneTree(rootVisualElement);

            var settingsArea = rootVisualElement.Q<VisualElement>("SettingsArea");
            var editArea = rootVisualElement.Q<VisualElement>("EditArea");

            settingsArea.Q<VisualElement>("CropSettings").style.display =
                hideSettings ? DisplayStyle.None : DisplayStyle.Flex;

            settingsArea.Q<VisualElement>("TargetImage").Q<IMGUIContainer>().onGUIHandler = () =>
            {
                targetImage =
                    (Texture2D)EditorGUILayout.ObjectField("TargetImage", targetImage, typeof(Texture2D), false);
            };

            //AdjustRatioToggle
            var adjustRatioToggle = settingsArea.Q<Toggle>("AdjustRatio");
            adjustRatioToggle.RegisterValueChangedCallback((e) => { isAdjustRatio = e.newValue; });
            adjustRatioToggle.value = isAdjustRatio;

            //RatioWidth
            var ratioWidthField = settingsArea.Q<FloatField>("Width");
            ratioWidthField.RegisterValueChangedCallback((e) => { cropRatioWidth = e.newValue; });
            ratioWidthField.value = cropRatioWidth;

            //RatioHeight
            var ratioHeightField = settingsArea.Q<FloatField>("Height");
            ratioHeightField.RegisterValueChangedCallback((e) => { cropRatioHeight = e.newValue; });
            ratioHeightField.value = cropRatioHeight;

            //CenterSelectionButton
            _centerSelectionButton = settingsArea.Q<Button>("CenterSelection");
            _centerSelectionButton.RegisterCallback<MouseUpEvent>((e) => { CenterSelection(); });

            //MaximizeSelectionButton
            _maximizeSelectionButton = settingsArea.Q<Button>("MaximizeSelection");
            _maximizeSelectionButton.RegisterCallback<MouseUpEvent>((e) => { MaximizeSelection(); });

            //CropImageButton
            _cropImageButton = settingsArea.Q<Button>("CropImage");
            _cropImageButton.RegisterCallback<MouseUpEvent>((e) =>
            {
                CropImage(AssetDatabase.GetAssetPath(targetImage), _selectionRect.x, _selectionRect.y,
                    _selectionRect.width,
                    _selectionRect.height);
            });

            //CurrentImageSize
            _currentImageSizeLabel = settingsArea.Q<Label>("CurrentImageSize");

            //FitButton
            _fitScaleButton = settingsArea.Q<Button>("Fit");
            _fitScaleButton.RegisterCallback<MouseUpEvent>((e) => { AutoDrawRectFitting(); });

            //ZoomInButton
            _zoomInButton = settingsArea.Q<Button>("ZoomIn");
            _zoomInButton.RegisterCallback<MouseUpEvent>((e) =>
            {
                _imageScale = Mathf.Min(MAXIMUM_IMAGE_SCALE, _imageScale + IMAGE_SCALE_STEP);
                ResetSelection();
                _imageUpdated = true; // 画像が更新されたことを示す
            });

            //ZoomOutButton
            _zoomOutButton = settingsArea.Q<Button>("ZoomOut");
            _zoomOutButton.RegisterCallback<MouseUpEvent>((e) =>
            {
                _imageScale = Mathf.Max(MINIMUM_IMAGE_SCALE, _imageScale - IMAGE_SCALE_STEP);
                ResetSelection();
                _imageUpdated = true; // 画像が更新されたことを示す
            });

            //EditArea
            var editAreaImguiContainer = editArea.Q<IMGUIContainer>();
            editAreaImguiContainer.onGUIHandler = () =>
            {
                // ウィンドウのサイズが変更された場合、選択をリセット
                if (_lastWindowRect.size != position.size)
                {
                    ResetSelection();
                    _lastWindowRect = position;

                    _currentWindowRect = position;
                }

                //画像が変更された場合、選択をリセット
                if (_lastTargetImage != targetImage)
                {
                    _imageSelected = true;
                    _lastTargetImage = targetImage;
                }

                if (targetImage != null)
                {
                    if (_imageUpdated)
                    {
                        UpdateCurrentImageSize();
                        _imageUpdated = false;
                    }

                    if (_imageSelected)
                    {
                        UpdateCurrentImageSize();
                        AutoDrawRectFitting();
                        _imageSelected = false;
                    }

                    _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

                    // スケーリングされた画像の表示
                    var scaledWidth = _currentRawImageSize.x * _imageScale;
                    var scaledHeight = _currentRawImageSize.y * _imageScale;
                    _currentDrawRect = GUILayoutUtility.GetRect(scaledWidth, scaledHeight);

                    //現在のWindowのサイズと画像のサイズを比較して、左右の余白のサイズを計算
                    _drawTextureAdjustX = (position.width - scaledWidth) / 2f;

                    _currentDrawRect.x += _drawTextureAdjustX;

                    _currentDrawRect.width = scaledWidth;
                    _currentDrawRect.height = scaledHeight;
                    GUI.DrawTexture(_currentDrawRect, targetImage);

                    // 画像の中心に白いグリッド線を描画
                    DrawCenterGrid(_currentDrawRect, scaledWidth, scaledHeight);

                    HandleSelection(_currentDrawRect);

                    if (_isSelecting || _selectionRect.size != Vector2.zero)
                    {
                        DrawSelectionRect();
                    }

                    EditorGUILayout.EndScrollView();
                }
                else
                {
                    EditorGUILayout.HelpBox("Select an image to crop.", MessageType.Info);
                    _imageSelected = true;
                }
            };
        }

        private void OnGUI()
        {
            var disableSelectionControlButtons =
                targetImage == null || _selectionRect.size.x <= 0 || _selectionRect.size.y <= 0;
            _centerSelectionButton.SetEnabled(!disableSelectionControlButtons);
            _cropImageButton.SetEnabled(!disableSelectionControlButtons);

            _currentImageSizeLabel.text = targetImage != null
                ? $"{_currentRawImageSize.x}px x {_currentRawImageSize.y}px"
                : "0px x 0px";
            _fitScaleButton.style.display = _imageScale != _fitScale ? DisplayStyle.Flex : DisplayStyle.None;
            _zoomOutButton.style.display = targetImage != null && _imageScale > MINIMUM_IMAGE_SCALE
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _zoomInButton.style.display = targetImage != null && _imageScale < MAXIMUM_IMAGE_SCALE
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        /// <summary>
        /// 選択範囲を画像の中央に配置
        /// </summary>
        private void CenterSelection()
        {
            if (targetImage == null || _selectionRect.size == Vector2.zero)
            {
                return;
            }

            // 画像の表示されている中心点を計算
            var centerX = (_currentRawImageSize.x * _imageScale) / 2f + _drawTextureAdjustX;
            var centerY = (_currentRawImageSize.y * _imageScale) / 2f;

            _selectionRect.x = centerX - _selectionRect.width / 2f;
            _selectionRect.y = centerY - _selectionRect.height / 2f;

            Repaint();
        }

        /// <summary>
        /// 選択範囲を画像のサイズに合わせて最大化
        /// </summary>
        private void MaximizeSelection()
        {
            if (targetImage == null)
            {
                return;
            }

            // 画像のアスペクト比と選択範囲のアスペクト比を計算
            var imageAspectRatio = _currentRawImageSize.x / _currentRawImageSize.y;

            // 現在の選択範囲のアスペクト比を計算
            float selectionAspectRatio;
            if (isAdjustRatio)
            {
                selectionAspectRatio = cropRatioWidth / cropRatioHeight;
            }
            else
            {
                selectionAspectRatio = _selectionRect.width / _selectionRect.height;
            }

            // 画像のスケール後のサイズを計算
            var scaledImageWidth = _currentRawImageSize.x * _imageScale;
            var scaledImageHeight = _currentRawImageSize.y * _imageScale;

            float width, height;
            if (selectionAspectRatio > imageAspectRatio)
            {
                // 画像の幅に合わせて選択範囲の高さを調整
                width = scaledImageWidth;
                height = width / selectionAspectRatio;
            }
            else
            {
                // 画像の高さに合わせて選択範囲の幅を調整
                height = scaledImageHeight;
                width = height * selectionAspectRatio;
            }

            // 選択範囲を画像の中央に配置
            var centerX = (_currentRawImageSize.x * _imageScale) / 2f + _drawTextureAdjustX;
            var centerY = (_currentRawImageSize.y * _imageScale) / 2f;

            _selectionRect = new Rect(centerX - width / 2f, centerY - height / 2f, width, height);

            Repaint();
        }

        /// <summary>
        /// 画像のサイズを再取得する
        /// </summary>
        private void UpdateCurrentImageSize()
        {
            var imageFilePath = AssetDatabase.GetAssetPath(targetImage);
            var imageSize = ImageMagickUtility.GetImageSize(imageFilePath);

            if (imageSize.x <= 0 || imageSize.y <= 0)
            {
                imageSize = Vector2.zero;
            }

            _currentRawImageSize = imageSize;
        }

        /// <summary>
        /// 画像をウィンドウのサイズに合わせる
        /// </summary>
        private void AutoDrawRectFitting()
        {
            // ウィンドウの幅と画像の幅に基づいてスケールを計算
            var widthScale = (_currentWindowRect.width - 20) / _currentRawImageSize.x;

            // ウィンドウの高さとUIの高さを考慮して、高さに基づいてスケールを計算
            var heightScale = (rootVisualElement.layout.height - 20) / _currentRawImageSize.y;

            // 幅と高さのスケールのうち、小さい方を採用
            _imageScale = Mathf.Min(widthScale, heightScale);
            _fitScale = _imageScale;

            // 選択範囲をリセット
            ResetSelection();
        }

        /// <summary>
        /// 範囲選択の処理
        /// </summary>
        /// <param name="imageRect">画像の表示範囲</param>
        private void HandleSelection(Rect imageRect)
        {
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                imageRect.Contains(Event.current.mousePosition))
            {
                _isSelecting = true;
                _initialMousePosition = Event.current.mousePosition;
                _selectionRect = new Rect(Event.current.mousePosition, Vector2.zero);
            }
            else if (Event.current.type == EventType.MouseDown && Event.current.button == 1 &&
                     _selectionRect.Contains(Event.current.mousePosition))
            {
                _isMovingSelection = true;
                _lastMousePosition = Event.current.mousePosition;
            }

            if (_isSelecting)
            {
                if (Event.current.type == EventType.MouseDrag)
                {
                    var currentMousePosition = Event.current.mousePosition;

                    // カーソル位置を画像の境界内にクランプ
                    ClampPositionToImage(ref currentMousePosition, imageRect);

                    var rectX = Mathf.Min(_initialMousePosition.x, currentMousePosition.x);
                    var rectY = Mathf.Min(_initialMousePosition.y, currentMousePosition.y);
                    var width = Mathf.Abs(currentMousePosition.x - _initialMousePosition.x);
                    var height = Mathf.Abs(currentMousePosition.y - _initialMousePosition.y);

                    if (isAdjustRatio)
                    {
                        var aspectRatio = cropRatioWidth / cropRatioHeight;
                        if (width / height > aspectRatio)
                        {
                            // 高さを幅に合わせて調整
                            height = width / aspectRatio;
                            if (currentMousePosition.y < _initialMousePosition.y)
                            {
                                rectY = _initialMousePosition.y - height;
                            }
                        }
                        else
                        {
                            // 幅を高さに合わせて調整
                            width = height * aspectRatio;
                            if (currentMousePosition.x < _initialMousePosition.x)
                            {
                                rectX = _initialMousePosition.x - width;
                            }
                        }
                    }

                    // 選択範囲が画像の境界外に出た場合、選択範囲を画像の境界内にクランプ
                    if (rectY < imageRect.y)
                    {
                        // 上に出た場合
                        height = _initialMousePosition.y - imageRect.y;
                        if (isAdjustRatio)
                        {
                            width = height * (cropRatioWidth / cropRatioHeight);
                        }

                        if (_initialMousePosition.x < currentMousePosition.x)
                        {
                            rectX = _initialMousePosition.x;
                        }
                        else
                        {
                            rectX = _initialMousePosition.x - width;
                        }

                        if (_initialMousePosition.y < currentMousePosition.y)
                        {
                            rectY = _initialMousePosition.y;
                        }
                        else
                        {
                            rectY = _initialMousePosition.y - height;
                        }
                    }
                    else if (rectX < imageRect.x)
                    {
                        // 左に出た場合
                        width = _initialMousePosition.x - imageRect.x;
                        if (isAdjustRatio)
                        {
                            height = width * (cropRatioHeight / cropRatioWidth);
                        }

                        if (_initialMousePosition.x < currentMousePosition.x)
                        {
                            rectX = _initialMousePosition.x;
                        }
                        else
                        {
                            rectX = _initialMousePosition.x - width;
                        }

                        if (_initialMousePosition.y < currentMousePosition.y)
                        {
                            rectY = _initialMousePosition.y;
                        }
                        else
                        {
                            rectY = _initialMousePosition.y - height;
                        }
                    }
                    else if (rectX + width > imageRect.x + imageRect.width)
                    {
                        // 右に出た場合
                        width = imageRect.x + imageRect.width - rectX;
                        if (isAdjustRatio)
                        {
                            height = width * (cropRatioHeight / cropRatioWidth);
                        }

                        if (_initialMousePosition.x < currentMousePosition.x)
                        {
                            rectX = _initialMousePosition.x;
                        }
                        else
                        {
                            rectX = _initialMousePosition.x - width;
                        }

                        if (_initialMousePosition.y < currentMousePosition.y)
                        {
                            rectY = _initialMousePosition.y;
                        }
                        else
                        {
                            rectY = _initialMousePosition.y - height;
                        }
                    }
                    else if (rectY + height > imageRect.y + imageRect.height)
                    {
                        // 下に出た場合
                        height = imageRect.y + imageRect.height - rectY;
                        if (isAdjustRatio)
                        {
                            width = height * (cropRatioWidth / cropRatioHeight);
                        }

                        if (_initialMousePosition.x < currentMousePosition.x)
                        {
                            rectX = _initialMousePosition.x;
                        }
                        else
                        {
                            rectX = _initialMousePosition.x - width;
                        }
                    }

                    _selectionRect = new Rect(rectX, rectY, width, height);
                    Repaint();
                }
                else if (Event.current.type == EventType.MouseUp)
                {
                    _isSelecting = false;
                }
            }
            else if (_isMovingSelection)
            {
                if (Event.current.type == EventType.MouseDrag && Event.current.button == 1)
                {
                    var delta = Event.current.mousePosition - _lastMousePosition;
                    _selectionRect.position += delta;
                    ClampSelectionToImageRect(ref _selectionRect, imageRect);
                    _lastMousePosition = Event.current.mousePosition;
                    Repaint();
                }
                else if (Event.current.type == EventType.MouseUp && Event.current.button == 1)
                {
                    _isMovingSelection = false;
                }
            }
        }

        /// <summary>
        /// 選択範囲が画像の外に出ないようにクランプ
        /// </summary>
        /// <param name="selection">選択範囲</param>
        /// <param name="imageRect">画像の表示範囲</param>
        private static void ClampSelectionToImageRect(ref Rect selection, Rect imageRect)
        {
            var xMax = imageRect.x + imageRect.width - selection.width;
            var yMax = imageRect.y + imageRect.height - selection.height;
            selection.x = Mathf.Clamp(selection.x, imageRect.x, xMax);
            selection.y = Mathf.Clamp(selection.y, imageRect.y, yMax);
        }

        /// <summary>
        /// カーソル位置を画像の境界内にクランプ
        /// </summary>
        /// <param name="cursorPosition">カーソル位置</param>
        /// <param name="imageRect">画像の表示範囲</param>
        private static void ClampPositionToImage(ref Vector2 cursorPosition, Rect imageRect)
        {
            cursorPosition.x = Mathf.Clamp(cursorPosition.x, imageRect.x, imageRect.x + imageRect.width);
            cursorPosition.y = Mathf.Clamp(cursorPosition.y, imageRect.y, imageRect.y + imageRect.height);
        }

        /// <summary>
        /// 選択範囲のグリッドを描画
        /// </summary>
        private void DrawSelectionRect()
        {
            EditorGUI.DrawRect(_selectionRect, new Color(0.5f, 0.5f, 0.5f, 0.25f));
            Handles.color = SELECTION_AREA_GRID_COLOR;

            //DrawWireCubeの上辺が1px見切れるので、1px分下にずらす
            var selectionRectWithOffset = new Rect(_selectionRect.x, _selectionRect.y + 1, _selectionRect.width,
                _selectionRect.height);
            Handles.DrawWireCube(selectionRectWithOffset.center, selectionRectWithOffset.size);

            // 選択範囲に十字線を描画
            Handles.DrawLine(new Vector2(_selectionRect.x, _selectionRect.y + _selectionRect.height / 2),
                new Vector2(_selectionRect.x + _selectionRect.width, _selectionRect.y + _selectionRect.height / 2));
            Handles.DrawLine(new Vector2(_selectionRect.x + _selectionRect.width / 2, _selectionRect.y),
                new Vector2(_selectionRect.x + _selectionRect.width / 2, _selectionRect.y + _selectionRect.height));

            var scale = 1f / _imageScale;
            var scaledSelectionWidth = _selectionRect.width * scale;
            var scaledSelectionHeight = _selectionRect.height * scale;
            if (scaledSelectionWidth >= 120 && scaledSelectionHeight >= 40)
            {
                var defaultColor = GUI.color;
                if (isAdjustRatio)
                {
                    // 比率を選択範囲の左上に表示
                    // 小数点以下があれば2桁まで表示、なければ整数のみ表示
                    var cropRatioWidthStr = cropRatioWidth % 1 == 0 ? $"{cropRatioWidth:f0}" : $"{cropRatioWidth:f2}";
                    var cropRatioHeightStr =
                        cropRatioHeight % 1 == 0 ? $"{cropRatioHeight:f0}" : $"{cropRatioHeight:f2}";
                    var ratioText = $"{cropRatioWidthStr} : {cropRatioHeightStr}";

                    var ratioTextSize = EditorStyles.label.CalcSize(new GUIContent(ratioText));
                    var ratioTextRect = new Rect(_selectionRect.x, _selectionRect.y, ratioTextSize.x, ratioTextSize.y);
                    EditorGUI.DrawRect(ratioTextRect, SELECTION_AREA_TEXT_BG_COLOR);

                    GUI.color = SELECTION_AREA_TEXT_COLOR;
                    EditorGUI.LabelField(ratioTextRect, ratioText, EditorStyles.label);
                    GUI.color = defaultColor;
                }

                // 選択範囲のサイズを選択範囲の右下に表示
                var sizeText = $"{scaledSelectionWidth:f0} x {scaledSelectionHeight:f0}";
                var sizeTextSize = EditorStyles.label.CalcSize(new GUIContent(sizeText));
                var sizeTextRect = new Rect(_selectionRect.x + _selectionRect.width - sizeTextSize.x,
                    _selectionRect.y + _selectionRect.height - sizeTextSize.y, sizeTextSize.x, sizeTextSize.y);
                EditorGUI.DrawRect(sizeTextRect, SELECTION_AREA_TEXT_BG_COLOR);

                defaultColor = GUI.color;
                GUI.color = SELECTION_AREA_TEXT_COLOR;
                EditorGUI.LabelField(sizeTextRect, sizeText, EditorStyles.label);
                GUI.color = defaultColor;
            }
        }

        /// <summary>
        /// 選択範囲をリセット
        /// </summary>
        private void ResetSelection()
        {
            _isSelecting = false;
            _selectionRect = Rect.zero;
        }

        /// <summary>
        /// 画像の上に十字線を描画
        /// </summary>
        private void DrawCenterGrid(Rect imageRect, float scaledWidth, float scaledHeight)
        {
            GUI.color = GRID_COLOR;
            var imageX = imageRect.x + (imageRect.width - scaledWidth) / 2;
            var imageY = imageRect.y + (imageRect.height - scaledHeight) / 2;
            GUI.DrawTexture(new Rect(imageX + scaledWidth / 2, imageY, 1, scaledHeight), EditorGUIUtility.whiteTexture);
            GUI.DrawTexture(new Rect(imageX, imageY + scaledHeight / 2, scaledWidth, 1), EditorGUIUtility.whiteTexture);
            GUI.color = GRID_COLOR;
        }

        /// <summary>
        /// 画像をトリミングして保存
        /// </summary>
        /// <param name="imagePath">画像のパス</param>
        private void CropImage(string imagePath, float x, float y, float width, float height)
        {
            try
            {
                var scale = 1f / _imageScale;
                x = (x - _drawTextureAdjustX) * scale;
                y *= scale;
                width *= scale;
                height *= scale;

                using (var magickImage = new MagickImage(imagePath))
                {
                    var magickRectangle = new MagickGeometry((int)x, (int)y, (int)width, (int)height)
                    {
                        IgnoreAspectRatio = isAdjustRatio
                    };

                    magickImage.Crop(magickRectangle);
                    magickImage.RePage();
                    magickImage.Write(imagePath);
                    AssetDatabase.Refresh();

                    //Reload size
                    UpdateCurrentImageSize();

                    //reset selectionRect
                    ResetSelection();

                    //auto draw rect fitting
                    AutoDrawRectFitting();
                }

                Debug.Log("Image cropped and saved successfully.");

                onCropImage?.Invoke(targetImage);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error cropping image: {ex.Message}");

                // ダイアログを表示
                var errorMessage = $"対応していない画像形式です\n{ex.Message}";
                EditorUtility.DisplayDialog("Error", errorMessage, "OK");
            }
        }
    }
}