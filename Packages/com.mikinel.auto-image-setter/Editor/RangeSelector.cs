using UnityEngine;

namespace mikinel.vrc.AutoImageSetter.Editor
{
    public class RangeSelector
    {
        /// <summary>
        /// 範囲選択中かどうか
        /// </summary>
        public bool IsSelecting { get; private set; }

        /// <summary>
        /// 選択範囲を移動中かどうか
        /// </summary>
        public bool IsMovingSelection { get; private set; }

        /// <summary>
        /// アスペクト比を保持するかどうか
        /// </summary>
        public bool IsAdjustRatio { get; set; }

        /// <summary>
        /// 選択範囲の比率の（横幅）
        /// </summary>
        public float CropRatioWidth { get; set; } = 1;

        /// <summary>
        /// 選択範囲の比率の（高さ）
        /// </summary>
        public float CropRatioHeight { get; set; } = 1;

        /// <summary>
        /// 範囲選択の矩形領域を描画できるかどうか
        /// </summary>
        public bool CanDrawSelectionRect => IsSelecting || _selectionRect.size.x > 0 || _selectionRect.size.y > 0;

        /// <summary>
        /// 再描画が必要かどうか
        /// </summary>
        public bool NeedRepaint => (IsSelecting || IsMovingSelection) && Event.current.type == EventType.MouseDrag;

        /// <summary>
        /// 範囲選択の矩形領域
        /// </summary>
        public Rect SelectionRect => _selectionRect;

        Vector2 _initialMousePosition; // 範囲選択の開始位置
        Vector2 _lastMousePosition; // 選択範囲の移動開始時のマウス位置
        Rect _selectionRect; // 範囲選択の矩形領域

        /// <summary>
        /// 範囲選択の処理
        /// </summary>
        /// <param name="imageRect">画像の表示範囲</param>
        public void HandleSelection(Rect imageRect)
        {
            // 範囲選択/範囲選択移動を検出
            if (Event.current.type == EventType.MouseDown && imageRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 0)
                {
                    IsSelecting = true;
                    _initialMousePosition = Event.current.mousePosition;
                    _selectionRect = new Rect(Event.current.mousePosition, Vector2.zero);
                }
                else if (Event.current.button == 1)
                {
                    IsMovingSelection = true;
                    _lastMousePosition = Event.current.mousePosition;
                }
            }

            // 選択範囲を更新
            if (IsSelecting)
            {
                if (Event.current.type == EventType.MouseDrag)
                {
                    var currentMousePosition = Event.current.mousePosition;

                    // カーソル位置を画像の境界内にクランプ
                    ClampPositionToImage(ref currentMousePosition, imageRect);

                    var rectPosition = new Vector2(
                        Mathf.Min(_initialMousePosition.x, currentMousePosition.x),
                        Mathf.Min(_initialMousePosition.y, currentMousePosition.y)
                    );
                    var width = Mathf.Abs(currentMousePosition.x - _initialMousePosition.x);
                    var height = Mathf.Abs(currentMousePosition.y - _initialMousePosition.y);

                    if (IsAdjustRatio)
                    {
                        var aspectRatio = CropRatioWidth / CropRatioHeight;
                        if (width / height > aspectRatio)
                        {
                            // 高さを幅に合わせて調整
                            height = width / aspectRatio;
                            if (currentMousePosition.y < _initialMousePosition.y)
                            {
                                rectPosition.y = _initialMousePosition.y - height;
                            }
                        }
                        else
                        {
                            // 幅を高さに合わせて調整
                            width = height * aspectRatio;
                            if (currentMousePosition.x < _initialMousePosition.x)
                            {
                                rectPosition.x = _initialMousePosition.x - width;
                            }
                        }
                    }

                    // 選択範囲が画像の境界外に出た場合、選択範囲を画像の境界内にクランプ
                    if (rectPosition.y < imageRect.y)
                    {
                        // 上に出た場合
                        height = _initialMousePosition.y - imageRect.y;
                        if (IsAdjustRatio)
                        {
                            width = height * (CropRatioWidth / CropRatioHeight);
                        }
                    }
                    else if (rectPosition.x < imageRect.x)
                    {
                        // 左に出た場合
                        width = _initialMousePosition.x - imageRect.x;
                        if (IsAdjustRatio)
                        {
                            height = width * (CropRatioHeight / CropRatioWidth);
                        }
                    }
                    else if (rectPosition.x + width > imageRect.x + imageRect.width)
                    {
                        // 右に出た場合
                        width = imageRect.x + imageRect.width - rectPosition.x;
                        if (IsAdjustRatio)
                        {
                            height = width * (CropRatioHeight / CropRatioWidth);
                        }
                    }
                    else if (rectPosition.y + height > imageRect.y + imageRect.height)
                    {
                        // 下に出た場合
                        height = imageRect.y + imageRect.height - rectPosition.y;
                        if (IsAdjustRatio)
                        {
                            width = height * (CropRatioWidth / CropRatioHeight);
                        }
                    }

                    rectPosition = AdjustRectPosition(_initialMousePosition, currentMousePosition, new Vector2(width, height));

                    _selectionRect = new Rect(rectPosition.x, rectPosition.y, width, height);
                }
                else if (Event.current.type == EventType.MouseUp)
                {
                    IsSelecting = false;
                }
            }
            else if (IsMovingSelection)
            {
                if (Event.current.type == EventType.MouseDrag && Event.current.button == 1)
                {
                    var delta = Event.current.mousePosition - _lastMousePosition;
                    _selectionRect.position += delta;
                    ClampSelectionToImageRect(ref _selectionRect, imageRect);
                    _lastMousePosition = Event.current.mousePosition;
                }
                else if (Event.current.type == EventType.MouseUp && Event.current.button == 1)
                {
                    IsMovingSelection = false;
                }
            }
        }

        /// <summary>
        /// 選択範囲をリセットする。
        /// </summary>
        public void ResetSelection()
        {
            IsSelecting = false;
            _selectionRect = Rect.zero;
        }

        /// <summary>
        /// 選択範囲を変更する。
        /// </summary>
        /// <param name="rect">選択範囲</param>
        public void ChangeSelection(Rect rect)
        {
            _selectionRect = rect;
        }

        /// <summary>
        /// 選択範囲の位置を変更する。
        /// </summary>
        /// <param name="x">選択範囲の位置x</param>
        /// <param name="y">選択範囲の位置y</param>
        public void ChangeSelectionPosition(float x, float y)
        {
            _selectionRect.x = x;
            _selectionRect.y = y;
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
        /// 選択範囲の位置を補正する。
        /// </summary>
        /// <param name="initialPosition">範囲選択の開始位置</param>
        /// <param name="currentPosition">現在のマウス位置</param>
        /// <param name="size">選択範囲のサイズ</param>
        /// <returns>補正後の位置</returns>
        private static Vector2 AdjustRectPosition(Vector2 initialPosition, Vector2 currentPosition, Vector2 size)
        {
            var result = Vector2.zero;
            if (initialPosition.x < currentPosition.x)
            {
                result.x = initialPosition.x;
            }
            else
            {
                result.x = initialPosition.x - size.x;
            }

            if (initialPosition.y < currentPosition.y)
            {
                result.y = initialPosition.y;
            }
            else
            {
                result.y = initialPosition.y - size.y;
            }

            return result;
        }
    }
}