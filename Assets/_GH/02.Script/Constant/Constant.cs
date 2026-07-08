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
}
