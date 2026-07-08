using HDY.Item;
using KGH.Data;
using KMS.InventoryDuped;
using System;
using UnityEngine;


public class Chest : MonoBehaviour
{
    [Header("Setting")]
    [Tooltip("현재는 다중 드랍으로 구조 작성")][SerializeField] private ChestItem[] dropItem;
    [Tooltip("False일 경우 0번 인덱스만 드랍")][SerializeField] private bool isOverlap;

/// <summary>
/// 인벤토리 수정 후 동작구조 보완 필요
/// </summary>
    private void ChestItem(PlayerInventory inventory)
    {
        if(!isOverlap)
        {
            int count = UnityEngine.Random.Range(dropItem[0].minDrop, dropItem[0].maxDrop +1);
            //inventory.AddItem(dropItem[0].itemData, count);
        }
        else
        {
            for(int i = 0; i < dropItem.Length; i++)
            {
                int count = UnityEngine.Random.Range(dropItem[i].minDrop, dropItem[i].maxDrop + 1);
                //inventory.AddItem(dropItem[i].itemData, count);
            }
        }
    }
}
