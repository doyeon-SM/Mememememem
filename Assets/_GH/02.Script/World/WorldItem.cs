using HDY.Item;
using UnityEngine;
using KMS.InventoryDuped;
public class WorldItem : MonoBehaviour
{
    [Header("Ref")]
    [SerializeField] private ItemData itemdata;
    [SerializeField] private int amount;

    private void OnCollisionEnter(Collision collision)
    {
        KMS.InventoryDuped.PlayerInventory inventory = GetComponent<KMS.InventoryDuped.PlayerInventory>();
        if(inventory != null)
        {
            //inventory.AddItem(itemdata, amount);
            //TODO : 풀링 구조 , 인벤토리 데이터 수정 후 수정 필요
        }
    }
}
