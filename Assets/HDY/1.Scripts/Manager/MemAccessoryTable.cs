using System.Collections.Generic;
using UnityEngine;
using MemSystem.Data;

namespace HDY.Mem
{
    /// <summary>
    /// accessoryId -> MemAccessoryData 매핑 전용 SO.
    /// MemCatalogManager가 시트(csv)의 AccessoryIds 컬럼(세미콜론 구분 accessoryId 목록, 예:
    /// "acc_head_strawhat;acc_head_daisy")을 실제 MemAccessoryData 에셋으로 변환할 때 이 테이블에서
    /// 조회한다. (MemAppearanceTable과 동일한 패턴 - ScriptableObject/GameObject 참조는 CSV에 담을 수
    /// 없어 별도 테이블로 분리해 관리한다.)
    ///
    /// [HDY 요청 - 아이콘 굽기 도구 연동] MemIconBakerWindow도 같은 테이블을 참조해서, CSV에 지정된
    /// 악세서리를 붙인 모습 그대로 아이콘을 굽는다(런타임과 동일한 소스 오브 트루스).
    ///
    /// 악세서리 목록 자체는 Pikachu 팀의 MemAccessoryData 에셋이 원본이라, 여기서는 "어떤 에셋들을
    /// accessoryId로 찾을 수 있게 등록해둘지"만 관리한다. 새 악세서리 에셋이 추가되면 이 목록에도
    /// 등록해야 CSV에서 참조할 수 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "MemAccessoryTable", menuName = "HDY/Mem/Mem Accessory Table", order = 2)]
    public class MemAccessoryTable : ScriptableObject
    {
        [Header("등록된 악세서리 목록 (각 에셋 자신의 accessoryId 필드로 조회됨)")]
        [SerializeField] private List<MemAccessoryData> accessories = new List<MemAccessoryData>();

        private Dictionary<string, MemAccessoryData> lookup;

        private void BuildLookupIfNeeded()
        {
            if (lookup != null) return;

            lookup = new Dictionary<string, MemAccessoryData>();
            foreach (var accessory in accessories)
            {
                if (accessory == null || string.IsNullOrEmpty(accessory.accessoryId)) continue;

                if (!lookup.ContainsKey(accessory.accessoryId))
                {
                    lookup.Add(accessory.accessoryId, accessory);
                }
                else
                {
                    Debug.LogWarning($"[MemAccessoryTable] accessoryId가 중복되었습니다: {accessory.accessoryId} (먼저 등록된 항목을 유지합니다)");
                }
            }
        }

        /// <summary>accessoryId로 MemAccessoryData를 찾는다. 목록에 없으면 null.</summary>
        public MemAccessoryData GetAccessory(string accessoryId)
        {
            if (string.IsNullOrEmpty(accessoryId)) return null;

            BuildLookupIfNeeded();
            return lookup.TryGetValue(accessoryId, out var accessory) ? accessory : null;
        }

#if UNITY_EDITOR
        /// <summary>에디터 마이그레이션 툴 등에서 항목을 채울 때 사용. 런타임에는 사용하지 않는다.</summary>
        public void EditorSetAccessories(List<MemAccessoryData> newAccessories)
        {
            accessories = newAccessories;
            lookup = null;
        }

        /// <summary>[HDY 요청 - 아이콘 굽기 도구 연동] 전체 항목을 읽기 전용으로 열람한다.</summary>
        public IReadOnlyList<MemAccessoryData> EditorAccessories => accessories;
#endif
    }
}
