using HDY;
using HDY.Item;
using System;
using UnityEngine;

namespace KGH.Data
{
    [Serializable]
    public enum ObjectType
    {
        None,
        Tree,
        Stone,
        Bush
    }

    [Serializable]
    public struct ChestItem
    {
        // [HDY 요청] ItemData 직접 참조 대신 Item_ID 문자열로 변경.
        // ItemCatalogManager가 시트 기반으로 바뀌면서 런타임에 매번 새 ItemData 인스턴스를
        // 만들기 때문에, 여기서 특정 ItemData 애셋을 직접 들고 있으면 같은 Item_ID를 가진
        // 두 개의 서로 다른 객체가 메모리에 동시에 존재하게 되어 다른 곳(GridManager 등)의
        // Resources.FindObjectsOfTypeAll<ItemData>() 조회가 꼬일 수 있다. ID 문자열만 들고
        // 있다가 ItemCatalogManager.FindItemData(itemId)로 조회하는 방식으로 통일했다.
        public string itemId;

        // [HDY 요청] 드랍 개수를 Min~Max 랜덤 범위 대신 고정값 하나로 변경.
        // 기존 minDrop/maxDrop 필드는 제거했다. 프리팹(GH_Chest_Prefeb)에 이미 설정돼
        // 있던 값은 필드명이 바뀌면서 초기화되므로, 팀 확인 후 Inspector에서 직접 재입력.
        public int dropCount;
    }

    [Serializable]
    public struct ObjectDropItem
    {
        [Tooltip("ItemCatalogManager에 등록된 ItemData.Item_ID를 입력합니다. 별도 월드 아이템 프리팹은 필요하지 않습니다.")]
        public string itemId;
    }

    [Serializable]
    public struct CommonClassBonus
    {
        public CommonClass commonClass;
        public float bushBonus;
        public float stoneBonus;
        public float treeBonus;

    }
}
