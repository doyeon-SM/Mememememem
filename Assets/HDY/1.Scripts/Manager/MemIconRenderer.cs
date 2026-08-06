using UnityEngine;

namespace HDY.Mem
{
    /// <summary>
    /// 멤 아이콘(Sprite) 조회 전용 런타임 컴포넌트.
    ///
    /// [HDY 요청 - 에디터 사전 굽기 전환] 예전에는 이 클래스가 런타임에 카메라로 modelPrefab을
    /// 직접 촬영해서 아이콘을 만들었다(멤창고 그리드를 열 때마다 수십~수백 개를 촬영하는 비용이
    /// 있었음). 이제는 에디터 전용 도구(MemIconBaker, Assets/HDY/Editor/)가 미리 구워서 만든
    /// MemIconTable을 그대로 조회만 한다. 런타임에는 카메라/RenderTexture 생성이 전혀 일어나지
    /// 않는다. 아직 굽지 않은 멤은 MemIconTable의 fallbackMemId 아이콘이 자동으로 대신 반환된다.
    /// </summary>
    public class MemIconRenderer : MonoBehaviour
    {
        public static MemIconRenderer Instance { get; private set; }

        [Header("데이터 참조")]
        [SerializeField] private MemIconTable iconTable;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[MemIconRenderer] 씬에 MemIconRenderer가 이미 있어 중복 오브젝트를 파괴합니다.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        /// <summary>memId에 해당하는 아이콘을 반환한다(기존 호출부 호환용 - 128px 기본 해상도).</summary>
        public Sprite GetIcon(string memId) => GetIcon128(memId);

        /// <summary>memId에 해당하는 64px 아이콘(시설/창고 슬롯용)을 반환한다.</summary>
        public Sprite GetIcon64(string memId) => iconTable != null ? iconTable.GetIcon64(memId) : null;

        /// <summary>memId에 해당하는 128px 아이콘(도감 슬롯용)을 반환한다.</summary>
        public Sprite GetIcon128(string memId) => iconTable != null ? iconTable.GetIcon128(memId) : null;

        /// <summary>memId에 해당하는 512px 아이콘(멤 정보 큰 아이콘용)을 반환한다.</summary>
        public Sprite GetIcon512(string memId) => iconTable != null ? iconTable.GetIcon512(memId) : null;
    }
}
