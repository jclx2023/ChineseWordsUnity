using UnityEngine;
using System.Data;
using Mono.Data.Sqlite;

/// <summary>
/// �����ࣺ�� SQLite ���ݿ����������� Word �� WordChar ���ݣ��Զ������Ѵ��ڵĴ�
/// </summary>
public static class WordImporter
{
    // ���ݿ������ַ�����ָ�� Assets/StreamingAssets/Temp.db��
    private static readonly string ConnString =
        "URI=file:" + Application.streamingAssetsPath + "/Temp.db";

    /// <summary>
    /// �� Word��WordChar �����һ�����Ｐ���������
    /// �� Word.text �Ѵ��ڣ���������
    /// </summary>
    /// <param name="word">Ҫ����Ĵ�����硰Ҷ��������</param>
    /// <param name="freq">��ƵȨ�أ�Ĭ�� 1</param>
    public static void InsertWord(string word, int freq = 1)
    {
        using (var conn = new SqliteConnection(ConnString))
        {
            conn.Open();

            // 1) ����Ƿ��Ѵ���
            using (var checkCmd = conn.CreateCommand())
            {
                checkCmd.CommandText = "SELECT COUNT(1) FROM Word WHERE text = @text";
                checkCmd.Parameters.AddWithValue("@text", word);
                long exists = (long)checkCmd.ExecuteScalar();
                if (exists > 0)
                {
                    Debug.Log($"Skipping duplicate word: '{word}'");
                    return;
                }
            }

            using (var tx = conn.BeginTransaction())
            {
                // 2) ���� Word ��
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "INSERT INTO Word(text, length, freq) VALUES(@text, @len, @freq);";
                    cmd.Parameters.AddWithValue("@text", word);
                    cmd.Parameters.AddWithValue("@len", word.Length);
                    cmd.Parameters.AddWithValue("@freq", freq);
                    cmd.ExecuteNonQuery();
                }

                // 3) ��ȡ��������
                long wordId;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT last_insert_rowid();";
                    wordId = (long)cmd.ExecuteScalar();
                }

                // 4) ����ַ������� WordChar ��
                for (int i = 0; i < word.Length; i++)
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText =
                            "INSERT INTO WordChar(word_id, char, pos) VALUES(@id, @ch, @pos);";
                        cmd.Parameters.AddWithValue("@id", wordId);
                        cmd.Parameters.AddWithValue("@ch", word[i].ToString());
                        cmd.Parameters.AddWithValue("@pos", i);
                        cmd.ExecuteNonQuery();
                    }
                }

                tx.Commit();
                Debug.Log($"Inserted word '{word}' with id {wordId}");
            }

            conn.Close();
        }
    }
}
