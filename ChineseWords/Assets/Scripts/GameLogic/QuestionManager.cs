using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Data;
using Mono.Data.Sqlite;

/// <summary>
/// ������⡢У��𰸣����� UI ����ʾ�����͡�Ͷ��������
/// </summary>
public class QuestionManager : MonoBehaviour
{
    [Header("���ݿ�·����StreamingAssets/Temp.db��")]
    private string dbPath;

    [Header("UI Components")]
    public TMP_Text questionText;    // ������ʾ _��__ ��ʽ��Ŀ
    public TMP_InputField answerInput;     // ���������������
    public Button submitButton;    // �ύ��ť
    public Button surrenderButton; // Ͷ����ť��ֱ����һ��
    public TMP_Text feedbackText;    // ����ȷ/������

    // ��ǰ�����
    private string selectedChar;
    private int selectedPos;
    private int selectedLen;

    void Awake()
    {
        // ���ݿ�·����.../Assets/StreamingAssets/Temp.db
        dbPath = Application.streamingAssetsPath + "/Temp.db";
    }

    void Start()
    {
        // �󶨰�ť�¼�
        submitButton.onClick.AddListener(OnSubmit);
        surrenderButton.onClick.AddListener(OnSurrender);
        // ��ʼ��շ���
        feedbackText.text = string.Empty;
        // ��������
        LoadRandomQuestion();
    }

    /// <summary>
    /// ��������ݿ����һ������� (_��__) ����ʾ
    /// </summary>
    void LoadRandomQuestion()
    {
        string connStr = "URI=file:" + dbPath;
        using (var conn = new SqliteConnection(connStr))
        {
            conn.Open();

            // ���ѡ�ּ���λ�úʹʳ�
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT wc.char, wc.pos, w.length
                      FROM WordChar wc
                      JOIN Word     w ON w.id = wc.word_id
                     GROUP BY wc.word_id
                     ORDER BY RANDOM()
                     LIMIT 1";
                using (var reader = cmd.ExecuteReader())
                {
                    reader.Read();
                    selectedChar = reader.GetString(0);
                    selectedPos = reader.GetInt32(1);
                    selectedLen = reader.GetInt32(2);
                }
            }
            conn.Close();
        }

        // ��ʾ��Ŀ������������
        questionText.text = GeneratePattern(selectedChar, selectedPos, selectedLen);
        answerInput.text = string.Empty;
        answerInput.ActivateInputField();
        // ��������� feedbackText�������ϴ���ʾֱ����һ��
    }

    /// <summary>
    /// �����ύ�𰸵ĵ��
    /// </summary>
    void OnSubmit()
    {
        string ans = answerInput.text.Trim();
        if (string.IsNullOrEmpty(ans)) return;
        // �������Э����ִ�У���ֹͣ
        StopAllCoroutines();
        StartCoroutine(HandleAnswer(ans));
    }

    IEnumerator HandleAnswer(string answer)
    {
        // У���
        bool ok = ValidateAnswer(answer);
        if (ok)
        {
            feedbackText.text = "�ش���ȷ!";
            feedbackText.color = Color.green;
        }
        else
        {
            feedbackText.text = "�ش����!";
            feedbackText.color = Color.red;
        }
        // �ȴ�һ���ٻ��⣬����ҿ�������
        yield return new WaitForSeconds(1f);
        LoadRandomQuestion();
    }

    /// <summary>
    /// ����Ͷ����ť���
    /// </summary>
    void OnSurrender()
    {
        StopAllCoroutines();
        StartCoroutine(HandleSurrender());
    }

    IEnumerator HandleSurrender()
    {
        feedbackText.text = "��Ͷ��,������һ��";
        feedbackText.color = Color.yellow;
        // �ȴ��������
        yield return new WaitForSeconds(0.5f);
        LoadRandomQuestion();
    }

    /// <summary>
    /// ��֤��Ҵ��Ƿ���ȷ
    /// </summary>
    bool ValidateAnswer(string answer)
    {
        bool result = false;
        string connStr = "URI=file:" + dbPath;
        using (var conn = new SqliteConnection(connStr))
        {
            conn.Open();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT COUNT(*)
                      FROM WordChar wc
                      JOIN Word     w  ON w.id = wc.word_id
                     WHERE w.length = @len
                       AND wc.char   = @ch
                       AND wc.pos    = @pos
                       AND w.text    = @ans";
                cmd.Parameters.AddWithValue("@len", selectedLen);
                cmd.Parameters.AddWithValue("@ch", selectedChar);
                cmd.Parameters.AddWithValue("@pos", selectedPos);
                cmd.Parameters.AddWithValue("@ans", answer);

                long cnt = (long)cmd.ExecuteScalar();
                result = cnt > 0;
            }
            conn.Close();
        }
        return result;
    }

    /// <summary>
    /// ���ݴʳ���ѡ�������� _��__ ��ʽ����մ�
    /// </summary>
    string GeneratePattern(string ch, int pos, int len)
    {
        char[] arr = new char[len];
        for (int i = 0; i < len; i++) arr[i] = '_';
        arr[pos] = ch[0];
        return new string(arr);
    }
}