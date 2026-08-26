using System.Collections.Generic;
using UnityEngine;

namespace KMS.Combat
{
    /// <summary>
    /// [멤] 기본 공격(좌클릭 원거리) 전용 투사체 오브젝트 풀. Prefab 참조를 키로 큐를 관리하며,
    /// prefab마다 독립된 풀을 갖는다.
    ///
    /// 스킬 발동은 사용 빈도가 낮고 "어떤 무기를 쓰든 항상 동일한 효과"만 보장하면 되므로 풀링하지
    /// 않고 기존처럼 Instantiate/Destroy를 그대로 쓴다(PlayerWeaponSkillController의 스킬 발사
    /// 경로 참고) - 기본 공격만 초당 발사 빈도가 높아 재활용 이득이 크다는 게 이유.
    ///
    /// 풀에 보관된 오브젝트들은 씬 전환에도 살아남도록 DontDestroyOnLoad 루트 밑에 둔다.
    /// </summary>
    public static class ProjectilePool
    {
        private static readonly Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();
        private static Transform poolRoot;

        private static Transform GetRoot()
        {
            if (poolRoot == null)
            {
                var rootObject = new GameObject("[ProjectilePool]");
                Object.DontDestroyOnLoad(rootObject);
                poolRoot = rootObject.transform;
            }

            return poolRoot;
        }

        /// <summary>prefab에 대응하는 풀에서 하나 꺼내 위치/회전을 설정하고 활성화해서 반환한다. 없으면 새로 만든다.</summary>
        public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            if (!pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                pools[prefab] = queue;
            }

            GameObject instance = null;
            while (queue.Count > 0)
            {
                instance = queue.Dequeue();
                if (instance != null) break;
            }

            if (instance == null)
            {
                instance = Object.Instantiate(prefab, GetRoot());
            }

            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            return instance;
        }

        /// <summary>사용이 끝난 인스턴스를 비활성화해 prefab에 대응하는 풀로 되돌린다.</summary>
        public static void Release(GameObject prefab, GameObject instance)
        {
            if (prefab == null || instance == null) return;

            instance.SetActive(false);
            instance.transform.SetParent(GetRoot());

            if (!pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                pools[prefab] = queue;
            }

            queue.Enqueue(instance);
        }
    }
}
