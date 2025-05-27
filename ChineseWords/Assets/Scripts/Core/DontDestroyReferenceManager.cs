using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace Core
{
    /// <summary>
    /// DontDestroyOnLoad对象引用管理器
    /// 解决场景切换后引用丢失的问题
    /// </summary>
    public static class DontDestroyReferenceManager
    {
        /// <summary>
        /// 在所有场景中查找组件，包括DontDestroyOnLoad
        /// </summary>
        public static T FindComponentInAllScenes<T>() where T : Component
        {
            // 1. 先在当前场景查找
            T component = Object.FindObjectOfType<T>();
            if (component != null)
                return component;

            // 2. 在DontDestroyOnLoad中查找
            return FindComponentInDontDestroy<T>();
        }

        /// <summary>
        /// 在DontDestroyOnLoad场景中查找组件
        /// </summary>
        public static T FindComponentInDontDestroy<T>() where T : Component
        {
            // 获取DontDestroyOnLoad场景中的所有根对象
            GameObject[] rootObjects = GetDontDestroyOnLoadObjects();

            foreach (var rootObj in rootObjects)
            {
                T component = rootObj.GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }

            return null;
        }

        /// <summary>
        /// 获取DontDestroyOnLoad场景中的所有根对象
        /// </summary>
        public static GameObject[] GetDontDestroyOnLoadObjects()
        {
            // 创建一个临时对象并移动到DontDestroyOnLoad
            GameObject temp = new GameObject("TempFinder");
            Object.DontDestroyOnLoad(temp);

            // 获取DontDestroyOnLoad场景
            Scene dontDestroyScene = temp.scene;

            // 获取场景中的所有根对象
            GameObject[] rootObjects = dontDestroyScene.GetRootGameObjects();

            // 销毁临时对象
            Object.DestroyImmediate(temp);

            return rootObjects;
        }

        /// <summary>
        /// 按名称在DontDestroyOnLoad中查找GameObject
        /// </summary>
        public static GameObject FindGameObjectInDontDestroy(string name)
        {
            GameObject[] rootObjects = GetDontDestroyOnLoadObjects();

            foreach (var rootObj in rootObjects)
            {
                if (rootObj.name == name)
                    return rootObj;

                // 递归查找子对象
                Transform found = rootObj.transform.Find(name);
                if (found != null)
                    return found.gameObject;
            }

            return null;
        }
    }

    /// <summary>
    /// 场景控制器基类 - 处理DontDestroyOnLoad引用问题
    /// </summary>
    public abstract class SceneControllerBase : MonoBehaviour
    {
        [Header("引用查找配置")]
        [SerializeField] protected bool autoFindReferences = true;
        [SerializeField] protected bool searchInDontDestroy = true;
        [SerializeField] protected float referenceSearchTimeout = 5f;

        protected virtual void Start()
        {
            if (autoFindReferences)
            {
                StartCoroutine(FindReferencesCoroutine());
            }
        }

        /// <summary>
        /// 查找引用的协程
        /// </summary>
        protected virtual IEnumerator FindReferencesCoroutine()
        {
            float startTime = Time.time;

            while (Time.time - startTime < referenceSearchTimeout)
            {
                if (TryFindAllReferences())
                {
                    Debug.Log($"[{GetType().Name}] 所有引用查找成功");
                    OnReferencesFound();
                    yield break;
                }

                yield return new WaitForSeconds(0.1f);
            }

            Debug.LogWarning($"[{GetType().Name}] 引用查找超时");
            OnReferencesFindTimeout();
        }

        /// <summary>
        /// 尝试查找所有必要的引用
        /// </summary>
        protected abstract bool TryFindAllReferences();

        /// <summary>
        /// 引用找到后的回调
        /// </summary>
        protected virtual void OnReferencesFound() { }

        /// <summary>
        /// 引用查找超时的回调
        /// </summary>
        protected virtual void OnReferencesFindTimeout() { }

        /// <summary>
        /// 安全查找组件
        /// </summary>
        protected T SafeFindComponent<T>() where T : Component
        {
            T component = null;

            // 1. 先在当前场景查找
            component = FindObjectOfType<T>();
            if (component != null)
                return component;

            // 2. 如果启用，在DontDestroyOnLoad中查找
            if (searchInDontDestroy)
            {
                component = DontDestroyReferenceManager.FindComponentInDontDestroy<T>();
            }

            return component;
        }

        /// <summary>
        /// 按名称安全查找GameObject
        /// </summary>
        protected GameObject SafeFindGameObject(string name)
        {
            // 1. 先在当前场景查找
            GameObject obj = GameObject.Find(name);
            if (obj != null)
                return obj;

            // 2. 如果启用，在DontDestroyOnLoad中查找
            if (searchInDontDestroy)
            {
                obj = DontDestroyReferenceManager.FindGameObjectInDontDestroy(name);
            }

            return obj;
        }
    }
}