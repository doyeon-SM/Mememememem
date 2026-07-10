using UnityEngine;
using UnityEngine.UI;
using HDY.Shop;

namespace HDY.UI
{
    /// <summary>
    /// [임시/테스트용] 상점 시스템을 테스트하기 위한 임시 버튼 핸들러. 지정된 상점 하나를 여는 것만
    /// 담당한다. 나중에 실제 상점 NPC 상호작용(대화, 트리거 진입 등)이 만들어지면 이 스크립트는 지우고
    /// 그쪽에서 ShopUI.Instance.Open(shop)을 직접 호출하면 된다.
    ///
    /// 같은 오브젝트의 Button.onClick을 Awake에서 스스로 연결하므로, 인스펙터에서 OnClick()에 별도로
    /// 연결하지 않아도 된다 - shopToOpen만 채워두면 된다.
    /// </summary>
    public class ShopTestOpenButton : MonoBehaviour
    {
        [Tooltip("이 버튼을 누르면 열릴 상점(테스트용 - 예: 마트).")]
        [SerializeField] private ShopData shopToOpen;
        [Tooltip("비워두면 같은 오브젝트의 Button 컴포넌트를 자동으로 찾는다.")]
        [SerializeField] private Button button;

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();

            if (button == null)
            {
                Debug.LogWarning("[ShopTestOpenButton] Button 컴포넌트를 찾을 수 없습니다.", this);
                return;
            }

            button.onClick.AddListener(OpenShop);
        }

        /// <summary>버튼 클릭 시 호출된다. Button.onClick에 직접 연결해서 써도 된다.</summary>
        public void OpenShop()
        {
            if (shopToOpen == null)
            {
                Debug.LogWarning("[ShopTestOpenButton] shopToOpen이 비어있습니다.", this);
                return;
            }

            if (ShopUI.Instance == null)
            {
                Debug.LogWarning("[ShopTestOpenButton] 씬에서 ShopUI를 찾을 수 없습니다.", this);
                return;
            }

            ShopUI.Instance.Open(shopToOpen);
        }
    }
}
