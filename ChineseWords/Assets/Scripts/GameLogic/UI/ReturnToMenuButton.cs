using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLogic.UI
{
    /// <summary>
    /// ���ڡ��������˵�����ť�ϣ����ʱ���� MainMenu ����
    /// </summary>
    public class ReturnToMenuButton : MonoBehaviour
    {
        // Inspector ��ָ��Ҫ���صĳ�������Ĭ�� "MainMenu"
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        /// <summary>
        /// �󶨵� Button �� OnClick
        /// </summary>
        public void OnClick_ReturnMainMenu()
        {
            // ��������һЩ�����������Ч����
            // Ȼ��������˵�����
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
