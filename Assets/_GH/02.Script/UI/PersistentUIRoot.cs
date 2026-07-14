using UnityEngine;

public class PersistentUIRoot : MonoBehaviour
{
    private static PersistentUIRoot instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
