using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace Core
{
    /// <summary>
    /// �����л������� - ��ֹ�ظ��л��ĵ���������
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        [Header("�����л�����")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private float transitionDelay = 2f;

        // ״̬����
        private static bool isTransitioning = false;
        private static string targetScene = "";

        // �¼�
        public static System.Action<string> OnSceneTransitionStarted;
        public static System.Action<string> OnSceneTransitionCompleted;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LogDebug("�����л���������ʼ�����");
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// ��ȫ�ĳ����л����� - ��ֹ�ظ��л�
        /// </summary>
        public bool TransitionToScene(string sceneName, float delay = 0f, string caller = "Unknown")
        {
            // ����Ƿ������л���
            if (isTransitioning)
            {
                LogDebug($"�����л����ܾ��������л��С������ߣ�{caller}��Ŀ�곡����{sceneName}����ǰĿ�꣺{targetScene}");
                return false;
            }

            // ���Ŀ�곡���Ƿ��뵱ǰ������ͬ
            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene.Equals(sceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                LogDebug($"�����л����ܾ�������Ŀ�곡���С������ߣ�{caller}��������{sceneName}");
                return false;
            }

            LogDebug($"��ʼ�����л���{currentScene} �� {sceneName}�������ߣ�{caller}���ӳ٣�{delay}��");

            // �����л�״̬
            isTransitioning = true;
            targetScene = sceneName;

            // �����л���ʼ�¼�
            OnSceneTransitionStarted?.Invoke(sceneName);

            // ��ʼ�л�Э��
            StartCoroutine(TransitionCoroutine(sceneName, delay));

            return true;
        }

        /// <summary>
        /// �����л�Э��
        /// </summary>
        private IEnumerator TransitionCoroutine(string sceneName, float delay)
        {
            // �ȴ�ָ���ӳ�
            if (delay > 0f)
            {
                LogDebug($"�ȴ� {delay} ����л��� {sceneName}");
                yield return new WaitForSeconds(delay);
            }

            try
            {
                LogDebug($"ִ�г����л�����{sceneName}");

                // ִ�г����л�
                SceneManager.LoadScene(sceneName);

                // �����л�����¼�
                OnSceneTransitionCompleted?.Invoke(sceneName);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"�����л�ʧ�ܣ�{e.Message}");

                // ����״̬����������
                ResetTransitionState();
            }
        }

        /// <summary>
        /// �����л�״̬�����ڴ���ָ���
        /// </summary>
        public static void ResetTransitionState()
        {
            isTransitioning = false;
            targetScene = "";
            Debug.Log("[SceneTransitionManager] �л�״̬������");
        }

        /// <summary>
        /// ����Ƿ������л�����
        /// </summary>
        public static bool IsTransitioning => isTransitioning;

        /// <summary>
        /// ��ȡĿ�곡������
        /// </summary>
        public static string GetTargetScene() => targetScene;

        /// <summary>
        /// ǿ���л�����������״̬��飩
        /// </summary>
        public void ForceTransitionToScene(string sceneName, string caller = "Force")
        {
            LogDebug($"ǿ�Ƴ����л���{sceneName}�������ߣ�{caller}");

            // ����״̬
            ResetTransitionState();

            // ֱ���л�
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// ������־
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SceneTransitionManager] {message}");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #region ��̬��ݷ���

        /// <summary>
        /// ��̬�������л�����Ϸ����
        /// </summary>
        public static bool SwitchToGameScene(string caller = "Unknown")
        {
            if (Instance != null)
                return Instance.TransitionToScene("NetworkGameScene", 2f, caller);

            Debug.LogError("SceneTransitionManagerʵ��������");
            return false;
        }

        /// <summary>
        /// ��̬�������������˵�
        /// </summary>
        public static bool ReturnToMainMenu(string caller = "Unknown")
        {
            if (Instance != null)
                return Instance.TransitionToScene("MainMenuScene", 0f, caller);

            Debug.LogError("SceneTransitionManagerʵ��������");
            return false;
        }

        #endregion

        #region ���Է���

        [ContextMenu("��ʾ�л�״̬")]
        public void ShowTransitionStatus()
        {
            Debug.Log($"=== �����л�״̬ ===");
            Debug.Log($"�����л���{isTransitioning}");
            Debug.Log($"Ŀ�곡����{targetScene}");
            Debug.Log($"��ǰ������{SceneManager.GetActiveScene().name}");
        }

        [ContextMenu("�����л�״̬")]
        public void DebugResetState()
        {
            ResetTransitionState();
        }

        #endregion
    }
}