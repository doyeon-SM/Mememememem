using MemSystem.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMS
{
    /// <summary>
    /// 화면 오버레이에 멤 이름과 포획 확률을 표시하고 멤의 머리 위 위치를 추적합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KMSMemCaptureFocusView : MonoBehaviour
    {
        [Header("Style")]
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private TMP_FontAsset fontAsset;
        [SerializeField] private Color panelColor = new Color(1f, 1f, 1f, 0.94f);
        [SerializeField] private Color nameColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        [SerializeField] private Color messageColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color lowRateColor = new Color(0.93f, 0.38f, 0.16f, 1f);
        [SerializeField] private Color highRateColor = new Color(0.12f, 0.65f, 0.25f, 1f);

        [Header("Layout")]
        [SerializeField] private Vector2 panelSize = new Vector2(230f, 72f);
        [SerializeField, Min(0f)] private float worldYOffset = 0.28f;
        [SerializeField] private Vector2 diagonalScreenOffset = new Vector2(155f, 28f);
        [SerializeField, Min(0f)] private float sideSwitchDeadZone = 100f;
        [SerializeField, Min(0f)] private float followSmoothTime = 0.055f;
        [SerializeField, Min(0f)] private float screenMargin = 8f;
        [SerializeField] private int sortingOrder = 1100;

        private Canvas overlayCanvas;
        private RectTransform canvasRect;
        private RectTransform panelRect;
        private TMP_Text nameText;
        private TMP_Text detailText;
        private Mem target;
        private Renderer[] targetRenderers;
        private bool displayRequested;
        private bool hasPosition;
        private Vector2 currentPosition;
        private Vector2 positionVelocity;
        private bool hasPlacementSide;
        private int placementSide = 1;

        private void Awake()
        {
            EnsureUI();
            Hide();
        }

        private void LateUpdate()
        {
            if (!displayRequested || target == null || !target.IsActive)
            {
                SetPanelActive(false);
                return;
            }

            Camera activeCamera = Camera.main;
            if (activeCamera == null || canvasRect == null || panelRect == null)
            {
                SetPanelActive(false);
                return;
            }

            Vector3 screenPosition = activeCamera.WorldToScreenPoint(GetWorldAnchor());
            bool onScreen = screenPosition.z > 0f
                            && screenPosition.x >= -screenMargin
                            && screenPosition.x <= Screen.width + screenMargin
                            && screenPosition.y >= -screenMargin
                            && screenPosition.y <= Screen.height + screenMargin;
            if (!onScreen)
            {
                SetPanelActive(false);
                return;
            }

            SetPanelActive(true);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPosition,
                    null,
                    out Vector2 localPosition))
            {
                return;
            }

            UpdatePlacementSide(screenPosition.x);
            Vector2 diagonalPosition = localPosition + new Vector2(
                Mathf.Abs(diagonalScreenOffset.x) * placementSide,
                diagonalScreenOffset.y);
            diagonalPosition = ClampPanelToCanvas(diagonalPosition);

            if (!hasPosition || followSmoothTime <= 0f)
            {
                currentPosition = diagonalPosition;
                positionVelocity = Vector2.zero;
                hasPosition = true;
            }
            else
            {
                currentPosition = Vector2.SmoothDamp(
                    currentPosition,
                    diagonalPosition,
                    ref positionVelocity,
                    followSmoothTime,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);
            }

            panelRect.anchoredPosition = currentPosition;
        }

        public void ShowCaptureRate(Mem newTarget, string displayName, float captureRate)
        {
            SetTarget(newTarget);
            EnsureUI();

            if (nameText != null)
            {
                nameText.text = $"{GetTierMarker(newTarget)} {displayName}";
            }

            float normalizedRate = Mathf.Clamp01(captureRate);
            if (detailText != null)
            {
                detailText.text = $"포획 확률 {normalizedRate:P0}";
                detailText.color = Color.Lerp(lowRateColor, highRateColor, normalizedRate);
            }

            displayRequested = target != null;
        }

        public void ShowMessage(Mem newTarget, string displayName, string message)
        {
            SetTarget(newTarget);
            EnsureUI();

            if (nameText != null)
            {
                nameText.text = $"{GetTierMarker(newTarget)} {displayName}";
            }

            if (detailText != null)
            {
                detailText.text = message;
                detailText.color = messageColor;
            }

            displayRequested = target != null;
        }

        public void Hide()
        {
            displayRequested = false;
            target = null;
            targetRenderers = null;
            hasPosition = false;
            positionVelocity = Vector2.zero;
            hasPlacementSide = false;
            SetPanelActive(false);
        }

        private void SetTarget(Mem newTarget)
        {
            if (target == newTarget)
            {
                return;
            }

            target = newTarget;
            targetRenderers = target != null ? target.GetComponentsInChildren<Renderer>(true) : null;
            hasPosition = false;
            positionVelocity = Vector2.zero;
            hasPlacementSide = false;
        }

        private void UpdatePlacementSide(float anchorScreenX)
        {
            float screenCenterX = Screen.width * 0.5f;
            float horizontalOffset = Mathf.Abs(diagonalScreenOffset.x);
            float halfPanelWidth = panelSize.x * 0.5f;
            float requiredRoom = horizontalOffset + halfPanelWidth + screenMargin;

            // 화면 가장자리에서는 패널이 화면 안쪽을 향하도록 우선 배치합니다.
            if (anchorScreenX + requiredRoom > Screen.width)
            {
                placementSide = -1;
                hasPlacementSide = true;
                return;
            }

            if (anchorScreenX - requiredRoom < 0f)
            {
                placementSide = 1;
                hasPlacementSide = true;
                return;
            }

            // 화면 중앙 부근에서 카메라가 조금 흔들릴 때 좌우가 계속 뒤집히지 않도록
            // 데드존을 두고, 대상을 새로 잡았을 때는 여유 공간이 더 많은 쪽을 선택합니다.
            if (!hasPlacementSide)
            {
                placementSide = anchorScreenX <= screenCenterX ? 1 : -1;
                hasPlacementSide = true;
                return;
            }

            if (placementSide > 0 && anchorScreenX > screenCenterX + sideSwitchDeadZone)
            {
                placementSide = -1;
            }
            else if (placementSide < 0 && anchorScreenX < screenCenterX - sideSwitchDeadZone)
            {
                placementSide = 1;
            }
        }

        private Vector2 ClampPanelToCanvas(Vector2 position)
        {
            Rect canvasBounds = canvasRect.rect;
            Rect panelBounds = panelRect.rect;
            Vector2 pivot = panelRect.pivot;

            float minX = canvasBounds.xMin + panelBounds.width * pivot.x + screenMargin;
            float maxX = canvasBounds.xMax - panelBounds.width * (1f - pivot.x) - screenMargin;
            float minY = canvasBounds.yMin + panelBounds.height * pivot.y + screenMargin;
            float maxY = canvasBounds.yMax - panelBounds.height * (1f - pivot.y) - screenMargin;

            if (minX <= maxX)
            {
                position.x = Mathf.Clamp(position.x, minX, maxX);
            }

            if (minY <= maxY)
            {
                position.y = Mathf.Clamp(position.y, minY, maxY);
            }

            return position;
        }

        private static string GetTierMarker(Mem mem)
        {
            if (mem == null) return string.Empty;

            int grade = Mathf.Clamp((int)mem.Tier + 1, 1, 5);
            return grade switch
            {
                1 => "①",
                2 => "②",
                3 => "③",
                4 => "④",
                _ => "⑤"
            };
        }

        private Vector3 GetWorldAnchor()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                return target.transform.position + Vector3.up * (1.5f + worldYOffset);
            }

            bool hasBounds = false;
            Bounds combinedBounds = default;
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer targetRenderer = targetRenderers[i];
                if (targetRenderer == null || !targetRenderer.enabled || !targetRenderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = targetRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(targetRenderer.bounds);
                }
            }

            if (!hasBounds)
            {
                return target.transform.position + Vector3.up * (1.5f + worldYOffset);
            }

            Vector3 anchor = combinedBounds.center;
            anchor.y = combinedBounds.max.y + worldYOffset;
            return anchor;
        }

        private void EnsureUI()
        {
            if (overlayCanvas != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject(
                "KMS Mem Capture Focus Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            overlayCanvas = canvasObject.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasRect = canvasObject.GetComponent<RectTransform>();

            GameObject panelObject = new GameObject(
                "Mem Capture Focus Panel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            panelObject.transform.SetParent(canvasObject.transform, false);

            panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.sizeDelta = panelSize;

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.sprite = backgroundSprite;
            panelImage.color = panelColor;
            panelImage.raycastTarget = false;
            panelImage.type = backgroundSprite != null && backgroundSprite.border.sqrMagnitude > 0f
                ? Image.Type.Sliced
                : Image.Type.Simple;

            Outline outline = panelObject.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.28f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;

            nameText = CreateLabel("Mem Name", panelRect, new Vector2(0f, 0.48f), Vector2.one, 20f);
            detailText = CreateLabel("Capture Rate", panelRect, Vector2.zero, new Vector2(1f, 0.56f), 18f);
        }

        private TMP_Text CreateLabel(
            string objectName,
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            float fontSize)
        {
            GameObject labelObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = anchorMin;
            labelRect.anchorMax = anchorMax;
            labelRect.offsetMin = new Vector2(10f, 2f);
            labelRect.offsetMax = new Vector2(-10f, -2f);

            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            if (fontAsset != null)
            {
                label.font = fontAsset;
            }

            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = nameColor;
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = fontSize;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private void SetPanelActive(bool active)
        {
            if (panelRect != null && panelRect.gameObject.activeSelf != active)
            {
                panelRect.gameObject.SetActive(active);
            }
        }
    }
}
