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
        public ItemData itemData;
        public int minDrop;
        public int maxDrop;
    }

    [Serializable]
    public struct ObjectDropItem
    {
        public GameObject dropPrefab;
        public int minDrop;
        public int maxDrop;
    }

    [Serializable]
    public struct CommonClassBonus
    {
        public CommonClass commonClass;
        public float bushBonus;
        public float stoneBonus;
        public float treeBonus;

    }
    [System.Serializable]
    public enum ItemGrade
    {
        Rare,
        Epic,
        Unique,
        Legendary,
        Myth
    }
    [System.Serializable]
    public class GradeBonus
    {
        public ItemGrade grade;
        public int damageBonus;
        public int dropBonus;
    }
}
