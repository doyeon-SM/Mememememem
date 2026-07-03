using UnityEngine;

public class WayPointStone : MonoBehaviour
{
    [SerializeField] private Vector3 spawnPoistion;

    private bool isActive = false;

    public void SetActive(bool active)
    {
        isActive = active;
    }
}
