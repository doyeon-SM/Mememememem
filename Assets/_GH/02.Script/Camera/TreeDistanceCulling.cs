using UnityEngine;

[RequireComponent(typeof(Camera))]
public class TreeDistanceCulling : MonoBehaviour
{
    [SerializeField] private string treeLayerName = "Tree";
    [SerializeField] private float cullDistance = 100f;

    private Camera cachedCamera;

    private void Awake()
    {
        ApplyCullDistance();
    }

    public void SetCullDistance(float distance)
    {
        cullDistance = Mathf.Max(0f, distance);
        ApplyCullDistance();
    }

    public static void ApplyTreeDistanceToAll(float treeDistance)
    {
        TreeDistanceCulling[] controllers = FindObjectsByType<TreeDistanceCulling>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
            {
                controllers[i].SetCullDistance(treeDistance);
            }
        }
    }

    private void ApplyCullDistance()
    {
        if (cachedCamera == null)
        {
            cachedCamera = GetComponent<Camera>();
        }

        int treeLayer = LayerMask.NameToLayer(treeLayerName);

        if (treeLayer < 0)
        {
            Debug.LogError($"'{treeLayerName}' 레이어가 없습니다.");
            return;
        }

        float[] distances = cachedCamera.layerCullDistances;
        distances[treeLayer] = Mathf.Max(0f, cullDistance);

        cachedCamera.layerCullDistances = distances;
    }
}
