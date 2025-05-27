using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace Core
{
    /// <summary>
    /// DontDestroyOnLoad�������ù�����
    /// ��������л������ö�ʧ������
    /// </summary>
    public static class DontDestroyReferenceManager
    {
        /// <summary>
        /// �����г����в������������DontDestroyOnLoad
        /// </summary>
        public static T FindComponentInAllScenes<T>() where T : Component
        {
            // 1. ���ڵ�ǰ��������
            T component = Object.FindObjectOfType<T>();
            if (component != null)
                return component;

            // 2. ��DontDestroyOnLoad�в���
            return FindComponentInDontDestroy<T>();
        }

        /// <summary>
        /// ��DontDestroyOnLoad�����в������
        /// </summary>
        public static T FindComponentInDontDestroy<T>() where T : Component
        {
            // ��ȡDontDestroyOnLoad�����е����и�����
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
        /// ��ȡDontDestroyOnLoad�����е����и�����
        /// </summary>
        public static GameObject[] GetDontDestroyOnLoadObjects()
        {
            // ����һ����ʱ�����ƶ���DontDestroyOnLoad
            GameObject temp = new GameObject("TempFinder");
            Object.DontDestroyOnLoad(temp);

            // ��ȡDontDestroyOnLoad����
            Scene dontDestroyScene = temp.scene;

            // ��ȡ�����е����и�����
            GameObject[] rootObjects = dontDestroyScene.GetRootGameObjects();

            // ������ʱ����
            Object.DestroyImmediate(temp);

            return rootObjects;
        }

        /// <summary>
        /// ��������DontDestroyOnLoad�в���GameObject
        /// </summary>
        public static GameObject FindGameObjectInDontDestroy(string name)
        {
            GameObject[] rootObjects = GetDontDestroyOnLoadObjects();

            foreach (var rootObj in rootObjects)
            {
                if (rootObj.name == name)
                    return rootObj;

                // �ݹ�����Ӷ���
                Transform found = rootObj.transform.Find(name);
                if (found != null)
                    return found.gameObject;
            }

            return null;
        }
    }

    /// <summary>
    /// �������������� - ����DontDestroyOnLoad��������
    /// </summary>
    public abstract class SceneControllerBase : MonoBehaviour
    {
        [Header("���ò�������")]
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
        /// �������õ�Э��
        /// </summary>
        protected virtual IEnumerator FindReferencesCoroutine()
        {
            float startTime = Time.time;

            while (Time.time - startTime < referenceSearchTimeout)
            {
                if (TryFindAllReferences())
                {
                    Debug.Log($"[{GetType().Name}] �������ò��ҳɹ�");
                    OnReferencesFound();
                    yield break;
                }

                yield return new WaitForSeconds(0.1f);
            }

            Debug.LogWarning($"[{GetType().Name}] ���ò��ҳ�ʱ");
            OnReferencesFindTimeout();
        }

        /// <summary>
        /// ���Բ������б�Ҫ������
        /// </summary>
        protected abstract bool TryFindAllReferences();

        /// <summary>
        /// �����ҵ���Ļص�
        /// </summary>
        protected virtual void OnReferencesFound() { }

        /// <summary>
        /// ���ò��ҳ�ʱ�Ļص�
        /// </summary>
        protected virtual void OnReferencesFindTimeout() { }

        /// <summary>
        /// ��ȫ�������
        /// </summary>
        protected T SafeFindComponent<T>() where T : Component
        {
            T component = null;

            // 1. ���ڵ�ǰ��������
            component = FindObjectOfType<T>();
            if (component != null)
                return component;

            // 2. ������ã���DontDestroyOnLoad�в���
            if (searchInDontDestroy)
            {
                component = DontDestroyReferenceManager.FindComponentInDontDestroy<T>();
            }

            return component;
        }

        /// <summary>
        /// �����ư�ȫ����GameObject
        /// </summary>
        protected GameObject SafeFindGameObject(string name)
        {
            // 1. ���ڵ�ǰ��������
            GameObject obj = GameObject.Find(name);
            if (obj != null)
                return obj;

            // 2. ������ã���DontDestroyOnLoad�в���
            if (searchInDontDestroy)
            {
                obj = DontDestroyReferenceManager.FindGameObjectInDontDestroy(name);
            }

            return obj;
        }
    }
}