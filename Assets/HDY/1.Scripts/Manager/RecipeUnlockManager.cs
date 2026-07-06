using System;
using System.Collections.Generic;
using UnityEngine;
using HDY.Item;

namespace HDY.Recipe
{
    /// <summary>
    /// 제작법 하나(아이템 SO)와 해금 여부를 짝지어 보관하는 항목.
    /// 인스펙터에서 ItemData와 해금 bool을 한 번에 확인/설정할 수 있다.
    /// </summary>
    [Serializable]
    public class RecipeUnlockEntry
    {
        public ItemData Recipe;
        public bool IsUnlocked;
    }

    /// <summary>
    /// 영지에서 사용 가능한 제작법의 해금 여부를 보관하는 매니저.
    /// 현재 단계에서는 bool 값만 저장하며, ItemData(제작법 SO)를 키로 탐색한다.
    /// </summary>
    public class RecipeUnlockManager : MonoBehaviour
    {
        [Header("제작법 해금 목록 (인스펙터에서 SO + 해금여부를 함께 등록/확인)")]
        [SerializeField] private List<RecipeUnlockEntry> recipeUnlocks = new List<RecipeUnlockEntry>();

        public IReadOnlyList<RecipeUnlockEntry> RecipeUnlocks => recipeUnlocks;

        /// <summary>해당 제작법(ItemData)의 해금 여부를 반환한다. 목록에 없으면 false.</summary>
        public bool IsUnlocked(ItemData recipe)
        {
            if (recipe == null) return false;

            var entry = recipeUnlocks.Find(e => e.Recipe == recipe);
            return entry != null && entry.IsUnlocked;
        }

        /// <summary>해당 제작법(ItemData)을 해금 처리한다. 목록에 없으면 새 항목을 추가해 해금 처리.</summary>
        public void Unlock(ItemData recipe)
        {
            if (recipe == null) return;

            var entry = recipeUnlocks.Find(e => e.Recipe == recipe);
            if (entry != null)
            {
                entry.IsUnlocked = true;
            }
            else
            {
                recipeUnlocks.Add(new RecipeUnlockEntry { Recipe = recipe, IsUnlocked = true });
            }
        }

        /// <summary>해당 제작법(ItemData)을 다시 잠금 처리한다. 목록에 없으면 아무 동작도 하지 않는다.</summary>
        public void Lock(ItemData recipe)
        {
            if (recipe == null) return;

            var entry = recipeUnlocks.Find(e => e.Recipe == recipe);
            if (entry != null)
            {
                entry.IsUnlocked = false;
            }
        }
    }
}
