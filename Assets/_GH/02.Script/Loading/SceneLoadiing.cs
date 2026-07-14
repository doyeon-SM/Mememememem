using GH.Loading;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadiing : MonoBehaviour
{
    public void loadScene(string SceneName)
    {
        if (LoadingManager.Instance == null) return;
        if (SceneManager.GetActiveScene().name == SceneName)
        {
            return;
        }
        LoadingManager.Instance.LoadScene(SceneName, string.Empty);
    }
}
