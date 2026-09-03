using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using KMS.Equipment;
using KMS.InventoryDuped;

/// <summary>
/// [멤] 장비 시스템 저장/불러오기 어댑터. 대장간 도구의 ForgeRecordData와 정확히 대칭되는 구조이며,
/// 두 가지를 담당한다:
/// - 장착창 12칸(PlayerEquipment) -> SaveData.playerEquipmentData
/// - 장비 개체 목록(EquipmentInstanceRegistry) -> SaveData.equipmentInstanceDataList
///
/// [멤] 주의: RecordManager.LoadAndBroadcastTerritoryData는 IRecord를 제네릭 루프가 아니라
/// 타입 이름 하드코딩으로 개별 호출하므로, 이 클래스 이름("EquipmentRecordData")이 그 목록에
/// 들어있어야 ApplyData가 호출된다(이미 추가해둠).
/// </summary>
public class EquipmentRecordData : MonoBehaviour, IRecord
{
    private PlayerEquipment cachedEquipment;

    private void OnEnable()
    {
        EquipmentInstanceRegistry.OnEquipmentInstanceDataChanged += HandleEquipmentDataChanged;
        SubscribePlayerEquipment();
    }

    private void OnDisable()
    {
        EquipmentInstanceRegistry.OnEquipmentInstanceDataChanged -= HandleEquipmentDataChanged;
        UnsubscribePlayerEquipment();
    }

    private void SubscribePlayerEquipment()
    {
        cachedEquipment = ResolveEquipment();
        if (cachedEquipment != null) cachedEquipment.OnEquipmentChanged += HandleEquipmentDataChanged;
    }

    private void UnsubscribePlayerEquipment()
    {
        if (cachedEquipment != null) cachedEquipment.OnEquipmentChanged -= HandleEquipmentDataChanged;
        cachedEquipment = null;
    }

    // [멤] 장착/해제 또는 개체 변경(전승 등)이 일어나면 즉시 저장한다 - 다른 RecordData들과 동일한 관례.
    private void HandleEquipmentDataChanged()
    {
        if (RecordManager.IsLoadingData) return;
        if (RecordManager.Instance == null) return;

        // [멤] 저장 빈도 감축 - 장착/해제는 자주 일어나므로 변경 표시만 한다.
        RecordManager.NotifyDataChanged();
    }

    private static PlayerEquipment ResolveEquipment()
    {
        return UnityEngine.Object.FindFirstObjectByType<PlayerEquipment>();
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.equipmentInstanceDataList = new List<EquipmentInstanceData>();
        saveData.playerEquipmentData = CreateEmptyContainer();
    }

    public void SaveData(string saveFilePath)
    {
        if (RecordManager.Instance == null) return;

        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        var registry = EquipmentInstanceRegistry.Resolve(null);
        if (registry != null)
        {
            currentData.equipmentInstanceDataList = new List<EquipmentInstanceData>(registry.AllInstances);
        }

        // [멤] 플레이어가 없는 씬(영지 등)에서는 장착창을 건드리지 않고 기존 저장값을 그대로 둔다 -
        // 빈 값으로 덮어써서 장비가 사라지는 사고를 막기 위함이다.
        var equipment = ResolveEquipment();
        if (equipment != null)
        {
            currentData.playerEquipmentData = CaptureContainer(equipment);
        }
        else if (currentData.playerEquipmentData == null)
        {
            currentData.playerEquipmentData = CreateEmptyContainer();
        }

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        if (saveData == null) return;

        var registry = EquipmentInstanceRegistry.Resolve(null);
        if (registry != null && saveData.equipmentInstanceDataList != null)
        {
            registry.RestoreInstances(saveData.equipmentInstanceDataList);

            // 런타임 ItemData 캐시를 복원된 개체 상태로 다시 계산한다(ForgeRecordData와 동일한 처리).
            var provider = EquipmentInstanceItemDataProvider.Resolve(null);
            if (provider != null)
            {
                foreach (var instance in saveData.equipmentInstanceDataList)
                {
                    if (instance != null) provider.RefreshRuntimeItemData(instance.BuildCompositeId());
                }
            }
        }

        var equipment = ResolveEquipment();
        if (equipment != null)
        {
            equipment.RestoreSaveData(ToItemStacks(saveData.playerEquipmentData));
        }

        // 씬 전환으로 플레이어가 새로 생성됐을 수 있으므로 구독 대상을 다시 잡는다.
        UnsubscribePlayerEquipment();
        SubscribePlayerEquipment();
    }

    // ---- 변환 헬퍼 ----

    private static ContainerData CreateEmptyContainer()
    {
        var container = new ContainerData
        {
            width = EquipmentSlotLayout.TotalSlotCount,
            height = 1,
            slots = new List<ItemStackData>()
        };

        for (int i = 0; i < EquipmentSlotLayout.TotalSlotCount; i++)
        {
            container.slots.Add(new ItemStackData { itemId = string.Empty, amount = 0 });
        }

        return container;
    }

    private static ContainerData CaptureContainer(PlayerEquipment equipment)
    {
        ItemStack[] captured = equipment.CaptureSaveData();

        var container = new ContainerData
        {
            width = EquipmentSlotLayout.TotalSlotCount,
            height = 1,
            slots = new List<ItemStackData>()
        };

        for (int i = 0; i < captured.Length; i++)
        {
            ItemStack slot = captured[i];
            container.slots.Add(new ItemStackData
            {
                itemId = slot != null && slot.itemId != null ? slot.itemId : string.Empty,
                amount = slot != null ? slot.amount : 0,
                durability = slot != null ? slot.durability : -1
            });
        }

        return container;
    }

    private static ItemStack[] ToItemStacks(ContainerData container)
    {
        var stacks = new ItemStack[EquipmentSlotLayout.TotalSlotCount];
        for (int i = 0; i < stacks.Length; i++)
        {
            ItemStackData source = container != null && container.slots != null && i < container.slots.Count
                ? container.slots[i]
                : null;

            stacks[i] = source == null
                ? new ItemStack()
                : new ItemStack { itemId = source.itemId, amount = source.amount, durability = source.durability };
        }

        return stacks;
    }
}
