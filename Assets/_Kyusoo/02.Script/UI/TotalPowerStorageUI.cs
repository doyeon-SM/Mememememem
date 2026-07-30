using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 발전기 좌측 하단 전력 축적량(15칸 세그먼트 배터리) 및 호버 툴팁 컴포넌트입니다.
/// </summary>
public class TotalPowerStorageUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("15칸 세그먼트 배터리 UI")]
    [Tooltip("배터리를 구성하는 15개의 노란색 칸(Image) 리스트")]
    [SerializeField] private List<Image> segmentImages = new List<Image>();

    [Header("단일 Filled 이미지 사용 시 (옵션)")]
    [Tooltip("15칸 세그먼트 이미지 리스트 대신 단일 Filled 이미지를 스냅 단계로 채울 경우 연결")]
    [SerializeField] private Image fillImage;

    [Header("호버 툴팁 UI")]
    [SerializeField] private GameObject tooltipObject;
    [SerializeField] private TMP_Text tooltipText;

    private GeneratorRuntime currentFacility;

    private void Awake()
    {
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(false);
        }
    }

    /// <summary>
    /// GeneratorPanelUI에서 실시간으로 호출되어 15칸 게이지 및 툴팁 수치를 갱신합니다.
    /// </summary>
    public void RefreshUI(GeneratorRuntime facility)
    {
        if (facility == null) return;
        currentFacility = facility;

        int currentStorage = facility.currentPowerStorage;
        int totalTerritoryMaxStorage = CalculateTotalTerritoryMaxStorage();

        // 비율 계산 (현재 시설 축적량 / 영지 전체 발전기 최대 축적량)
        float fillRatio = totalTerritoryMaxStorage > 0 ? (float)currentStorage / totalTerritoryMaxStorage : 0f;
        fillRatio = Mathf.Clamp01(fillRatio);

        // 1. 15칸 세그먼트 이미지 개별 제어 방식
        if (segmentImages != null && segmentImages.Count > 0)
        {
            int activeSegmentCount = Mathf.RoundToInt(fillRatio * segmentImages.Count);
            for (int i = 0; i < segmentImages.Count; i++)
            {
                if (segmentImages[i] != null)
                {
                    segmentImages[i].gameObject.SetActive(i < activeSegmentCount);
                }
            }
        }
        // 2. 단일 Filled Image 사용 시 15단계 스냅 처리 방식
        else if (fillImage != null)
        {
            float steppedFill = Mathf.Floor(fillRatio * 15f) / 15f;
            fillImage.fillAmount = steppedFill;
        }

        // 3. 호버 툴팁 수치화 (수치 + 한글 설명 2줄 표시)
        if (tooltipText != null)
        {
            tooltipText.text = $"{currentStorage} / {totalTerritoryMaxStorage} W\n<size=85%><color=#CCCCCC>(현재 축적량 / 전체 축적량)</color></size>";
        }
    }

    /// <summary>
    /// 영지 내에 배치된 모든 GeneratorRuntime의 maxPowerStorage 합산
    /// </summary>
    private int CalculateTotalTerritoryMaxStorage()
    {
        var generators = FindObjectsByType<GeneratorRuntime>(FindObjectsSortMode.None);
        int sumMaxStorage = 0;

        foreach (var gen in generators)
        {
            if (gen != null)
            {
                sumMaxStorage += gen.maxPowerStorage;
            }
        }

        return sumMaxStorage;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipObject != null)
        {
            tooltipObject.SetActive(false);
        }
    }
}