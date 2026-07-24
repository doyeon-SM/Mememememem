using UnityEngine;
using UnityEngine.InputSystem;
using MemSystem.Core;

namespace Pikachu.Player
{
    public class PlayerTestAttack : MonoBehaviour
    {
        void Update()
        {
            // 새 Input System의 마우스 입력 확인
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) 
            {
                // 화면 중앙(또는 마우스 커서 위치)에서 안쪽으로 레이저 쏘기
                Vector2 mousePosition = Mouse.current.position.ReadValue();
                Ray ray = Camera.main.ScreenPointToRay(mousePosition);
                
                // 15m 거리 안에서 무언가 맞았다면
                if (Physics.Raycast(ray, out RaycastHit hit, 15f))
                {
                    // 맞은 물체(또는 부모)에 Mem 컴포넌트가 있는지 확인
                    Mem mem = hit.collider.GetComponentInParent<Mem>();
                    if (mem != null)
                    {
                        Debug.Log($"[테스트] 플레이어가 {mem.name} 을(를) 공격함! (데미지 1)");
                        // 멤에게 데미지 1 입히기 (테스트용)
                        // 레이가 날아온 쪽(카메라/공격 지점)을 공격자 위치로 넘겨 "맞은 방향"으로 밀리게 한다.
                        mem.TakeDamage(1, ray.origin);
                    }
                }
            }
        }
    }
}
