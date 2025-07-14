using UnityEngine;

namespace Core
{
    /// <summary>
    /// HP�������ݽṹ
    /// �����������ֵ�Ϳ�Ѫ�������
    /// </summary>
    [CreateAssetMenu(fileName = "HPConfig", menuName = "Game/HP Config")]
    public class HPConfig : ScriptableObject
    {
        [Header("����ֵ����")]
        [Range(10f, 500f)]
        [Tooltip("��ǰ/��ʼ����ֵ")]
        public float currentHealth = 100f;

        [Range(1f, 100f)]
        [Tooltip("ÿ�δ��۳���Ѫ��")]
        public float damagePerWrong = 20f;

        [Header("Ԫ����")]
        [SerializeField] private string configName = "Ĭ��HP����";
        [SerializeField] private string configDescription = "��׼����ֵ�Ϳ�Ѫ����";

        /// <summary>
        /// ��������
        /// </summary>
        public string ConfigName => configName;

        /// <summary>
        /// ��������
        /// </summary>
        public string ConfigDescription => configDescription;

        /// <summary>
        /// ��ȡ��ǰ����ֵ��Ҳ�ǳ�ʼ/�������ֵ��
        /// </summary>
        public float GetCurrentHealth() => currentHealth;

        /// <summary>
        /// ��ȡÿ�δ��Ŀ�Ѫ��
        /// </summary>
        public float GetDamagePerWrong() => damagePerWrong;

        /// <summary>
        /// ��ȡ�������ֵ����ͬ�ڵ�ǰ����ֵ��
        /// </summary>
        public float GetMaxHealth() => currentHealth;

        /// <summary>
        /// ���������Դ��Ĵ���
        /// </summary>
        public int GetMaxWrongAnswers()
        {
            if (damagePerWrong <= 0) return 0;
            return Mathf.FloorToInt(currentHealth / damagePerWrong);
        }

        /// <summary>
        /// ��������ֵ
        /// </summary>
        /// <param name="health">�µ�����ֵ</param>
        public void SetCurrentHealth(float health)
        {
            currentHealth = Mathf.Clamp(health, 10f, 500f);
        }

        /// <summary>
        /// ���ÿ�Ѫ��
        /// </summary>
        /// <param name="damage">�µĿ�Ѫ��</param>
        public void SetDamagePerWrong(float damage)
        {
            damagePerWrong = Mathf.Clamp(damage, 1f, 100f);
        }

        /// <summary>
        /// ��֤���õ���Ч��
        /// </summary>
        public bool ValidateConfig()
        {
            bool isValid = true;

            // �������ֵ
            if (currentHealth <= 0)
            {
                Debug.LogError("[HPConfig] ����ֵ����С�ڵ���0");
                isValid = false;
            }

            if (currentHealth < 10f)
            {
                Debug.LogError("[HPConfig] ����ֵ���ͣ���������10��");
                isValid = false;
            }

            // ����Ѫ��
            if (damagePerWrong <= 0)
            {
                Debug.LogError("[HPConfig] ��Ѫ������С�ڵ���0");
                isValid = false;
            }

            if (damagePerWrong > currentHealth)
            {
                Debug.LogError("[HPConfig] ��Ѫ�����ܴ�������ֵ");
                isValid = false;
            }

            // ��������
            if (GetMaxWrongAnswers() < 1)
            {
                Debug.LogError("[HPConfig] ����Ҫ�ܴ��1��");
                isValid = false;
            }

            if (GetMaxWrongAnswers() > 50)
            {
                Debug.LogWarning("[HPConfig] �ɴ��������࣬��Ϸ���ܹ��ڼ�");
            }

            return isValid;
        }

        /// <summary>
        /// ����ΪĬ������
        /// </summary>
        public void ResetToDefault()
        {
            currentHealth = 100f;
            damagePerWrong = 20f;
            configName = "Ĭ��HP����";
            configDescription = "��׼����ֵ�Ϳ�Ѫ����";

            Debug.Log("[HPConfig] ����������ΪĬ��ֵ");
        }

        /// <summary>
        /// ��ȡ����ժҪ
        /// </summary>
        public string GetConfigSummary()
        {
            var summary = $"=== {configName} ===\n";
            summary += $"����: {configDescription}\n\n";
            summary += $"����ֵ����:\n";
            summary += $"  ��ʼ����ֵ: {currentHealth}��\n";
            summary += $"  ����Ѫ: {damagePerWrong}��\n";
            summary += $"  ���ɴ��: {GetMaxWrongAnswers()}��\n";

            // ���������������Ϣ
            float survivalRate = (float)GetMaxWrongAnswers() / (GetMaxWrongAnswers() + 1) * 100f;
            summary += $"  �ݴ���: {survivalRate:F1}%\n";

            return summary;
        }

        /// <summary>
        /// �������õ����
        /// </summary>
        public HPConfig CreateCopy()
        {
            var copy = CreateInstance<HPConfig>();

            copy.currentHealth = currentHealth;
            copy.damagePerWrong = damagePerWrong;
            copy.configName = configName + " (����)";
            copy.configDescription = configDescription;

            return copy;
        }

        /// <summary>
        /// ����һ�����ø�������
        /// </summary>
        /// <param name="other">Դ����</param>
        public void CopyFrom(HPConfig other)
        {
            if (other == null)
            {
                Debug.LogWarning("[HPConfig] ���Դӿ����ø�������");
                return;
            }

            currentHealth = other.currentHealth;
            damagePerWrong = other.damagePerWrong;
            configDescription = other.configDescription;
            // �������������ƣ����ֵ�ǰ����

            Debug.Log($"[HPConfig] �Ѵ� {other.configName} ������������");
        }

        /// <summary>
        /// ��������Ƿ���δ����ĸ���
        /// </summary>
        public bool HasUnsavedChanges()
        {
            // ���������������߼�
            // Ŀǰ��Ϊʼ�շ���false
            return false;
        }

        /// <summary>
        /// �������Ϊ�ɾ�״̬���ѱ��棩
        /// </summary>
        public void ClearDirty()
        {
            // �������
            // Ŀǰ��Ϊ��ʵ��
        }

        /// <summary>
        /// ��ȡ���õ���ϸ��Ϣ�������ã�
        /// </summary>
        public string GetDetailedInfo()
        {
            var info = $"=== HP������ϸ��Ϣ ===\n";
            info += $"��������: {configName}\n";
            info += $"��������: {configDescription}\n";
            info += $"ʵ��ID: {GetInstanceID()}\n";
            info += $"��ǰ����ֵ: {currentHealth}\n";
            info += $"��Ѫ��: {damagePerWrong}\n";
            info += $"��������: {GetMaxWrongAnswers()}\n";
            info += $"������Ч��: {(ValidateConfig() ? "��Ч" : "��Ч")}\n";
            info += $"�ݴ���: {((float)GetMaxWrongAnswers() / (GetMaxWrongAnswers() + 1) * 100f):F1}%\n";

            return info;
        }

        private void OnValidate()
        {
            // ��Inspector���޸�ʱ�Զ���֤��Լ����ֵ
            currentHealth = Mathf.Clamp(currentHealth, 10f, 500f);
            damagePerWrong = Mathf.Clamp(damagePerWrong, 1f, 100f);

            // ȷ����Ѫ������������ֵ
            if (damagePerWrong > currentHealth)
            {
                damagePerWrong = currentHealth;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("��ʾ����ժҪ")]
        public void ShowConfigSummary()
        {
            Debug.Log(GetConfigSummary());
        }

        [ContextMenu("��֤����")]
        public void ValidateConfigEditor()
        {
            bool isValid = ValidateConfig();
            Debug.Log($"[HPConfig] ������֤���: {(isValid ? "ͨ��" : "ʧ��")}");
        }

        [ContextMenu("����ΪĬ������")]
        public void ResetToDefaultEditor()
        {
            ResetToDefault();
            Debug.Log("[HPConfig] ������ΪĬ������");
        }

        [ContextMenu("��ʾ��ϸ��Ϣ")]
        public void ShowDetailedInfo()
        {
            Debug.Log(GetDetailedInfo());
        }

        [ContextMenu("���Դ�������")]
        public void TestCreateCopy()
        {
            var copy = CreateCopy();
            Debug.Log($"[HPConfig] ���������ɹ�: {copy.configName} (ID: {copy.GetInstanceID()})");
        }
#endif
    }
}