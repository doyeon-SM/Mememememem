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
        public int minDrop;
        public int maxDrop;
    }

    [Serializable]
    public struct ObjectDropItem
    {
        public GameObject dropPrefab;
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
