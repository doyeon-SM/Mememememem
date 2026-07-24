using System.Collections.Generic;
using UnityEngine;

public class FacilityCollectUI : MonoBehaviour
{
    public static FacilityCollectUI Instance { get; private set; }

    [Header("프리팹 및 컨테이너")]
    [SerializeField] private GameObject bubblePrefab;
    [SerializeField] private Transform bubbleParentContainer;

    private Camera mainCamera;
    private Dictionary<MonoBehaviour, FacilityBubbleUI> activeBubbles = new Dictionary<MonoBehaviour, FacilityBubbleUI>();
    private Queue<FacilityBubbleUI> bubblePool = new Queue<FacilityBubbleUI>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        // 활성화된 모든 말풍선을 3D 월드 위치에 맞추어 Screen 위치 추적
        foreach (var pair in activeBubbles)
        {
            MonoBehaviour facility = pair.Key;
            FacilityBubbleUI bubble = pair.Value;

            if (facility != null && bubble != null && bubble.gameObject.activeInHierarchy)
            {
                Vector3 worldPos = facility.transform.position + new Vector3(0f, 2.2f, 0f);
                Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

                // 카메라 뒤편으로 넘어간 경우 안보이게 처리
                if (screenPos.z > 0)
                {
                    bubble.transform.position = screenPos;
                }
            }
        }
    }

    public void ShowBubble(MonoBehaviour facility, Sprite icon, Vector3 worldPos)
    {
        if (facility == null) return;

        if (!activeBubbles.TryGetValue(facility, out FacilityBubbleUI bubble))
        {
            bubble = GetBubbleFromPool();
            activeBubbles[facility] = bubble;
        }

        bubble.Setup(icon, () => FacilityCollectManager.Instance.CollectAll());
        bubble.PlayPopShowAnimation();
    }

    public void HideBubble(MonoBehaviour facility)
    {
        if (facility == null) return;

        if (activeBubbles.TryGetValue(facility, out FacilityBubbleUI bubble))
        {
            activeBubbles.Remove(facility);
            ReturnToPool(bubble);
        }
    }

    public void RemoveBubble(MonoBehaviour facility)
    {
        HideBubble(facility);
    }

    public void AnimateCollectAllBubbles()
    {
        List<MonoBehaviour> keys = new List<MonoBehaviour>(activeBubbles.Keys);
        foreach (var key in keys)
        {
            if (activeBubbles.TryGetValue(key, out FacilityBubbleUI bubble))
            {
                bubble.PlayCollectAnimation(() => ReturnToPool(bubble));
            }
        }
        activeBubbles.Clear();
    }

    private FacilityBubbleUI GetBubbleFromPool()
    {
        FacilityBubbleUI bubble = null;
        if (bubblePool.Count > 0)
        {
            bubble = bubblePool.Dequeue();
        }
        else
        {
            GameObject obj = Instantiate(bubblePrefab, bubbleParentContainer != null ? bubbleParentContainer : transform);
            bubble = obj.GetComponent<FacilityBubbleUI>();
        }

        bubble.gameObject.SetActive(true);
        return bubble;
    }

    private void ReturnToPool(FacilityBubbleUI bubble)
    {
        if (bubble == null) return;
        bubble.gameObject.SetActive(false);
        if (!bubblePool.Contains(bubble))
        {
            bubblePool.Enqueue(bubble);
        }
    }
}