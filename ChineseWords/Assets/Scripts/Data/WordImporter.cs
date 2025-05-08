using UnityEngine;
using System.Data;
using Mono.Data.Sqlite;

/// <summary>
/// �����ࣺ�� SQLite ���ݿ����������� Word �� WordChar ����
/// </summary>
public static class WordImporter
{
    // ���ݿ������ַ�����ָ�� Assets/Resources/Questions/Temp.db��
    private static readonly string ConnString =
        "URI=file:" + Application.dataPath + "/Resources/Questions/Temp.db";
    /// <summary>
    /// �� Word��WordChar �����һ�����Ｐ���������
    /// </summary>
    /// <param name="word">Ҫ����Ĵ�����硰Ҷ��������</param>
    /// <param name="freq">��ƵȨ�أ�Ĭ�� 1</param>
    public static void InsertWord(string word, int freq = 1)
    {
        using (var conn = new SqliteConnection(ConnString))
        {
            conn.Open();
            using (var tx = conn.BeginTransaction())
            {
                // 1) ���� Word ��
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "INSERT INTO Word(text, length, freq) VALUES(@text, @len, @freq);";
                    cmd.Parameters.AddWithValue("@text", word);
                    cmd.Parameters.AddWithValue("@len", word.Length);
                    cmd.Parameters.AddWithValue("@freq", freq);
                    cmd.ExecuteNonQuery();
                }

                // 2) ȡ���ղ��������������ͨ�� SQL ��ѯ last_insert_rowid()
                long wordId;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT last_insert_rowid();";
                    wordId = (long)cmd.ExecuteScalar();
                }

                // 3) ����ַ������� WordChar ��
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

                // �ύ����
                tx.Commit();

                Debug.Log($"Inserted word '{word}' with id {wordId}");
            }
            conn.Close();
        }
    }
}
