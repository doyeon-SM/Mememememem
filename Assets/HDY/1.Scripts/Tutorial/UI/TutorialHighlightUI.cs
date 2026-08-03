using UnityEngine;
using UnityEngine.UI;

namespace HDY.Tutorial
{
    /// <summary>
    /// 화면 전체를 반투명하게 어둡게 하고, 하이라이트 대상만 원형으로 오려내 원래 밝기로 보여주는
    /// 스포트라이트 연출. TutorialSpotlightMask 셰이더를 쓰는 Material이 연결된 UI Image 하나로
    /// 구현한다.
    ///
    /// [두 종류의 대상 지원]
    /// - 월드 오브젝트(Transform): TutorialSightDetector가 시야 감지로 찾은 오브젝트/멤/웨이포인트/상자.
    ///   Camera.WorldToViewportPoint로 화면 좌표를 구한다.
    /// - UI 요소(RectTransform): TutorialUIHighlightTarget으로 등록된 버튼/패널 등. 여신상/제작대/
    ///   탐험대/대장간 버튼처럼 "이 버튼을 눌러보세요" 안내에 쓴다. RectTransformUtility로 화면 좌표를
    ///   구한다.
    /// 두 값 다 같은 셰이더 파라미터(_Center/_Radius)로 변환해서 넘기기 때문에, 실제 화면에 보이는
    /// 효과는 완전히 동일하다.
    ///
    /// [Inspector 준비물 - 도연님 작업] 이 컴포넌트가 붙을 Image는 화면 전체를 덮도록 RectTransform
    /// 앵커를 (0,0)-(1,1) 풀스트레치로 설정해야 한다(그래야 Image의 uv가 곧 뷰포트 좌표와 일치함).
    /// Image의 Material 슬롯에는 셰이더 "HDY/Tutorial/SpotlightMask"를 사용하는 Material 에셋을
    /// 새로 만들어 연결해야 한다(Source Image는 비워도 됨).
    ///
    /// [씬 전환 대응 / 등록 유지] GameTimeTextBinder-TutorialDialogueUI와 동일하게, OnEnable에서
    /// TutorialManager에 자기 자신을 등록하고 OnDisable에서 해제한다. Hide() 시 오브젝트를
    /// SetActive(false)하지 않고 셰이더의 dim 알파값만 0으로 낮춰 숨긴다 - 예전에
    /// TutorialDialogueUI에서 자기 자신을 꺼서 등록이 풀렸던 것과 같은 문제를 처음부터 피하기 위함.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class TutorialHighlightUI : MonoBehaviour
    {
        [Tooltip("비워두면 자동 탐색(TutorialManager.Resolve).")]
        [SerializeField] private TutorialManager tutorialManager;

        [Tooltip("월드 오브젝트 강조 기준 카메라. 비워두면 Camera.main 사용.")]
        [SerializeField] private Camera targetCamera;

        [Header("연출 설정")]
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.65f;
        [SerializeField] private float minRadius = 0.08f;
        [SerializeField] private float maxRadius = 0.35f;
        [SerializeField] private float edgeSoftness = 0.05f;

        private Image image;
        private Material materialInstance;

        private Transform worldTarget;
        private RectTransform uiTarget;

        private readonly Vector3[] cornersBuffer = new Vector3[4];

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int CenterId = Shader.PropertyToID("_Center");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");

        private void Awake()
        {
            image = GetComponent<Image>();

            // Inspector에 연결된 Material 에셋을 그대로 쓰지 않고 인스턴스를 만들어 쓴다 -
            // 그래야 런타임에 값을 바꿔도 프로젝트에 저장된 원본 Material 에셋이 오염되지 않는다.
            materialInstance = image.material != null ? Instantiate(image.material) : null;
            if (materialInstance == null)
            {
                Debug.LogWarning("[TutorialHighlightUI] Image에 SpotlightMask Material이 연결되어 있지 않습니다.", this);
                return;
            }

            image.material = materialInstance;
            materialInstance.SetFloat(SoftnessId, edgeSoftness);
            ApplyAlpha(0f);
        }

        private void OnEnable()
        {
            tutorialManager = TutorialManager.Resolve(tutorialManager);
            if (tutorialManager == null)
            {
                Debug.LogWarning("[TutorialHighlightUI] TutorialManager를 찾을 수 없어 등록하지 못했습니다.", this);
                return;
            }

            tutorialManager.RegisterHighlightUI(this);
        }

        private void OnDisable()
        {
            tutorialManager?.UnregisterHighlightUI(this);
        }

        private void Update()
        {
            if (materialInstance == null) return;

            if (uiTarget != null)
            {
                UpdateForUITarget();
            }
            else if (worldTarget != null)
            {
                UpdateForWorldTarget();
            }
        }

        private void UpdateForWorldTarget()
        {
            var cam = ResolveCamera();
            if (cam == null) return;

            Vector3 viewportPos = cam.WorldToViewportPoint(worldTarget.position);
            if (viewportPos.z <= 0f)
            {
                // 대상이 카메라 뒤로 넘어감 - 다음 프레임에 다시 앞으로 오면 자동으로 복구됨.
                ApplyAlpha(0f);
                return;
            }

            materialInstance.SetVector(CenterId, new Vector4(viewportPos.x, viewportPos.y, 0f, 0f));
            materialInstance.SetFloat(RadiusId, ComputeWorldRadius(cam));
            ApplyAlpha(dimAlpha);
        }

        private void UpdateForUITarget()
        {
            var canvas = uiTarget.GetComponentInParent<Canvas>();
            Camera uiCamera = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera
                : null;

            Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, uiTarget.position);
            Vector2 viewportPos = new Vector2(screenPos.x / Screen.width, screenPos.y / Screen.height);

            uiTarget.GetWorldCorners(cornersBuffer);
            Vector3 cornerScreenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, cornersBuffer[2]); // 우상단 모서리
            float pixelRadius = Vector2.Distance(new Vector2(screenPos.x, screenPos.y), new Vector2(cornerScreenPos.x, cornerScreenPos.y));
            float viewportRadius = Mathf.Clamp(pixelRadius / Screen.height, minRadius, maxRadius);

            materialInstance.SetVector(CenterId, new Vector4(viewportPos.x, viewportPos.y, 0f, 0f));
            materialInstance.SetFloat(RadiusId, viewportRadius);
            ApplyAlpha(dimAlpha);
        }

        private Camera ResolveCamera()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            return targetCamera;
        }

        /// <summary>
        /// 대상의 Renderer 바운드를 화면에 투영해 반지름을 추정한다(카메라의 up 벡터 방향으로 재는
        /// 세로 뷰포트 거리라, 셰이더의 가로세로 비율 보정을 다시 거칠 필요가 없다). Renderer가
        /// 없으면 최소 반지름을 그대로 쓴다.
        /// </summary>
        private float ComputeWorldRadius(Camera cam)
        {
            if (worldTarget.TryGetComponent<Renderer>(out var renderer))
            {
                float worldRadius = renderer.bounds.extents.magnitude;
                Vector3 center = cam.WorldToViewportPoint(worldTarget.position);
                Vector3 edge = cam.WorldToViewportPoint(worldTarget.position + cam.transform.up * worldRadius);
                float viewportRadius = Mathf.Abs(edge.y - center.y);
                return Mathf.Clamp(viewportRadius, minRadius, maxRadius);
            }
            return minRadius;
        }

        private void ApplyAlpha(float alpha)
        {
            if (materialInstance == null) return;

            var color = materialInstance.GetColor(ColorId);
            color.a = alpha;
            materialInstance.SetColor(ColorId, color);
        }

        /// <summary>TutorialManager가 월드 오브젝트를 강조해야 할 때 호출한다.</summary>
        public void Show(Transform target)
        {
            worldTarget = target;
            uiTarget = null;
        }

        /// <summary>TutorialManager가 UI 요소(버튼 등)를 강조해야 할 때 호출한다.</summary>
        public void ShowUI(RectTransform target)
        {
            uiTarget = target;
            worldTarget = null;
        }

        /// <summary>TutorialManager가 강조를 그만해야 할 때 호출한다.</summary>
        public void Hide()
        {
            worldTarget = null;
            uiTarget = null;
            ApplyAlpha(0f);
        }
    }
}
