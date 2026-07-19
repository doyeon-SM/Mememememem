using System.Collections.Generic;
using UnityEngine;

namespace HDY.Mem
{
    /// <summary>
    /// MemData.modelPrefab(3D 모델)을 런타임에 촬영해서 아이콘(Sprite)으로 만들어주는 렌더러.
    ///
    /// [리소스 최소화] 카메라/RenderTexture를 프로젝트 에셋 파일로 만들지 않고 전부 코드에서 런타임에
    /// 생성한다(새 에셋 없음). 촬영 결과(Sprite)는 memId별로 한 번만 렌더링해서 메모리에 캐싱하고,
    /// 이후 같은 멤을 다시 요청하면 캐시에서 즉시 반환한다(재렌더링 없음).
    ///
    /// [촬영 위치] 별도 Layer를 추가하는 대신, 월드 좌표상 아주 멀리 떨어진 지점(shootingStagePosition)에서
    /// 촬영한다 - 프로젝트 설정(Tags and Layers) 변경이 필요 없고, 다른 오브젝트와 겹칠 일도 없다.
    ///
    /// [카메라 프레이밍 - 월드 바운드 계산] Renderer.bounds(월드 공간)는 SkinnedMeshRenderer의 경우 스키닝/
    /// 애니메이션이 실제로 갱신된 뒤에야 정확해지는데, 이 클래스는 Instantiate 직후 같은 프레임 안에서 바로
    /// 촬영하기 때문에(아래 [동기 처리] 참고) bounds가 아직 갱신되지 않아 발밑 근처의 아주 작은 값으로 잘못
    /// 계산될 수 있다(실제로 이 문제로 카메라가 발끝 근처에 붙어 위를 올려다보는 각도로 찍힌 적이 있었다).
    /// 그래서 Renderer.bounds 대신 Renderer.localBounds(메시 원본 기준 로컬 바운드, 애니메이션 상태와 무관하게
    /// 항상 안정적)를 각 Renderer의 transform으로 직접 월드로 변환해서 합산하는 방식을 쓴다.
    ///
    /// [카메라 프레이밍 - 높이 기준점] 계산된 월드 바운드의 기하학적 중심이 아니라, "발끝(bounds.min.y) 기준으로
    /// 전체 키의 verticalFocusRatio 비율만큼 올라간 높이"를 카메라가 바라보도록 한다. 기본값 0.55는 사람 기준
    /// 배-가슴 사이 높이 정도이며, 모델 비율에 따라 인스펙터에서 조정할 수 있다.
    ///
    /// [카메라 프레이밍 - 정면 촬영] 촬영용 인스턴스는 항상 Quaternion.identity로 생성되므로, 모델의 로컬
    /// 정면(+Z)이 곧 월드 +Z가 된다. 카메라 오프셋은 이 +Z 정면 방향을 기준으로, cameraAzimuthDegrees(오른쪽
    /// 회전)와 cameraElevationDegrees(위로 들어올리는 각도)만큼 구면 좌표로 회전시켜 계산한다. 두 각도가 모두
    /// 0이면 정면에서 바라보는 것과 같고, 값을 조절하면 거리(카메라~focusPoint)는 그대로 유지한 채 카메라
    /// 위치만 오른쪽/위쪽으로 돌아간다.
    ///
    /// [촬영용 조명 - 역광 방지] 씬의 Directional Light는 카메라 위치와 무관하게 항상 고정된 방향에서만
    /// 비추기 때문에, 위 정면 촬영으로 카메라를 돌리고 나면 모델 앞면이 역광으로 어둡게 나오는 문제가
    /// 있었다. 그래서 카메라와 같은 위치를 따라다니는 촬영 전용 Point 라이트(iconLight)를 별도로 두어,
    /// 씬의 메인 라이트와 무관하게 항상 카메라가 보는 방향에서 밝게 비추도록 한다. 이 라이트는 카메라
    /// 오브젝트의 자식이라 FrameCameraToBounds()가 매 촬영마다 카메라를 옮겨도 자동으로 따라간다.
    /// range는 촬영 거리 기준으로 좁게 제한해서, shootingStagePosition 주변에서만 빛이 미치고 원점 부근의
    /// 실제 게임 씬까지는 새어나가지 않도록 한다.
    ///
    /// [동기 처리] Camera.Render()는 카메라의 enabled 여부와 무관하게 즉시 강제 렌더링을 수행하므로,
    /// Instantiate -> 프레이밍 -> Render -> ReadPixels -> Destroy를 코루틴 없이 한 메서드 안에서 끝낸다.
    /// 다만 이 방식은 Start()가 실행되기 전에(같은 프레임 내에서) 촬영이 끝나므로, modelPrefab의 외형이
    /// Start()에서 스크립트로 결정되는 경우 그 부분은 반영되지 않는다(대부분의 정적 메시 모델은 문제 없음).
    ///
    /// [빌드에서만 아이콘이 노이즈로 깨지는 문제 - 수정됨] 에디터는 모든 셰이더가 이미 컴파일되어 있지만,
    /// 빌드에서는 특정 멤 모델의 셰이더가 "처음 그려지는 순간" 드라이버가 그 자리에서 셰이더 변형을
    /// 컴파일하는 경우가 있다. 이 클래스는 멤창고 그리드가 열릴 때 수십~수백 개의 서로 다른 멤을 순식간에
    /// 연속으로 촬영하는데, 그 "처음 그려지는 프레임"이 아직 준비되지 않은 상태로 캡처되면 알록달록한
    /// 노이즈로 찍히고, 이 결과가 영구 캐싱되어 계속 그렇게 보이는 문제가 있었다. 그래서 같은 프레이밍으로
    /// 두 번 렌더링하되 첫 번째 결과는 버리고(셰이더/드라이버 예열용) 두 번째 결과만 실제로 캡처한다.
    /// 추가로 Camera.Render() 직후 GL.Flush()를 호출해 GPU 커맨드가 실제로 끝났음을 보장한다.
    ///
    /// [부작용 방지] 촬영용 임시 인스턴스는 Collider를 끄고 Rigidbody를 kinematic으로 돌려서, 물리
    /// 충돌/낙하 등 부작용 없이 순수 비주얼만 담당하도록 한다(GridManager의 건물 미리보기 인스턴스와 같은 방식).
    /// </summary>
    public class MemIconRenderer : MonoBehaviour
    {
        public static MemIconRenderer Instance { get; private set; }

        [Header("데이터 참조")]
        [SerializeField] private MemCatalogManager catalogManager;

        [Header("촬영 설정")]
        [Tooltip("생성되는 아이콘 텍스처의 가로/세로 픽셀 크기(정사각형)")]
        [SerializeField] private int iconResolution = 128;
        [Tooltip("모델을 촬영하는 월드 좌표. 씬의 다른 콘텐츠와 겹치지 않도록 아주 멀리 떨어진 곳으로 둔다.")]
        [SerializeField] private Vector3 shootingStagePosition = new Vector3(10000f, 10000f, 10000f);
        [Tooltip("자동 프레이밍 시 모델 경계에 주는 여유 배율(1보다 크면 모델 주변에 약간 여백이 생김)")]
        [SerializeField] private float cameraFitPadding = 1.2f;
        [Tooltip("카메라가 바라볼 높이 비율(0 = 발끝, 1 = 정수리, 0.5 = 키의 정중앙). 기본 0.55는 배-가슴 사이 높이 정도.")]
        [Range(0f, 1f)]
        [SerializeField] private float verticalFocusRatio = 0.55f;

        [Header("촬영 각도 설정")]
        [Tooltip("정면(0도)을 기준으로 카메라를 오른쪽으로 회전시키는 각도. 거리는 그대로 유지된 채 위치만 돈다.")]
        [SerializeField] private float cameraAzimuthDegrees = 30f;
        [Tooltip("눈높이(0도)를 기준으로 카메라를 위로 들어올리는 각도. 90에 가까울수록 위에서 내려다보는 구도가 된다.")]
        [SerializeField] private float cameraElevationDegrees = 45f;

        [Header("촬영용 조명 설정")]
        [Tooltip("아이콘 촬영용 키라이트의 밝기. 씬의 Directional Light와는 무관하게 이 값으로만 밝기가 정해진다.")]
        [SerializeField] private float iconLightIntensity = 3f;
        [Tooltip("아이콘 촬영용 키라이트의 색상")]
        [SerializeField] private Color iconLightColor = Color.white;

        private Camera iconCamera;
        private Light iconLight;
        private RenderTexture iconRenderTexture;
        private readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[MemIconRenderer] 씬에 MemIconRenderer가 이미 있어 중복 오브젝트를 파괴합니다.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;

            catalogManager = MemCatalogManager.Resolve(catalogManager);

            SetupIconCamera();
        }

        /// <summary>촬영 전용 카메라 + RenderTexture를 코드로만 생성한다(프로젝트에 새 에셋 파일 없음).</summary>
        private void SetupIconCamera()
        {
            var cameraObject = new GameObject("MemIconRenderCamera");
            cameraObject.transform.SetParent(transform, false);
            // 정면(+Z)에서 촬영 지점을 바라보는 기본 자세. 실제 촬영 직전에는 항상 FrameCameraToBounds()가
            // 모델 크기 및 cameraAzimuthDegrees/cameraElevationDegrees에 맞춰 위치를 다시 계산하므로,
            // 여기 값은 초기 기본 자세일 뿐이다.
            cameraObject.transform.position = shootingStagePosition + new Vector3(0f, 0f, 5f);
            cameraObject.transform.LookAt(shootingStagePosition);

            iconCamera = cameraObject.AddComponent<Camera>();
            iconCamera.orthographic = true;
            iconCamera.clearFlags = CameraClearFlags.SolidColor;
            iconCamera.backgroundColor = new Color(0f, 0f, 0f, 0f); // 완전 투명 배경
            iconCamera.cullingMask = ~0; // 전체 레이어를 보되, 촬영 위치가 멀리 떨어져 있어 다른 오브젝트와 겹치지 않는다.
            iconCamera.nearClipPlane = 0.01f;
            iconCamera.enabled = false; // 자동 매 프레임 렌더링을 막고, Render()를 직접 호출할 때만 그린다.

            CreateIconRenderTexture();
            SetupIconLight(cameraObject);
        }

        /// <summary>
        /// 카메라와 같은 위치에서 항상 카메라가 보는 쪽을 밝게 비추는 촬영 전용 키라이트를 만든다.
        /// 씬의 Directional Light는 위치와 무관하게 고정된 방향에서만 비추기 때문에, 카메라를 정면으로
        /// 돌리면 모델 앞면이 역광으로 어둡게 나오는 문제가 있었다. 이 라이트는 카메라 오브젝트의 자식으로
        /// 붙어 있어 FrameCameraToBounds()가 매 촬영마다 카메라 위치를 옮겨도 자동으로 같은 위치를
        /// 따라가며, Point 라이트라 방향과 무관하게 주변(=카메라가 보는 모델)을 비춘다.
        /// [메인 씬에 영향 없음] range를 FrameCameraToBounds()에서 촬영 거리 기준으로 좁게 제한해서,
        /// shootingStagePosition 주변에서만 빛이 미치고 원점 부근의 실제 게임 씬까지는 닿지 않게 한다.
        /// </summary>
        private void SetupIconLight(GameObject cameraObject)
        {
            iconLight = cameraObject.AddComponent<Light>();
            iconLight.type = LightType.Point;
            iconLight.color = iconLightColor;
            iconLight.intensity = iconLightIntensity;
            iconLight.shadows = LightShadows.None; // 촬영용 단일 라이트라 그림자는 끄고 밝기를 안정적으로 유지
            iconLight.cullingMask = ~0;
        }

        /// <summary>RenderTexture를 생성한다. 빌드에서 드물게 생성이 실패하는 경우를 대비해 별도 메서드로
        /// 분리하고, RenderIcon()에서 매번 IsCreated()를 확인해 필요하면 다시 생성하도록 한다.</summary>
        private void CreateIconRenderTexture()
        {
            iconRenderTexture = new RenderTexture(iconResolution, iconResolution, 16, RenderTextureFormat.ARGB32)
            {
                name = "MemIconRenderTexture",
                antiAliasing = 1
            };
            iconRenderTexture.Create();

            iconCamera.targetTexture = iconRenderTexture;
        }

        /// <summary>
        /// memId에 해당하는 아이콘을 반환한다. 처음 요청되면 modelPrefab을 촬영해서 캐싱하고,
        /// 이후에는 캐시에서 즉시 반환한다. MemData를 찾을 수 없거나 modelPrefab이 비어있으면 null을 반환한다
        /// (호출 쪽에서 아이콘 영역을 감추는 등 안전하게 폴백 처리하면 된다).
        /// </summary>
        public Sprite GetIcon(string memId)
        {
            if (string.IsNullOrEmpty(memId)) return null;

            if (iconCache.TryGetValue(memId, out var cachedSprite))
            {
                return cachedSprite;
            }

            var memData = catalogManager != null ? catalogManager.FindMemData(memId) : null;
            if (memData == null || memData.modelPrefab == null)
            {
                return null;
            }

            var sprite = RenderIcon(memData.modelPrefab);
            if (sprite != null)
            {
                iconCache[memId] = sprite;
            }

            return sprite;
        }

        /// <summary>modelPrefab을 촬영 위치에 임시로 세워두고 찍은 뒤 바로 정리한다.
        /// [빌드 노이즈 방지] 같은 프레이밍으로 두 번 렌더링한다 - 첫 번째는 셰이더/드라이버 예열용으로
        /// 버리고, 두 번째 결과만 실제로 ReadPixels로 캡처한다. 빌드에서 특정 멤의 셰이더가 처음 그려지는
        /// 순간 드라이버가 그 자리에서 컴파일하며 한 프레임 깨지는 경우가 있는데, 이 방식이면 그 깨진
        /// 프레임은 버려지고 캐싱되지 않는다.</summary>
        private Sprite RenderIcon(GameObject modelPrefab)
        {
            if (iconRenderTexture == null || !iconRenderTexture.IsCreated())
            {
                Debug.LogWarning("[MemIconRenderer] iconRenderTexture가 준비되지 않아 다시 생성합니다.", this);
                CreateIconRenderTexture();
            }

            var instance = Instantiate(modelPrefab, shootingStagePosition, Quaternion.identity);

            DisablePhysicsSideEffects(instance);
            FrameCameraToBounds(instance);

            // 1차 렌더(예열) - 결과는 버린다. 셰이더 변형이 이 시점에 컴파일되어도 캡처되지 않는다.
            iconCamera.Render();
            GL.Flush();

            // 2차 렌더(실제 캡처) - 이제는 셰이더가 준비된 상태로 그려진다.
            iconCamera.Render();
            GL.Flush();

            var texture = new Texture2D(iconResolution, iconResolution, TextureFormat.RGBA32, false);

            var previousActive = RenderTexture.active;
            RenderTexture.active = iconRenderTexture;
            texture.ReadPixels(new Rect(0, 0, iconResolution, iconResolution), 0, 0);
            texture.Apply();
            RenderTexture.active = previousActive;

            Destroy(instance);

            return Sprite.Create(texture, new Rect(0, 0, iconResolution, iconResolution), new Vector2(0.5f, 0.5f));
        }

        /// <summary>촬영용 임시 인스턴스가 물리적으로 부작용(낙하, 충돌 등)을 일으키지 않도록 막는다.</summary>
        private void DisablePhysicsSideEffects(GameObject instance)
        {
            foreach (var col in instance.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }

            foreach (var rb in instance.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
        }

        /// <summary>
        /// 안정적인 월드 바운드(CalculateWorldBounds)를 계산해서, 카메라가 "발끝 기준 verticalFocusRatio
        /// 높이"의 지점을 cameraAzimuthDegrees/cameraElevationDegrees 각도로 바라보도록 위치/Orthographic
        /// Size를 맞춘다. 촬영용 키라이트(iconLight)도 카메라와 같은 위치를 따라가므로, 여기서 모델 크기에
        /// 맞춰 조명 range도 함께 갱신한다.
        /// </summary>
        private void FrameCameraToBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"[MemIconRenderer] '{instance.name}'에서 Renderer를 찾을 수 없어 기본 프레이밍으로 촬영합니다.", this);
                return;
            }

            Bounds bounds = CalculateWorldBounds(renderers);

            float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z, 0.01f);
            iconCamera.orthographicSize = maxExtent * cameraFitPadding;

            // 카메라가 바라볼 지점: 발끝(bounds.min.y)에서 전체 키(bounds.size.y)의 verticalFocusRatio만큼 올라간 높이.
            float focusHeight = bounds.min.y + bounds.size.y * verticalFocusRatio;
            Vector3 focusPoint = new Vector3(bounds.center.x, focusHeight, bounds.center.z);

            // [정면 기준 구면 좌표 회전] 인스턴스는 Quaternion.identity로 생성되어 로컬 정면(+Z)이 곧 월드
            // +Z이다. 회전이 전혀 없을 때(0, 0)의 오프셋은 정면(+Z)이고, cameraAzimuthDegrees만큼 오른쪽
            // (+X 쪽)으로, cameraElevationDegrees만큼 위(+Y)로 들어올린다. distance(카메라~focusPoint 거리)는
            // 각도와 무관하게 항상 동일하게 유지된다 - 방향 벡터를 단위 벡터로 만든 뒤 distance를 곱하기 때문.
            float azimuthRad = cameraAzimuthDegrees * Mathf.Deg2Rad;
            float elevationRad = cameraElevationDegrees * Mathf.Deg2Rad;
            Vector3 offsetDirection = new Vector3(
                Mathf.Sin(azimuthRad) * Mathf.Cos(elevationRad),
                Mathf.Sin(elevationRad),
                Mathf.Cos(azimuthRad) * Mathf.Cos(elevationRad));

            float cameraDistance = maxExtent * 4f + 1f;
            Vector3 cameraOffset = offsetDirection * cameraDistance;
            iconCamera.transform.position = focusPoint + cameraOffset;
            iconCamera.transform.LookAt(focusPoint);

            // [조명 range 갱신] iconLight는 카메라의 자식이라 위치는 자동으로 따라가지만, 모델 크기에 따라
            // 카메라~모델 거리가 달라지므로 range는 매번 다시 계산해야 한다. 다만 shootingStagePosition이
            // 원점에서 아주 멀리 떨어져 있다는 점을 이용해, stage까지 거리의 절반을 넘지 않도록 안전 마진을
            // 둬서 혹시 모델이 비정상적으로 클 때도 메인 씬까지 빛이 새어나가지 않게 한다.
            float safeMaxRange = shootingStagePosition.magnitude * 0.5f;
            iconLight.range = Mathf.Min(maxExtent * 7f + 1f, safeMaxRange);
        }

        /// <summary>
        /// Renderer.bounds(월드, 애니메이션/스키닝 상태에 따라 달라짐) 대신 Renderer.localBounds(메시 원본
        /// 기준, 항상 안정적)를 각 Renderer의 transform으로 월드 변환해서 합산한다. SkinnedMeshRenderer는
        /// Instantiate 직후(같은 프레임)에는 bounds가 아직 갱신되지 않아 잘못된 값을 반환할 수 있어, 이 방식이
        /// 훨씬 안정적이다.
        /// </summary>
        private Bounds CalculateWorldBounds(Renderer[] renderers)
        {
            Bounds combined = TransformLocalBoundsToWorld(renderers[0]);

            for (int i = 1; i < renderers.Length; i++)
            {
                combined.Encapsulate(TransformLocalBoundsToWorld(renderers[i]));
            }

            return combined;
        }

        /// <summary>로컬 바운드의 8개 모서리를 각각 월드로 변환해서 다시 감싼다 - 회전이 있어도 정확하다.</summary>
        private Bounds TransformLocalBoundsToWorld(Renderer renderer)
        {
            var localBounds = renderer.localBounds;
            var rendererTransform = renderer.transform;

            Bounds worldBounds = new Bounds(rendererTransform.TransformPoint(localBounds.center), Vector3.zero);

            for (int xSign = -1; xSign <= 1; xSign += 2)
            {
                for (int ySign = -1; ySign <= 1; ySign += 2)
                {
                    for (int zSign = -1; zSign <= 1; zSign += 2)
                    {
                        Vector3 corner = localBounds.center + Vector3.Scale(localBounds.extents, new Vector3(xSign, ySign, zSign));
                        worldBounds.Encapsulate(rendererTransform.TransformPoint(corner));
                    }
                }
            }

            return worldBounds;
        }

        private void OnDestroy()
        {
            if (iconRenderTexture != null)
            {
                iconRenderTexture.Release();
            }
        }
    }
}
