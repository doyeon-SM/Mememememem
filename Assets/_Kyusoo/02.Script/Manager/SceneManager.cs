using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneChanger : MonoBehaviour
{
    [Header("설정할 버튼과 이동할 씬 이름")]
    [SerializeField] private Button targetButton; 
    [SerializeField] private string sceneName;    

    void Start()
    {
        if (targetButton != null)
        {
            // 버튼 클릭 이벤트에 씬 전환 함수 연결
            targetButton.onClick.AddListener(ChangeScene);
        }
        else
        {
        }
    }

    private void ChangeScene()
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
        }
    }
}