using System;
using System.Collections.Generic;
using UnityEngine;
using HDY.Item;

namespace HDY.Cook
{
    /// <summary>
    /// 요리 레시피(CookRecipeData, ItemCatalogManager.CookRecipeDataList가 "요리 레시피 카탈로그" 전체)의
    /// 해금 여부를 관리하는 매니저.
    ///
    /// [HDY 요청 - 상점 레시피북 기능] 상점에서 cook_recipebook을 구매하면, 아직 해금되지 않은 요리
    /// 레시피 중 하나를 무작위로 뽑아 해금한다(ShopUI가 TryUnlockRandom을 구매 수량만큼 반복 호출).
    /// 이미 해금된 레시피는 자동으로 후보 풀에서 제외되므로(unlockedRecipeIds에 있는 것만 걸러냄)
    /// 중복 해금은 일어나지 않는다.
    ///
    /// [일반 제작법(HDY.Recipe.RecipeUnlockManager)과는 별개] 여신상에서 쓰는 일반 제작법 해금 시스템과는
    /// 완전히 분리된 매니저다 - 요리 레시피는 영지 레벨/골드 조건부 개별 구매가 아니라, 상점에서 뽑기 형태로
    /// 해금되는 별도 기획이라 데이터 구조와 저장 형식도 다를 수 있어 섞지 않았다.
    ///
    /// [모닥불/주방 사용 가능 여부] IsUnlocked(itemId)가 true인 레시피만 모닥불/주방에서 사용할 수 있어야
    /// 한다는 게 기획이지만, 그 화면(CampFirePanelUI, _Kyusoo 소유) 쪽 필터링 반영은 이 작업 범위에
    /// 포함하지 않았다 - 이 매니저는 해금 상태를 들고 IsUnlocked만 제공한다.
    ///
    /// [씬 배치] 다른 매니저들과 동일하게 씬에 미리 배치해두는 컴포넌트다(자동 생성하지 않음).
    ///
    /// [HDY 요청 - 저장/불러오기 연동] 이 매니저는 순수하게 데이터 보관 + 이벤트 발행만 담당하고, 실제
    /// 파일 저장/불러오기 연동(Kyusoo의 IRecord/RecordManager 패턴)은 이 작업 범위에 포함하지 않았다.
    /// 대신 연동에 필요한 3가지를 공개한다:
    /// 1) UnlockedRecipeIds(읽기 전용) - 지금까지 해금된 Result_Item_ID 전체. 저장 시 이 값을 그대로
    ///    SaveData 쪽 리스트로 복사하면 된다.
    /// 2) LoadUnlockedRecipeIds(loadedIds) - 불러온 세이브 데이터를 이 매니저에 그대로 주입한다.
    /// 3) OnRecipeUnlocked 이벤트 - 새로 해금될 때마다 발행되므로, 이 이벤트를 구독해 실시간 저장을
    ///    트리거할 수 있다(MemCaptureManager.OnCapturedMemsChanged와 동일한 역할).
    /// </summary>
    public class CookRecipeUnlockManager : MonoBehaviour
    {
        public static CookRecipeUnlockManager Instance { get; private set; }

        [Header("아이템 카탈로그 참조 (요리 레시피 카탈로그 전체 조회용, 비어있으면 자동 탐색)")]
        [SerializeField] private ItemCatalogManager itemCatalogManager;

        [Header("해금된 레시피 목록 (Result_Item_ID, 저장/불러오기 연동 대상)")]
        [SerializeField] private List<string> unlockedRecipeIds = new List<string>();

        private readonly HashSet<string> unlockedLookup = new HashSet<string>();

        // TryUnlockRandom에서 매번 새로 만들지 않도록 재사용하는 후보 목록 버퍼.
        private readonly List<CookRecipeData> candidateBuffer = new List<CookRecipeData>();

        /// <summary>
        /// [저장/불러오기 연동용] 지금까지 해금된 요리 레시피(Result_Item_ID) 전체.
        /// 저장 시스템이 이 값을 그대로 SaveData 쪽 리스트로 복사해서 저장하면 된다.
        /// </summary>
        public IReadOnlyList<string> UnlockedRecipeIds => unlockedRecipeIds;

        /// <summary>
        /// 새 요리 레시피가 해금될 때마다 발행된다(해금된 CookRecipeData 전달). 저장 시스템이 이 이벤트를
        /// 구독해서 실시간 저장을 트리거할 수 있다(MemCaptureManager.OnCapturedMemsChanged와 동일한 역할).
        /// </summary>
        public event Action<CookRecipeData> OnRecipeUnlocked;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[CookRecipeUnlockManager] 씬에 CookRecipeUnlockManager가 이미 있어 중복 오브젝트를 파괴합니다.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;

            itemCatalogManager = ItemCatalogManager.Resolve(itemCatalogManager);
            if (itemCatalogManager == null)
            {
                Debug.LogWarning("[CookRecipeUnlockManager] itemCatalogManager를 찾을 수 없습니다. 요리 레시피 카탈로그를 읽어올 수 없습니다.", this);
            }

            RebuildLookup();
        }

        /// <summary>인스펙터(또는 LoadUnlockedRecipeIds)에 채워진 unlockedRecipeIds 리스트로부터 조회용 HashSet을 다시 만든다.</summary>
        private void RebuildLookup()
        {
            unlockedLookup.Clear();

            foreach (var id in unlockedRecipeIds)
            {
                if (!string.IsNullOrEmpty(id)) unlockedLookup.Add(id);
            }
        }

        /// <summary>이 요리 레시피(Result_Item_ID)가 해금되었는지 여부.</summary>
        public bool IsUnlocked(string resultItemId)
        {
            if (string.IsNullOrEmpty(resultItemId)) return false;
            return unlockedLookup.Contains(resultItemId);
        }

        /// <summary>
        /// [HDY 요청 - 상점 레시피북] 아직 해금되지 않은 요리 레시피 중 하나를 무작위로 뽑아 해금한다.
        /// 후보가 하나도 없으면(카탈로그 전체가 이미 해금됨, 또는 카탈로그 자체가 비어있음) false를
        /// 반환하고 unlocked는 null - 호출 쪽(ShopUI)이 이 경우 환불 처리를 한다.
        /// </summary>
        public bool TryUnlockRandom(out CookRecipeData unlocked)
        {
            unlocked = null;

            itemCatalogManager = ItemCatalogManager.Resolve(itemCatalogManager);
            if (itemCatalogManager == null) return false;

            candidateBuffer.Clear();
            foreach (var recipe in itemCatalogManager.CookRecipeDataList)
            {
                if (recipe == null || string.IsNullOrEmpty(recipe.Result_Item_ID)) continue;
                if (unlockedLookup.Contains(recipe.Result_Item_ID)) continue; // 이미 해금됨 - 후보에서 제외(중복 방지)

                candidateBuffer.Add(recipe);
            }

            if (candidateBuffer.Count == 0) return false; // 해금 가능한 레시피가 없음

            int pickedIndex = UnityEngine.Random.Range(0, candidateBuffer.Count);
            unlocked = candidateBuffer[pickedIndex];

            unlockedRecipeIds.Add(unlocked.Result_Item_ID);
            unlockedLookup.Add(unlocked.Result_Item_ID);

            Debug.Log($"[CookRecipeUnlockManager] 요리 레시피 해금: Result_Item_ID={unlocked.Result_Item_ID}");

            OnRecipeUnlocked?.Invoke(unlocked);

            return true;
        }

        /// <summary>
        /// [저장/불러오기 연동용] 세이브 파일에서 불러온 해금 목록을 이 매니저에 그대로 주입한다.
        /// unlockedRecipeIds와 조회용 HashSet(unlockedLookup)을 한 번에 정합성 있게 교체하므로, 저장/불러오기
        /// 시스템은 리플렉션 없이 이 메서드 한 번만 호출하면 된다. 중복 ID는 한 번만 유지된다.
        /// </summary>
        public void LoadUnlockedRecipeIds(IEnumerable<string> loadedIds)
        {
            unlockedRecipeIds.Clear();
            unlockedLookup.Clear();

            if (loadedIds != null)
            {
                foreach (var id in loadedIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (unlockedLookup.Contains(id)) continue;

                    unlockedRecipeIds.Add(id);
                    unlockedLookup.Add(id);
                }
            }

            Debug.Log($"[CookRecipeUnlockManager] 저장된 요리 레시피 해금 목록 불러오기 완료: {unlockedRecipeIds.Count}개");
        }

        /// <summary>
        /// 다른 스크립트가 들고 있는 CookRecipeUnlockManager 참조가 비어있을 때 쓰는 공용 폴백 탐색.
        /// (MemCatalogManager.Resolve/MemDexRecordManager.Resolve와 동일한 패턴)
        /// </summary>
        public static CookRecipeUnlockManager Resolve(CookRecipeUnlockManager existing)
        {
            if (existing != null) return existing;
            if (Instance != null) return Instance;

            var found = FindFirstObjectByType<CookRecipeUnlockManager>();
            if (found == null)
            {
                Debug.LogWarning("[CookRecipeUnlockManager] 씬에서 CookRecipeUnlockManager를 찾을 수 없습니다.");
            }

            return found;
        }
    }
}
