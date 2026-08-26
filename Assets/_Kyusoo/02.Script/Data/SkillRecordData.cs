using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using KMS.Combat;

/// <summary>
/// [멤] 스킬 시스템(보유/장착/쿨타임)을 RecordManager의 IRecord 패턴에 연결하는 어댑터.
/// CookRecipeRecordData와 동일한 구조 - 실제 데이터는 SkillUnlockManager/PlayerSkillLoadout/
/// PlayerWeaponSkillController가 들고 있고, 이 클래스는 그 값을 SaveData 필드로 옮겨쓰고
/// 읽어오는 역할만 한다.
///
/// [멤] 영지(Territory) 처리: 영지는 전투가 불가능한(캐릭터 자체가 없는) 씬이라, ApplyData가
/// SceneType.Territory로 불리면 쿨타임을 이 시점에 완전히 초기화하고 그 초기화된 상태를 저장
/// 파일에도 바로 반영한다(다음에 탐험/보스 씬으로 나갈 때 항상 쿨타임 0부터 시작). 보유/장착
/// 데이터는 씬 종류와 무관하게 항상 그대로 복원한다 - "영지에서는 쿨타임이 초기화되어 스킬을
/// 변경하는데 제한이 없다"는 요구사항은 실제로는 UI 쪽(SkillRegistrationPanelUI.IsExplorationScene)
/// 에서 탐험 씬이 아닐 때 잠금 자체를 걸지 않는 방식으로 구현되어 있고, 여기서는 그 전제가 되는
/// "쿨타임 데이터 자체를 초기화"만 담당한다.
/// </summary>
public class SkillRecordData : MonoBehaviour, IRecord
{
    private SkillUnlockManager liveSkillUnlockManager;
    private PlayerSkillLoadout liveSkillLoadout;
    private PlayerWeaponSkillController liveWeaponController;

    private void OnEnable()
    {
        RefreshManagerReferences();
    }

    private void OnDisable()
    {
        UnsubscribeManagers();
    }

    private void RefreshManagerReferences()
    {
        UnsubscribeManagers();

        liveSkillUnlockManager = SkillUnlockManager.Resolve(null);
        liveSkillLoadout = FindFirstObjectByType<PlayerSkillLoadout>();
        liveWeaponController = FindFirstObjectByType<PlayerWeaponSkillController>();

        if (liveSkillUnlockManager != null)
        {
            liveSkillUnlockManager.OnSkillUnlocked += OnSkillUnlockedHandler;
        }

        if (liveSkillLoadout != null)
        {
            liveSkillLoadout.OnSlotChanged += OnLoadoutSlotChangedHandler;
            liveSkillLoadout.OnSpecialSlotChanged += OnLoadoutSpecialSlotChangedHandler;
        }
    }

    private void UnsubscribeManagers()
    {
        if (liveSkillUnlockManager != null)
        {
            liveSkillUnlockManager.OnSkillUnlocked -= OnSkillUnlockedHandler;
        }

        if (liveSkillLoadout != null)
        {
            liveSkillLoadout.OnSlotChanged -= OnLoadoutSlotChangedHandler;
            liveSkillLoadout.OnSpecialSlotChanged -= OnLoadoutSpecialSlotChangedHandler;
        }

        liveSkillUnlockManager = null;
        liveSkillLoadout = null;
        liveWeaponController = null;
    }

    /// <summary>새 스킬을 보유하게 되면(획득 데이터 변경) 실시간 저장을 트리거한다.</summary>
    private void OnSkillUnlockedHandler(SkillData _)
    {
        TriggerRealtimeSave();
    }

    /// <summary>장착 칸(1~4등급)이 바뀌면(설정 데이터 변경) 실시간 저장을 트리거한다.</summary>
    private void OnLoadoutSlotChangedHandler(int _, SkillData __)
    {
        TriggerRealtimeSave();
    }

    /// <summary>특수(5등급) 칸이 바뀌면(설정 데이터 변경) 실시간 저장을 트리거한다.</summary>
    private void OnLoadoutSpecialSlotChangedHandler(SkillData _)
    {
        TriggerRealtimeSave();
    }

    private void TriggerRealtimeSave()
    {
        if (RecordManager.IsLoadingData) return;
        if (RecordManager.Instance == null) return;

        SaveData(RecordManager.Instance.SaveFilePath);
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.unlockedSkillIds = new List<string>();
        saveData.equippedSkillIds = new List<string>();
        saveData.equippedSpecialSkillId = string.Empty;
        saveData.skillCooldowns = new List<SkillCooldownEntry>();
    }

    public void SaveData(string saveFilePath)
    {
        RefreshManagerReferences();

        SaveData currentData = RecordManager.Instance != null ? RecordManager.Instance.ReadRawSaveFileOnly() : null;
        if (currentData == null) currentData = new SaveData();

        currentData.unlockedSkillIds = (liveSkillUnlockManager != null && liveSkillUnlockManager.UnlockedSkillIds != null)
            ? new List<string>(liveSkillUnlockManager.UnlockedSkillIds)
            : new List<string>();

        if (liveSkillLoadout != null)
        {
            currentData.equippedSkillIds = new List<string>(liveSkillLoadout.GetEquippedSkillIdsForSave());
            currentData.equippedSpecialSkillId = liveSkillLoadout.GetSpecialSkillIdForSave();
        }

        // [멤] liveWeaponController가 null이면(영지처럼 전투 캐릭터 자체가 없는 씬) 쿨타임을 빈 목록으로
        // 저장한다 - 별도 분기 없이도 자연히 "영지에서는 쿨타임이 없다"가 유지된다.
        currentData.skillCooldowns = new List<SkillCooldownEntry>();
        if (liveWeaponController != null)
        {
            foreach (var pair in liveWeaponController.GetSkillCooldownSnapshotForSave())
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value <= 0f) continue;
                currentData.skillCooldowns.Add(new SkillCooldownEntry { skillId = pair.Key, remainingSeconds = pair.Value });
            }
        }

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        RefreshManagerReferences();

        // 보유/장착 데이터(획득 데이터, 설정 데이터)는 씬 종류와 무관하게 항상 그대로 복원한다.
        if (liveSkillUnlockManager != null && saveData.unlockedSkillIds != null)
        {
            liveSkillUnlockManager.LoadUnlockedSkillIds(saveData.unlockedSkillIds);
        }

        if (liveSkillLoadout != null)
        {
            liveSkillLoadout.LoadEquippedSkillIds(saveData.equippedSkillIds);
            liveSkillLoadout.LoadSpecialSkillId(saveData.equippedSpecialSkillId);
        }

        if (sceneType == SceneType.Territory)
        {
            // [멤] 영지는 전투 불가(캐릭터 없음) 씬이라 쿨타임을 이 시점에 완전히 초기화하고,
            // 저장 파일에도 즉시 반영해서 이후 다른 이유로 저장이 일어나도 계속 초기화 상태가 유지되게 한다.
            liveWeaponController?.ClearAllCooldowns();
            PersistClearedCooldowns(saveData);
            return;
        }

        if (liveWeaponController != null && saveData.skillCooldowns != null)
        {
            var loaded = new Dictionary<string, float>();
            foreach (var entry in saveData.skillCooldowns)
            {
                if (entry == null || string.IsNullOrEmpty(entry.skillId)) continue;
                loaded[entry.skillId] = entry.remainingSeconds;
            }

            liveWeaponController.LoadSkillCooldowns(loaded);
        }
    }

    /// <summary>영지 진입 시점에 방금 불러온 saveData 객체를 그대로 재사용해 쿨타임만 비우고 다시 저장한다.</summary>
    private void PersistClearedCooldowns(SaveData loadedData)
    {
        if (RecordManager.Instance == null) return;
        if (loadedData.skillCooldowns == null || loadedData.skillCooldowns.Count == 0) return;

        loadedData.skillCooldowns = new List<SkillCooldownEntry>();
        loadedData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(RecordManager.Instance.SaveFilePath, JsonUtility.ToJson(loadedData, true));
    }
}
