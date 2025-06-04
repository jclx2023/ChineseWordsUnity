using UnityEngine;
using System.Collections.Generic;
using Core;
using Core.Network;
using GameLogic.FillBlank;
using System.Linq;

namespace Core.Network
{
    /// <summary>
    /// ����֤������
    /// ר�Ÿ���������͵Ĵ���֤�߼�
    /// </summary>
    public class AnswerValidationManager
    {
        [Header("��������")]
        private bool enableDebugLogs = true;

        [Header("��֤����")]
        private bool strictValidation = false; // �Ƿ������ϸ���֤ģʽ
        private bool caseSensitive = false;    // �Ƿ��Сд����

        // ��֤������棨��ѡ�Ż���
        private Dictionary<string, bool> validationCache;
        private int maxCacheSize = 1000;

        // �������͹���������
        private IdiomChainQuestionManager idiomChainManager;
        private QuestionDataService questionDataService;

        // �¼�����
        public System.Action<QuestionType, string, bool> OnAnswerValidated; // questionType, answer, isCorrect
        public System.Action<string> OnValidationError; // error message

        /// <summary>
        /// ��֤������ݽṹ
        /// </summary>
        public class ValidationResult
        {
            public bool isCorrect;
            public string providedAnswer;
            public string correctAnswer;
            public QuestionType questionType;
            public string errorMessage;
            public float validationTime;

            public ValidationResult(bool correct, string provided, string expected, QuestionType type)
            {
                isCorrect = correct;
                providedAnswer = provided;
                correctAnswer = expected;
                questionType = type;
                errorMessage = "";
                validationTime = Time.time;
            }
        }

        /// <summary>
        /// ���캯��
        /// </summary>
        public AnswerValidationManager()
        {
            validationCache = new Dictionary<string, bool>();
            LogDebug("AnswerValidationManager ʵ���Ѵ���");
        }

        #region ��ʼ��

        /// <summary>
        /// ��ʼ������֤������
        /// </summary>
        /// <param name="dataService">��Ŀ���ݷ���</param>
        /// <param name="enableCache">�Ƿ�������֤����</param>
        public void Initialize(QuestionDataService dataService = null, bool enableCache = true)
        {
            LogDebug("��ʼ��AnswerValidationManager...");

            questionDataService = dataService ?? QuestionDataService.Instance;

            if (enableCache)
            {
                validationCache = new Dictionary<string, bool>();
            }
            else
            {
                validationCache = null;
            }

            LogDebug($"AnswerValidationManager��ʼ����� - ����: {(enableCache ? "����" : "����")}");
        }

        #endregion

        #region ����֤����

        /// <summary>
        /// ��֤�𰸣�����ڷ�����
        /// </summary>
        /// <param name="answer">��Ҵ�</param>
        /// <param name="question">��Ŀ����</param>
        /// <returns>��֤���</returns>
        public ValidationResult ValidateAnswer(string answer, NetworkQuestionData question)
        {
            if (question == null)
            {
                var error = "��Ŀ����Ϊ��";
                LogDebug($"��֤ʧ��: {error}");
                OnValidationError?.Invoke(error);
                return new ValidationResult(false, answer, "", QuestionType.HardFill) { errorMessage = error };
            }

            LogDebug($"��ʼ��֤��: [{answer}] ����: {question.questionType}");

            ValidationResult result;

            try
            {
                // ������Ŀ����ѡ����֤��ʽ
                switch (question.questionType)
                {
                    case QuestionType.IdiomChain:
                        result = ValidateIdiomChainAnswer(answer, question);
                        break;

                    case QuestionType.HardFill:
                        result = ValidateHardFillAnswer(answer, question);
                        break;

                    case QuestionType.SoftFill:
                        result = ValidateSoftFillAnswer(answer, question);
                        break;

                    case QuestionType.TextPinyin:
                        result = ValidatePinyinAnswer(answer, question);
                        break;

                    case QuestionType.ExplanationChoice:
                    case QuestionType.SimularWordChoice:
                        result = ValidateChoiceAnswer(answer, question);
                        break;

                    case QuestionType.SentimentTorF:
                    case QuestionType.UsageTorF:
                        result = ValidateTrueFalseAnswer(answer, question);
                        break;

                    case QuestionType.HandWriting:
                        result = ValidateHandwritingAnswer(answer, question);
                        break;

                    default:
                        result = ValidateGenericAnswer(answer, question);
                        break;
                }

                LogDebug($"��֤���: {question.questionType} - {(result.isCorrect ? "��ȷ" : "����")}");

                // ������֤�¼�
                OnAnswerValidated?.Invoke(question.questionType, answer, result.isCorrect);

                return result;
            }
            catch (System.Exception e)
            {
                var error = $"��֤�����з����쳣: {e.Message}";
                Debug.LogError($"[AnswerValidationManager] {error}");
                OnValidationError?.Invoke(error);
                return new ValidationResult(false, answer, question.correctAnswer, question.questionType) { errorMessage = error };
            }
        }

        /// <summary>
        /// �򻯵Ĵ���֤�����������ݣ�
        /// </summary>
        /// <param name="answer">��Ҵ�</param>
        /// <param name="question">��Ŀ����</param>
        /// <returns>�Ƿ���ȷ</returns>
        public bool ValidateAnswerSimple(string answer, NetworkQuestionData question)
        {
            var result = ValidateAnswer(answer, question);
            return result.isCorrect;
        }

        #endregion

        #region ����������֤

        /// <summary>
        /// ��֤���������
        /// </summary>
        /// <param name="answer">��Ҵ�</param>
        /// <param name="question">��Ŀ����</param>
        /// <returns>��֤���</returns>
        private ValidationResult ValidateIdiomChainAnswer(string answer, NetworkQuestionData question)
        {
            LogDebug($"��֤���������: {answer}");

            try
            {
                // ����Ŀ�����л�ȡ��ɳ���
                string baseIdiom = GetBaseIdiomFromQuestion(question);

                if (string.IsNullOrEmpty(baseIdiom))
                {
                    var error = "�޷���ȡ�����������ɳ���";
                    LogDebug(error);
                    return new ValidationResult(false, answer, question.correctAnswer, question.questionType) { errorMessage = error };
                }

                // ��ȡ�������������
                var idiomManager = GetIdiomChainManager();
                if (idiomManager == null)
                {
                    var error = "�޷��ҵ��������������";
                    LogDebug(error);
                    return new ValidationResult(false, answer, question.correctAnswer, question.questionType) { errorMessage = error };
                }

                // ����רҵ��֤����
                bool isValid = idiomManager.ValidateIdiomChain(answer, baseIdiom);
                LogDebug($"���������֤���: {answer} (����: {baseIdiom}) -> {isValid}");

                return new ValidationResult(isValid, answer, question.correctAnswer, question.questionType);
            }
            catch (System.Exception e)
            {
                var error = $"���������֤ʧ��: {e.Message}";
                LogDebug(error);
                return new ValidationResult(false, answer, question.correctAnswer, question.questionType) { errorMessage = error };
            }
        }

        /// <summary>
        /// ��֤Ӳ��������
        /// </summary>
        /// <param name="answer">��Ҵ�</param>
        /// <param name="question">��Ŀ����</param>
        /// <returns>��֤���</returns>
        private ValidationResult ValidateHardFillAnswer(string answer, NetworkQuestionData question)
        {
            LogDebug($"��֤Ӳ����մ�: {answer}");

            try
            {
                // ����HardFill�������ľ�̬��֤����
                bool isValid = HardFillQuestionManager.ValidateAnswerStatic(answer, question);
                LogDebug($"Ӳ�������֤���: {isValid}");

                return new ValidationResult(isValid, answer, question.correctAnswer, question.questionType);
            }
            catch (System.Exception e)
            {
                var error = $"Ӳ�������֤ʧ��: {e.Message}";
                LogDebug(error);

                // ���˵�ͨ����֤
                bool fallbackResult = ValidateGenericAnswerInternal(answer, question.correctAnswer);
                return new ValidationResult(fallbackResult, answer, question.correctAnswer, question.questionType) { errorMessage = error };
            }
        }

        /// <summary>
        /// ��֤����������
        /// </summary>
        /// <param name="answer">��Ҵ�</param>
        /// <param name="question">��Ŀ����</param>
        /// <returns>��֤���</returns>
        private ValidationResult ValidateSoftFillAnswer(string answer, NetworkQuestionData question)
        {
            LogDebug($"��֤������մ�: {answer}");

            // �������ͨ���ж�����ܵ���ȷ��
            // ���������չ֧�ֶ����֤
            bool isValid = ValidateGenericAnswerInternal(answer, question.correctAnswer);

            // �������������������յ������߼�
            // ����ͬ��ʼ�顢����ƥ���

            return new ValidationResult(isValid, answer, question.correctAnswer, question.questionType);
        }

        /// <summary>
        /// ��֤ƴ����
        /// </summary>
        /// <param name="answer">��Ҵ�</param>
        /// <param name="question">��Ŀ����</param>
        /// <returns>��֤���</returns>
        private ValidationResult ValidatePinyinAnswer(string answer, NetworkQuestionData question)
        {
            LogDebug($"��֤ƴ����: {answer}");

            // ƴ����֤�����⴦��
            string normalizedAnswer = NormalizePinyinAnswer(answer);
            string normalizedCorrect = NormalizePinyinAnswer(question.correctAnswer);

            bool isValid = normalizedAnswer.Equals(normalizedCorrect, System.StringComparison.OrdinalIgnoreCase);

            return new ValidationResult(isValid, answer, question.correctAnswer, question.questionType);
        }

        /// <summary>
        /// ��֤ѡ�����
        /// </summary>
        /// <param name="answer">��Ҵ�</param>
        /// <param name="question">��Ŀ����</param>
        /// <returns>��֤���</returns>
        private ValidationResult ValidateChoiceAnswer(string answer, NetworkQuestionData question)
        {
            LogDebug($"��֤ѡ�����: {answer}");

            // ѡ����ͨ���Ǿ�ȷƥ��
            bool isValid = answer.Trim().Equals(question.correctAnswer.Trim(),
                caseSensitive ? System.StringComparison.Ordinal : System.StringComparison.OrdinalIgnoreCase);

            return new ValidationResult(isValid, answer, question.correctAnswer, question.questionType);
        }

        /// <summary>
        /// ��֤�ж����
        /// </summary>
        /// <param name="answer">��Ҵ�</param>
        /// <param name="question">��Ŀ����</param>
        /// <returns>��֤���</returns>
        private ValidationResult ValidateTrueFalseAnswer(string answer, NetworkQuestionData question)
        {
            LogDebug($"��֤�ж����: {answer}");

            // ��׼���ж����
            string normalizedAnswer = NormalizeTrueFalseAnswer(answer);
            string normalizedCorrect = NormalizeTrueFalseAnswer(question.correctAnswer);

            bool isValid = normalizedAnswer.Equals(normalizedCorrect, System.StringComparison.OrdinalIgnoreCase);

            return new ValidationResult(isValid, answer, question.correctAnswer, question.questionType);
        }

        /// <summary>
        /// ��֤��д���
        /// </summary>
        /// <param name="answer">��Ҵ�</param>
        /// <param name="question">��Ŀ����</param>
        /// <returns>��֤���</returns>
        private ValidationResult ValidateHandwritingAnswer(string answer, NetworkQuestionData question)
        {
            LogDebug($"��֤��д���: {answer}");

            // ��д�������Ҫ�����ʶ�����֤�߼�
            // ������ʱʹ��ͨ����֤��ʵ�ʿ�����ҪOCR��ͼ��ʶ��

            bool isValid = ValidateGenericAnswerInternal(answer, question.correctAnswer);

            return new ValidationResult(isValid, answer, question.correctAnswer, question.questionType);
        }

        /// <summary>
        /// ͨ�ô���֤
        /// </summary>
        /// <param name="answer">��Ҵ�</param>
        /// <param name="question">��Ŀ����</param>
        /// <returns>��֤���</returns>
        private ValidationResult ValidateGenericAnswer(string answer, NetworkQuestionData question)
        {
            LogDebug($"��֤ͨ�ô�: {answer}");

            bool isValid = ValidateGenericAnswerInternal(answer, question.correctAnswer);

            return new ValidationResult(isValid, answer, question.correctAnswer, question.questionType);
        }

        #endregion

        #region �𰸱�׼������

        /// <summary>
        /// ��׼��ƴ����
        /// </summary>
        /// <param name="answer">ԭʼ��</param>
        /// <returns>��׼����Ĵ�</returns>
        private string NormalizePinyinAnswer(string answer)
        {
            if (string.IsNullOrEmpty(answer))
                return "";

            // �Ƴ��ո��������ŵ�
            string normalized = answer.Trim()
                .Replace(" ", "")
                .Replace("��", "a").Replace("��", "a").Replace("��", "a").Replace("��", "a")
                .Replace("��", "e").Replace("��", "e").Replace("��", "e").Replace("��", "e")
                .Replace("��", "i").Replace("��", "i").Replace("��", "i").Replace("��", "i")
                .Replace("��", "o").Replace("��", "o").Replace("��", "o").Replace("��", "o")
                .Replace("��", "u").Replace("��", "u").Replace("��", "u").Replace("��", "u")
                .Replace("��", "v").Replace("��", "v").Replace("��", "v").Replace("��", "v");

            return normalized.ToLowerInvariant();
        }

        /// <summary>
        /// ��׼���ж����
        /// </summary>
        /// <param name="answer">ԭʼ��</param>
        /// <returns>��׼����Ĵ�</returns>
        private string NormalizeTrueFalseAnswer(string answer)
        {
            if (string.IsNullOrEmpty(answer))
                return "";

            string normalized = answer.Trim().ToLowerInvariant();

            // ͳһ������"��"�ı�ʾ
            if (normalized == "true" || normalized == "��" || normalized == "��" ||
                normalized == "��ȷ" || normalized == "��" || normalized == "t" || normalized == "1")
            {
                return "true";
            }

            // ͳһ������"��"�ı�ʾ
            if (normalized == "false" || normalized == "��" || normalized == "��" ||
                normalized == "����" || normalized == "��" || normalized == "f" || normalized == "0")
            {
                return "false";
            }

            return normalized;
        }

        /// <summary>
        /// ͨ�ô𰸱�׼��
        /// </summary>
        /// <param name="answer">ԭʼ��</param>
        /// <returns>��׼����Ĵ�</returns>
        private string NormalizeGenericAnswer(string answer)
        {
            if (string.IsNullOrEmpty(answer))
                return "";

            string normalized = answer.Trim();

            // �Ƴ�����Ŀո�
            while (normalized.Contains("  "))
            {
                normalized = normalized.Replace("  ", " ");
            }

            // ͳһ��Ӣ�ı�����
            normalized = normalized
                .Replace("��", ",")
                .Replace("��", ".")
                .Replace("��", "?")
                .Replace("��", "!")
                .Replace("��", ";")
                .Replace("��", ":");

            return caseSensitive ? normalized : normalized.ToLowerInvariant();
        }

        #endregion

        #region ������֤����

        /// <summary>
        /// ͨ�ô���֤�ڲ�ʵ��
        /// </summary>
        /// <param name="answer">��Ҵ�</param>
        /// <param name="correctAnswer">��ȷ��</param>
        /// <returns>�Ƿ���ȷ</returns>
        private bool ValidateGenericAnswerInternal(string answer, string correctAnswer)
        {
            if (string.IsNullOrEmpty(answer) || string.IsNullOrEmpty(correctAnswer))
                return false;

            // ��黺��
            string cacheKey = $"{answer}|{correctAnswer}";
            if (validationCache != null && validationCache.ContainsKey(cacheKey))
            {
                return validationCache[cacheKey];
            }

            // ��׼����
            string normalizedAnswer = NormalizeGenericAnswer(answer);
            string normalizedCorrect = NormalizeGenericAnswer(correctAnswer);

            bool isCorrect;

            if (strictValidation)
            {
                // �ϸ�ģʽ��������ȫƥ��
                isCorrect = normalizedAnswer.Equals(normalizedCorrect);
            }
            else
            {
                // ����ģʽ��������ƥ��
                isCorrect = normalizedAnswer.Equals(normalizedCorrect) ||
                           normalizedCorrect.Contains(normalizedAnswer) ||
                           normalizedAnswer.Contains(normalizedCorrect);
            }

            // ������
            if (validationCache != null)
            {
                // ���ƻ����С
                if (validationCache.Count >= maxCacheSize)
                {
                    validationCache.Clear();
                }
                validationCache[cacheKey] = isCorrect;
            }

            return isCorrect;
        }

        /// <summary>
        /// ����Ŀ�����л�ȡ��ɳ���
        /// </summary>
        /// <param name="question">��Ŀ����</param>
        /// <returns>��ɳ���</returns>
        private string GetBaseIdiomFromQuestion(NetworkQuestionData question)
        {
            try
            {
                if (!string.IsNullOrEmpty(question.additionalData))
                {
                    var additionalInfo = JsonUtility.FromJson<IdiomChainAdditionalData>(question.additionalData);
                    return additionalInfo.currentIdiom;
                }
            }
            catch (System.Exception e)
            {
                LogDebug($"��ȡ��ɳ���ʧ��: {e.Message}");
            }

            return "";
        }

        /// <summary>
        /// ��ȡ�������������ʵ��
        /// </summary>
        /// <returns>�������������</returns>
        private IdiomChainQuestionManager GetIdiomChainManager()
        {
            // ����ѻ��棬ֱ�ӷ���
            if (idiomChainManager != null)
                return idiomChainManager;

            try
            {
                // ����Ŀ���ݷ����ȡ
                if (questionDataService != null)
                {
                    var getProviderMethod = questionDataService.GetType()
                        .GetMethod("GetOrCreateProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (getProviderMethod != null)
                    {
                        var provider = getProviderMethod.Invoke(questionDataService, new object[] { QuestionType.IdiomChain });

                        if (provider is IdiomChainQuestionManager manager)
                        {
                            idiomChainManager = manager;
                            return manager;
                        }
                    }
                }

                // ֱ���ڳ����в���
                idiomChainManager = Object.FindObjectOfType<IdiomChainQuestionManager>();
                return idiomChainManager;
            }
            catch (System.Exception e)
            {
                LogDebug($"��ȡIdiomChainQuestionManagerʧ��: {e.Message}");
                return null;
            }
        }

        #endregion

        #region ������֤

        /// <summary>
        /// ������֤��
        /// </summary>
        public List<ValidationResult> ValidateAnswersBatch(List<string> answers, List<NetworkQuestionData> questions)
        {
            if (answers == null || questions == null || answers.Count != questions.Count)
            {
                LogDebug("������֤������Ч");
                return new List<ValidationResult>();
            }

            LogDebug($"��ʼ������֤ {answers.Count} ����");

            var results = new List<ValidationResult>();

            for (int i = 0; i < answers.Count; i++)
            {
                var result = ValidateAnswer(answers[i], questions[i]);
                results.Add(result);
            }

            LogDebug($"������֤��ɣ���ȷ��: {results.Count(r => r.isCorrect)}/{results.Count}");

            return results;
        }

        #endregion

        #region ���ù���

        /// <summary>
        /// ���û����С
        /// </summary>
        /// <param name="size">��󻺴���Ŀ��</param>
        public void SetCacheSize(int size)
        {
            maxCacheSize = Mathf.Max(0, size);

            if (validationCache != null && validationCache.Count > maxCacheSize)
            {
                validationCache.Clear();
            }

            LogDebug($"��֤�����С������Ϊ: {maxCacheSize}");
        }

        /// <summary>
        /// �����֤����
        /// </summary>
        public void ClearValidationCache()
        {
            validationCache?.Clear();
            LogDebug("��֤���������");
        }

        #endregion

        #region ͳ����Ϣ

        /// <summary>
        /// ��ȡ��֤ͳ����Ϣ
        /// </summary>
        /// <returns>ͳ����Ϣ�ַ���</returns>
        public string GetValidationStats()
        {
            var stats = "=== AnswerValidationManagerͳ�� ===\n";
            stats += $"��֤ģʽ: {(strictValidation ? "�ϸ�" : "����")}\n";
            stats += $"��Сд����: {(caseSensitive ? "��" : "��")}\n";
            stats += $"��������: {(validationCache != null ? "��" : "��")}\n";

            if (validationCache != null)
            {
                stats += $"������Ŀ��: {validationCache.Count}/{maxCacheSize}\n";
                stats += $"����ʹ����: {(float)validationCache.Count / maxCacheSize:P1}\n";
            }

            stats += $"�������������: {(idiomChainManager != null ? "�ѻ���" : "δ����")}\n";
            stats += $"��Ŀ���ݷ���: {(questionDataService != null ? "������" : "δ����")}\n";

            return stats;
        }

        #endregion

        #region ���߷���

        /// <summary>
        /// ���õ�����־����
        /// </summary>
        /// <param name="enabled">�Ƿ����õ�����־</param>
        public void SetDebugLogs(bool enabled)
        {
            enableDebugLogs = enabled;
            LogDebug($"������־��{(enabled ? "����" : "����")}");
        }

        /// <summary>
        /// ������־���
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[AnswerValidationManager] {message}");
            }
        }

        #endregion

        #region ���ٺ�����

        /// <summary>
        /// ���ٴ���֤������
        /// </summary>
        public void Dispose()
        {
            // �����¼�
            OnAnswerValidated = null;
            OnValidationError = null;

            // ������
            validationCache?.Clear();
            validationCache = null;

            // ��������
            idiomChainManager = null;
            questionDataService = null;

            LogDebug("AnswerValidationManager������");
        }

        #endregion
    }

    /// <summary>
    /// ��������������ݽṹ����GameLogic.FillBlank�����ռ䱣��һ�£�
    /// </summary>
    [System.Serializable]
    public class IdiomChainAdditionalData
    {
        public string currentIdiom;
        public int chainCount;
        public string[] possibleAnswers;
    }
}