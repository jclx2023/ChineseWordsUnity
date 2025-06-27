using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core;
using Core.Network;
using System.Collections;
using Classroom.Player;

namespace UI.Blackboard
{
    /// <summary>
    /// 黑板显示管理器 - 基于手动配置的LayoutGroup系统
    /// 专注于显示逻辑，所有布局组件在Unity编辑器中预先配置
    /// </summary>
    public class BlackboardDisplayManager : MonoBehaviour
    {
        [Header("预配置的UI组件引用")]
        [SerializeField] private Canvas blackboardCanvas;
        [SerializeField] private RectTransform blackboardArea;
        [SerializeField] private RectTransform questionArea;
        [SerializeField] private RectTransform optionsArea;
        [SerializeField] private RectTransform statusArea;
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject optionTemplate;

        [Header("显示配置")]
        [SerializeField] private Color questionColor = Color.black;
        [SerializeField] private Color optionColor = Color.black;
        [SerializeField] private Color statusColor = Color.blue;

        public static BlackboardDisplayManager Instance { get; private set; }

        // 动态创建的选项文本
        private TextMeshProUGUI[] optionTexts;
        private NetworkQuestionData currentQuestion;
        private bool isDisplaying = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeBlackboard();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 延迟查找摄像机，确保玩家摄像机已经生成
            StartCoroutine(FindAndSetCamera());
        }

        /// <summary>
        /// 查找并设置动态生成的玩家摄像机
        /// </summary>
        private IEnumerator FindAndSetCamera()
        {
            Camera playerCamera = null;
            float timeoutCounter = 0f;
            float timeout = 5f; // 5秒超时

            // 循环查找玩家摄像机
            while (playerCamera == null && timeoutCounter < timeout)
            {
                // 尝试多种可能的摄像机查找方式
                playerCamera = FindPlayerCamera();

                if (playerCamera == null)
                {
                    timeoutCounter += 0.1f;
                    yield return new WaitForSeconds(0.1f);
                }
            }

            // 设置找到的摄像机
            if (playerCamera != null)
            {
                SetBlackboardCamera(playerCamera);
            }
            else
            {
                Debug.LogWarning("[BlackboardDisplayManager] 未能找到玩家摄像机，使用主摄像机作为备用");
                SetBlackboardCamera(Camera.main);
            }
        }

        /// <summary>
        /// 查找玩家摄像机的多种方式（基于PlayerCameraController的逻辑优化）
        /// </summary>
        private Camera FindPlayerCamera()
        {
            // 方式1: 通过PlayerCameraController组件查找（最可靠）
            var playerCameraControllers = FindObjectsOfType<PlayerCameraController>();
            foreach (var controller in playerCameraControllers)
            {
                if (controller.IsLocalPlayer && controller.PlayerCamera != null)
                {
                    Debug.Log($"[BlackboardDisplayManager] 通过PlayerCameraController找到摄像机: {controller.PlayerCamera.name}");
                    return controller.PlayerCamera;
                }
            }

            // 方式2: 查找MainCamera标签的摄像机（PlayerCameraController会设置此标签）
            GameObject mainCameraObj = GameObject.FindWithTag("MainCamera");
            if (mainCameraObj != null)
            {
                var camera = mainCameraObj.GetComponent<Camera>();
                if (camera != null && camera.enabled)
                {
                    Debug.Log($"[BlackboardDisplayManager] 通过MainCamera标签找到摄像机: {camera.name}");
                    return camera;
                }
            }

            // 方式3: 查找名称为"PlayerCamera"的摄像机
            Camera[] allCameras = FindObjectsOfType<Camera>();
            foreach (var cam in allCameras)
            {
                if (cam.gameObject.name == "PlayerCamera" && cam.enabled)
                {
                    Debug.Log($"[BlackboardDisplayManager] 通过名称找到PlayerCamera: {cam.name}");
                    return cam;
                }
            }

            // 方式4: 查找包含"Player"、"Local"等关键词的摄像机
            foreach (var cam in allCameras)
            {
                if ((cam.gameObject.name.Contains("Player") ||
                     cam.gameObject.name.Contains("Local") ||
                     cam.gameObject.name.Contains("FPS")) &&
                     cam.enabled)
                {
                    Debug.Log($"[BlackboardDisplayManager] 通过关键词找到摄像机: {cam.name}");
                    return cam;
                }
            }

            // 方式5: 查找除了默认主摄像机之外的第一个激活摄像机
            foreach (var cam in allCameras)
            {
                if (cam != Camera.main && cam.enabled && cam.gameObject.activeInHierarchy)
                {
                    Debug.Log($"[BlackboardDisplayManager] 找到备用摄像机: {cam.name}");
                    return cam;
                }
            }

            return null;
        }

        /// <summary>
        /// 设置黑板Canvas的摄像机
        /// </summary>
        private void SetBlackboardCamera(Camera camera)
        {
            if (blackboardCanvas != null && camera != null)
            {
                blackboardCanvas.worldCamera = camera;
                Debug.Log($"[BlackboardDisplayManager] 已设置摄像机: {camera.name}");
            }
        }

        /// <summary>
        /// 手动设置摄像机（供外部调用，特别是PlayerCameraController）
        /// </summary>
        public void SetCamera(Camera camera)
        {
            SetBlackboardCamera(camera);
        }

        /// <summary>
        /// 注册玩家摄像机控制器（推荐方式）
        /// </summary>
        public void RegisterPlayerCameraController(PlayerCameraController cameraController)
        {
            if (cameraController != null && cameraController.IsLocalPlayer && cameraController.PlayerCamera != null)
            {
                SetBlackboardCamera(cameraController.PlayerCamera);
                Debug.Log($"[BlackboardDisplayManager] 已注册玩家摄像机控制器: {cameraController.PlayerCamera.name}");
            }
        }

        /// <summary>
        /// 初始化黑板（验证组件引用）
        /// </summary>
        private void InitializeBlackboard()
        {
            // 验证必要的组件引用
            if (blackboardCanvas == null)
            {
                Debug.LogError("[BlackboardDisplayManager] BlackboardCanvas引用缺失");
                return;
            }

            if (questionText == null)
            {
                Debug.LogError("[BlackboardDisplayManager] QuestionText引用缺失");
                return;
            }

            if (statusText == null)
            {
                Debug.LogError("[BlackboardDisplayManager] StatusText引用缺失");
                return;
            }

            if (optionTemplate == null)
            {
                Debug.LogError("[BlackboardDisplayManager] OptionTemplate引用缺失");
                return;
            }

            // 确保模板处于非激活状态
            optionTemplate.SetActive(false);

            // 应用颜色设置
            ApplyColorSettings();

            // 初始清空显示
            ClearDisplay();

            Debug.Log("[BlackboardDisplayManager] 初始化完成");
        }

        /// <summary>
        /// 应用颜色设置
        /// </summary>
        private void ApplyColorSettings()
        {
            if (questionText != null)
            {
                questionText.color = questionColor;
            }

            if (statusText != null)
            {
                statusText.color = statusColor;
            }
        }

        /// <summary>
        /// 创建选项文本组件
        /// </summary>
        private void CreateOptionTexts(int count)
        {
            // 清理旧的选项文本
            if (optionTexts != null)
            {
                foreach (var optionText in optionTexts)
                {
                    if (optionText != null)
                    {
                        DestroyImmediate(optionText.gameObject);
                    }
                }
            }

            // 创建新的选项文本
            optionTexts = new TextMeshProUGUI[count];

            for (int i = 0; i < count; i++)
            {
                // 从模板实例化
                GameObject optionObj = Instantiate(optionTemplate, optionsArea);
                optionObj.name = $"Option{i + 1}";
                optionObj.SetActive(true);

                var optionText = optionObj.GetComponent<TextMeshProUGUI>();
                optionTexts[i] = optionText;

                // 应用颜色设置
                if (optionText != null)
                {
                    optionText.color = optionColor;
                }
            }
        }

        /// <summary>
        /// 显示题目
        /// </summary>
        public void DisplayQuestion(NetworkQuestionData questionData)
        {
            if (questionData == null)
            {
                ClearDisplay();
                return;
            }

            currentQuestion = questionData;
            isDisplaying = true;

            // 显示题干
            if (questionText != null)
            {
                questionText.text = questionData.questionText;
                questionArea.gameObject.SetActive(true);
            }

            // 根据题型显示内容
            switch (questionData.questionType)
            {
                case QuestionType.ExplanationChoice:
                case QuestionType.SimularWordChoice:
                    DisplayChoiceQuestion(questionData);
                    break;

                case QuestionType.SentimentTorF:
                case QuestionType.UsageTorF:
                    DisplayTrueFalseQuestion(questionData);
                    break;

                default:
                    DisplayFillQuestion(questionData);
                    break;
            }

            // 显示状态
            UpdateDisplayStatus("请作答", statusColor);

            // 强制更新布局
            StartCoroutine(RefreshLayout());
        }

        /// <summary>
        /// 显示选择题
        /// </summary>
        private void DisplayChoiceQuestion(NetworkQuestionData questionData)
        {
            if (questionData.options == null || questionData.options.Length == 0)
            {
                optionsArea.gameObject.SetActive(false);
                return;
            }

            // 创建选项文本
            CreateOptionTexts(questionData.options.Length);

            // 设置选项内容
            for (int i = 0; i < optionTexts.Length; i++)
            {
                optionTexts[i].text = $"{(char)('A' + i)}. {questionData.options[i]}";
            }

            optionsArea.gameObject.SetActive(true);
        }

        /// <summary>
        /// 显示判断题
        /// </summary>
        private void DisplayTrueFalseQuestion(NetworkQuestionData questionData)
        {
            CreateOptionTexts(2);

            optionTexts[0].text = "A. 正确";
            optionTexts[1].text = "B. 错误";

            optionsArea.gameObject.SetActive(true);
        }

        /// <summary>
        /// 显示填空题
        /// </summary>
        private void DisplayFillQuestion(NetworkQuestionData questionData)
        {
            // 隐藏选项区域，填空题通过沉浸式答题界面输入
            optionsArea.gameObject.SetActive(false);
        }

        /// <summary>
        /// 更新显示状态
        /// </summary>
        public void UpdateDisplayStatus(string status, Color color)
        {
            if (statusText != null)
            {
                statusText.text = status;
                statusText.color = color;
                statusArea.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 清空显示内容
        /// </summary>
        public void ClearDisplay()
        {
            currentQuestion = null;
            isDisplaying = false;

            if (questionText != null)
            {
                questionText.text = "";
            }

            if (statusText != null)
            {
                statusText.text = "";
            }

            // 清理选项文本
            if (optionTexts != null)
            {
                foreach (var optionText in optionTexts)
                {
                    if (optionText != null)
                    {
                        DestroyImmediate(optionText.gameObject);
                    }
                }
                optionTexts = null;
            }

            // 隐藏所有区域
            questionArea.gameObject.SetActive(false);
            optionsArea.gameObject.SetActive(false);
            statusArea.gameObject.SetActive(false);
        }

        /// <summary>
        /// 强制刷新布局
        /// </summary>
        private IEnumerator RefreshLayout()
        {
            yield return new WaitForEndOfFrame();

            // 强制更新Canvas布局
            Canvas.ForceUpdateCanvases();

            // 重建所有布局
            if (blackboardArea != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(blackboardArea);
            }

            if (optionsArea != null && optionsArea.gameObject.activeInHierarchy)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(optionsArea);
            }
        }

        /// <summary>
        /// 检查是否正在显示题目
        /// </summary>
        public bool IsDisplaying => isDisplaying;

        /// <summary>
        /// 获取当前显示的题目
        /// </summary>
        public NetworkQuestionData CurrentQuestion => currentQuestion;
    }
}