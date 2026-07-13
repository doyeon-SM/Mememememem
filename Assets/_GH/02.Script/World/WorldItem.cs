using HDY.Item;
using UnityEngine;
using KMS.InventoryDuped;
public class WorldItem : MonoBehaviour
{
    [Header("Ref")]
    [SerializeField] private ItemData itemdata;
    [SerializeField] private int amount = 1;

    private int initialAmount;

    private void Awake()
    {
        initialAmount = amount;
    }

    private void OnEnable()
    {
        // 풀에서 다시 사용될 때 프리팹에 설정된 원래 수량으로 복구한다.
        amount = initialAmount;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (itemdata == null || amount <= 0)
        {
            return;
        }

        PlayerInventory inventory = collision.collider.GetComponentInParent<PlayerInventory>();
        if (inventory == null)
        {
            return;
        }

        int remaining = inventory.AddItem(itemdata.Item_ID, amount);
        if (remaining > 0)
        {
            // 일부만 들어갔다면 남은 수량은 월드에 유지한다.
            amount = remaining;
            return;
        }

        PooledWorldDrop pooledDrop = GetComponent<PooledWorldDrop>();
        if (pooledDrop == null || !pooledDrop.ReturnToPool())
        {
            // 풀에서 생성되지 않은 씬 배치 아이템만 예외적으로 제거한다.
            Destroy(gameObject);
        }
    }
}
