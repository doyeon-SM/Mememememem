using UnityEngine;

[RequireComponent(typeof(Camera))]
public class TreeDistanceCulling : MonoBehaviour
{
    [SerializeField] private string treeLayerName = "Tree";
    [SerializeField] private float cullDistance = 100f;

    private void Awake()
    {
        Camera cam = GetComponent<Camera>();
        int treeLayer = LayerMask.NameToLayer(treeLayerName);

        if (treeLayer < 0)
        {
            Debug.LogError($"'{treeLayerName}' 레이어가 없습니다.");
            return;
        }

        float[] distances = cam.layerCullDistances;
        distances[treeLayer] = cullDistance;

        cam.layerCullDistances = distances;
        cam.layerCullSpherical = true;
    }
}