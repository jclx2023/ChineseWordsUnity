using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLogic.UI
{
    /// <summary>
    /// 挂在“返回主菜单”按钮上，点击时加载 MainMenu 场景
    /// </summary>
    public class ReturnToMenuButton : MonoBehaviour
    {
        // Inspector 可指定要加载的场景名，默认 "MainMenu"
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        /// <summary>
        /// 绑定到 Button 的 OnClick
        /// </summary>
        public void OnClick_ReturnMainMenu()
        {
            // …可先做一些数据清理或音效播放
            // 然后加载主菜单场景
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
