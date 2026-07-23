using System;
using HDY.Item;
using KMS.InventoryDuped;
using MemSystem.Core;
using MemSystem.Interface;
using UnityEngine;

namespace KMS
{
    /// <summary>
    /// 카메라 정면의 멤을 감지하고 현재 장착한 캡슐 기준 포획 확률을 표시합니다.
    /// 포획 공식은 멤의 ICapturable 구현에 위임합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KMSMemCaptureFocusController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private KMSMemCaptureFocusView view;
        [SerializeField] private Transform cameraTransform;

        [Header("Focus Detection")]
        [SerializeField, Min(0.1f)] private float maxFocusDistance = 30f;
        [SerializeField] private LayerMask focusLayers = ~0;
        [SerializeField, Min(1)] private int raycastBufferSize = 32;

        [Header("Display")]
        [SerializeField] private bool showEquipCapsuleMessage = true;
        [SerializeField] private string equipCapsuleMessage = "캡슐을 장착하세요";

        private RaycastHit[] raycastHits;

        private void Reset()
        {
            inventory = GetComponent<PlayerInventory>();
            view = GetComponent<KMSMemCaptureFocusView>();
            cameraTransform = Camera.main != null ? Camera.main.transform : null;
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureRaycastBuffer();
        }

        private void OnDisable()
        {
            view?.Hide();
        }

        private void Update()
        {
            ResolveReferences();
            EnsureRaycastBuffer();

            if (view == null)
            {
                return;
            }

            Transform focusCamera = ResolveCameraTransform();
            Mem focusedMem = focusCamera != null ? FindFocusedMem(focusCamera) : null;
            if (focusedMem == null || !focusedMem.IsActive)
            {
                view.Hide();
                return;
            }

            string displayName = focusedMem.Stats != null && !string.IsNullOrWhiteSpace(focusedMem.Stats.MemName)
                ? focusedMem.Stats.MemName
                : focusedMem.name;

            ItemData capsuleData = ResolveSelectedCapsule();
            if (capsuleData == null)
            {
                if (showEquipCapsuleMessage)
                {
                    view.ShowMessage(focusedMem, displayName, equipCapsuleMessage);
                }
                else
                {
                    view.Hide();
                }

                return;
            }

            ICapturable capturable = focusedMem;
            float captureRate = capturable.GetCaptureRate((int)capsuleData.ItemClass);
            view.ShowCaptureRate(focusedMem, displayName, captureRate);
        }

        private void ResolveReferences()
        {
            if (inventory == null)
            {
                inventory = GetComponent<PlayerInventory>();
            }

            if (view == null)
            {
                view = GetComponent<KMSMemCaptureFocusView>();
            }
        }

        private Transform ResolveCameraTransform()
        {
            if (cameraTransform != null && cameraTransform.gameObject.activeInHierarchy)
            {
                return cameraTransform;
            }

            Camera mainCamera = Camera.main;
            cameraTransform = mainCamera != null ? mainCamera.transform : null;
            return cameraTransform;
        }

        private ItemData ResolveSelectedCapsule()
        {
            if (inventory == null)
            {
                return null;
            }

            ItemStack selectedSlot = inventory.GetSelectedQuickSlot();
            if (selectedSlot == null || selectedSlot.IsEmpty)
            {
                return null;
            }

            ItemData itemData = inventory.FindItemData(selectedSlot.itemId);
            return itemData != null && itemData.Category == ItemCategory.Capsule ? itemData : null;
        }

        private Mem FindFocusedMem(Transform focusCamera)
        {
            Ray ray = new Ray(focusCamera.position, focusCamera.forward);
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                raycastHits,
                maxFocusDistance,
                focusLayers,
                QueryTriggerInteraction.Collide);

            if (hitCount <= 0)
            {
                return null;
            }

            Array.Sort(raycastHits, 0, hitCount, RaycastHitDistanceComparer.Instance);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = raycastHits[i].collider;
                if (hitCollider == null || IsPlayerCollider(hitCollider))
                {
                    continue;
                }

                Mem mem = hitCollider.GetComponentInParent<Mem>();
                if (mem != null)
                {
                    return mem.IsActive ? mem : null;
                }

                // 감지용 Trigger 볼륨은 시야를 가리지 않지만 실제 Collider는 가림막으로 취급합니다.
                if (!hitCollider.isTrigger)
                {
                    return null;
                }
            }

            return null;
        }

        private bool IsPlayerCollider(Collider candidate)
        {
            Transform candidateTransform = candidate.transform;
            return candidateTransform == transform || candidateTransform.IsChildOf(transform);
        }

        private void EnsureRaycastBuffer()
        {
            int requiredSize = Mathf.Max(1, raycastBufferSize);
            if (raycastHits == null || raycastHits.Length != requiredSize)
            {
                raycastHits = new RaycastHit[requiredSize];
            }
        }

        private sealed class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

            public int Compare(RaycastHit left, RaycastHit right)
            {
                return left.distance.CompareTo(right.distance);
            }
        }
    }
}
