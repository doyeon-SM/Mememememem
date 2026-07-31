// ============================================================================
// MemAccessoryData.cs
// ScriptableObject — 멤 악세서리(머리·몸 부착 3D 모델) 정의 데이터
//
// [담당자 안내]
// - Project 창 우클릭 → Create → Mem → MemAccessory 로 새 에셋을 생성합니다.
// - prefab에 악세서리 3D 모델(FBX/프리팹)을 넣고, slot으로 부착 위치를 고릅니다.
// - 런타임에 MemVisual이 해당 슬롯의 "뼈(Bone)"에 자식으로 붙여줍니다.
//   → 뼈에 붙으므로 애니메이션(고개 끄덕임 등)을 자동으로 따라갑니다.
// - 위치/회전/크기가 안 맞으면 아래 오프셋 3종으로 맞추세요.
//   Play Mode에서 MemAccessoryTester로 실시간 조정 후 값을 복사해오면 편합니다.
//
// [모델 제작 요청 시 주의]
// - 리깅(스키닝)된 모델이 아니라 "단단한(rigid) 단일 메시"로 받으세요.
//   뼈에 통째로 붙이는 방식이라 변형(스카프가 흩날린다든지)은 지원하지 않습니다.
// - 피벗을 부착 기준점(모자면 챙 안쪽 중앙 등)에 맞춰주면 오프셋 잡기가 쉽습니다.
// ============================================================================
using UnityEngine;

namespace MemSystem.Data
{
    /// <summary>
    /// 멤에게 부착할 악세서리 한 종류를 정의하는 ScriptableObject.
    ///
    /// 하나의 에셋 = 하나의 악세서리.
    /// 예: Acc_Head_Straw_Hat.asset, Acc_Body_Scarf.asset 등
    ///
    /// MemData.accessories 배열에 넣으면 그 멤이 스폰될 때 자동으로 장착됩니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMemAccessory", menuName = "Mem/MemAccessory")]
    public class MemAccessoryData : ScriptableObject
    {
        // =====================================================================
        // 기본 정보
        // =====================================================================

        [Header("기본 정보")]
        [Tooltip("고유 식별자 (예: acc_head_straw_hat). 저장/도감 연동 시 사용합니다.")]
        public string accessoryId;

        [Tooltip("게임 내 표시 이름 (예: 밀짚모자)")]
        public string displayName;

        // =====================================================================
        // 부착 설정
        // =====================================================================

        [Header("부착 설정")]
        [Tooltip("부착 위치 슬롯. 슬롯당 하나만 장착되며, 같은 슬롯에 새로 끼우면 기존 것이 교체됩니다.")]
        public MemAccessorySlot slot = MemAccessorySlot.Head;

        [Tooltip("장착할 3D 모델 (FBX 또는 프리팹). 스키닝 없는 단일 메시를 권장합니다.")]
        public GameObject prefab;

        // =====================================================================
        // 오프셋 (부착 뼈 기준 로컬 값)
        // =====================================================================

        [Header("오프셋 (부착 뼈 기준 로컬)")]
        [Tooltip("뼈 기준 위치 오프셋")]
        public Vector3 positionOffset = Vector3.zero;

        [Tooltip("뼈 기준 회전 오프셋 (Euler Angles)")]
        public Vector3 rotationOffset = Vector3.zero;

        [Tooltip("프리팹 원본 스케일에 곱해지는 배율. 모델이 너무 크거나 작을 때 조정하세요.")]
        public Vector3 scaleMultiplier = Vector3.one;

        // =====================================================================
        // 연출 연동
        // =====================================================================

        [Header("연출 연동")]
        [Tooltip("피격 플래시·포획 빛남 등 색상 연출에 이 악세서리도 함께 물들지 여부.\n" +
                 "끄면 악세서리만 원래 색을 유지합니다 (금속 장신구 등에 유용).")]
        public bool includeInColorEffects = true;
    }
}
