using GH.Loading;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoadiing : MonoBehaviour
{
    public void loadScene(string SceneName)
    {
        if (SceneManager.GetActiveScene().name == SceneName)
        {
            return;
        }

        if (WayPointManager.Instance != null
            && WayPointManager.Instance.IsTerritorySceneName(SceneName))
        {
            WayPointManager.Instance.TryTravelToTerritory();
            return;
        }

        if (LoadingManager.Instance == null) return;
        LoadingManager.Instance.LoadScene(SceneName, string.Empty);
    }
}
