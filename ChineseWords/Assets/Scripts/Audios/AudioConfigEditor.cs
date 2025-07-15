#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Audio.Editor
{
    /// <summary>
    /// ��Ƶ���õ��Զ���༭��
    /// </summary>
    [CustomEditor(typeof(AudioConfig))]
    public class AudioConfigEditor : UnityEditor.Editor
    {
        private SerializedProperty audioClips;
        private SerializedProperty masterVolume, musicVolume, sfxVolume, voiceVolume, uiVolume;
        private SerializedProperty musicSourceCount, sfxSourceCount, voiceSourceCount, uiSourceCount;

        private bool showAudioClips = true;
        private bool showVolumeSettings = true;
        private bool showPoolSettings = true;
        private bool showPreview = true;

        private string searchFilter = "";
        private AudioType filterType = AudioType.Music;
        private bool useTypeFilter = false;

        // Ԥ�����
        private AudioSource previewSource;
        private string currentPreviewId = "";

        private void OnEnable()
        {
            audioClips = serializedObject.FindProperty("audioClips");

            masterVolume = serializedObject.FindProperty("masterVolume");
            musicVolume = serializedObject.FindProperty("musicVolume");
            sfxVolume = serializedObject.FindProperty("sfxVolume");
            voiceVolume = serializedObject.FindProperty("voiceVolume");
            uiVolume = serializedObject.FindProperty("uiVolume");

            musicSourceCount = serializedObject.FindProperty("musicSourceCount");
            sfxSourceCount = serializedObject.FindProperty("sfxSourceCount");
            voiceSourceCount = serializedObject.FindProperty("voiceSourceCount");
            uiSourceCount = serializedObject.FindProperty("uiSourceCount");

            CreatePreviewSource();
        }

        private void OnDisable()
        {
            StopPreview();
            if (previewSource != null)
            {
                DestroyImmediate(previewSource.gameObject);
            }
        }

        public override void OnInspectorGUI()
        {
            // �ڿ�ʼʱǿ�Ƹ���SerializedObject
            serializedObject.Update();

            try
            {
                EditorGUILayout.Space();
                DrawHeader();
                EditorGUILayout.Space();

                DrawVolumeSettings();
                EditorGUILayout.Space();

                DrawPoolSettings();
                EditorGUILayout.Space();

                DrawAudioClipsSection();
                EditorGUILayout.Space();

                DrawPreviewSection();
                EditorGUILayout.Space();

                DrawUtilityButtons();

                // Ӧ���޸�
                if (serializedObject.hasModifiedProperties)
                {
                    serializedObject.ApplyModifiedProperties();
                }
            }
            catch (System.Exception e)
            {
                EditorGUILayout.HelpBox($"�༭�����ִ���: {e.Message}", MessageType.Error);

                Debug.LogError($"AudioConfigEditor����: {e}");
            }
        }

        private void DrawHeader()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 16;
            headerStyle.alignment = TextAnchor.MiddleCenter;

            EditorGUILayout.LabelField("��Ƶ���ù�����", headerStyle);

            // ��ʾͳ����Ϣ
            var config = target as AudioConfig;
            if (config != null && config.audioClips != null)
            {
                int totalClips = config.audioClips.Count;
                int musicCount = config.audioClips.Count(c => c.type == AudioType.Music);
                int sfxCount = config.audioClips.Count(c => c.type == AudioType.SFX);
                int voiceCount = config.audioClips.Count(c => c.type == AudioType.Voice);
                int uiCount = config.audioClips.Count(c => c.type == AudioType.UI);

                EditorGUILayout.LabelField($"����Ƶ: {totalClips} | ����: {musicCount} | ��Ч: {sfxCount} | ����: {voiceCount} | UI: {uiCount}",
                    EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawVolumeSettings()
        {
            showVolumeSettings = EditorGUILayout.Foldout(showVolumeSettings, "��������", true);
            if (showVolumeSettings)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(masterVolume, new GUIContent("������"));
                EditorGUILayout.PropertyField(musicVolume, new GUIContent("��������"));
                EditorGUILayout.PropertyField(sfxVolume, new GUIContent("��Ч����"));
                EditorGUILayout.PropertyField(voiceVolume, new GUIContent("��������"));
                EditorGUILayout.PropertyField(uiVolume, new GUIContent("UI����"));

                // �������ð�ť
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("ȫ�����", GUILayout.Width(80)))
                {
                    SetAllVolumes(1f);
                }
                if (GUILayout.Button("ȫ��һ��", GUILayout.Width(80)))
                {
                    SetAllVolumes(0.5f);
                }
                if (GUILayout.Button("ȫ������", GUILayout.Width(80)))
                {
                    SetAllVolumes(0f);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }
        }

        private void DrawPoolSettings()
        {
            showPoolSettings = EditorGUILayout.Foldout(showPoolSettings, "��ƵԴ������", true);
            if (showPoolSettings)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(musicSourceCount, new GUIContent("����Դ����", "ͬʱ���ŵ�����Դ���������ڽ��浭�뵭��"));
                EditorGUILayout.PropertyField(sfxSourceCount, new GUIContent("��ЧԴ����", "ͬʱ���ŵ���Ч����"));
                EditorGUILayout.PropertyField(voiceSourceCount, new GUIContent("����Դ����", "ͬʱ���ŵ���������"));
                EditorGUILayout.PropertyField(uiSourceCount, new GUIContent("UI��ЧԴ����", "ͬʱ���ŵ�UI��Ч����"));

                // ������ʾ
                if (musicSourceCount.intValue < 2)
                {
                    EditorGUILayout.HelpBox("����Դ������������Ϊ2����֧�ֽ��浭�뵭��", MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawAudioClipsSection()
        {
            showAudioClips = EditorGUILayout.Foldout(showAudioClips, $"��Ƶ�����б� ({audioClips.arraySize})", true);
            if (showAudioClips)
            {
                EditorGUI.indentLevel++;

                // �����͹���
                DrawSearchAndFilter();

                // �������Ƶ��ť
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("�����Ƶ����", GUILayout.Height(25)))
                {
                    audioClips.InsertArrayElementAtIndex(audioClips.arraySize);
                    var newElement = audioClips.GetArrayElementAtIndex(audioClips.arraySize - 1);
                    InitializeNewAudioClip(newElement);
                }

                if (GUILayout.Button("��������", GUILayout.Height(25), GUILayout.Width(80)))
                {
                    BatchImportAudioClips();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                // ������Ƶ�����б�
                DrawAudioClipsList();

                EditorGUI.indentLevel--;
            }
        }

        private void DrawSearchAndFilter()
        {
            EditorGUILayout.BeginHorizontal();

            // ������
            EditorGUILayout.LabelField("����:", GUILayout.Width(40));
            searchFilter = EditorGUILayout.TextField(searchFilter);

            // ���͹���
            useTypeFilter = EditorGUILayout.Toggle("���͹���:", useTypeFilter, GUILayout.Width(80));
            GUI.enabled = useTypeFilter;
            filterType = (AudioType)EditorGUILayout.EnumPopup(filterType, GUILayout.Width(80));
            GUI.enabled = true;

            if (GUILayout.Button("���", GUILayout.Width(50)))
            {
                searchFilter = "";
                useTypeFilter = false;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAudioClipsList()
        {
            var config = target as AudioConfig;
            if (config?.audioClips == null) return;

            // ȷ��SerializedProperty��ʵ���б�ͬ��
            serializedObject.Update();

            int arraySize = audioClips.arraySize;
            int listCount = config.audioClips.Count;

            // ��ȫ��飺ʹ�ý�С����ֵ��Ϊѭ������
            int safeCount = Mathf.Min(arraySize, listCount);

            for (int i = 0; i < safeCount; i++)
            {
                try
                {
                    var element = audioClips.GetArrayElementAtIndex(i);
                    var clipData = config.audioClips[i];

                    // ��ȫ��飺ȷ��clipData��Ϊnull
                    if (clipData == null) continue;

                    // Ӧ�������͹���
                    if (!ShouldShowClip(clipData)) continue;

                    EditorGUILayout.BeginVertical("box");

                    // ������Ƶ������
                    DrawAudioClipItem(element, i, clipData);

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
                catch (System.Exception e)
                {
                    // ���������Ŀ������ʾ������Ϣ����������������Ŀ
                    EditorGUILayout.HelpBox($"��Ƶ�� {i} ���ִ���: {e.Message}", MessageType.Error);

                    // �ṩ�޸���ť
                    if (GUILayout.Button($"�޸���Ƶ�� {i}"))
                    {
                        FixAudioClipItem(i);
                    }
                }
            }

            // ��������С��һ�£���ʾ������޸�ѡ��
            if (arraySize != listCount)
            {
                EditorGUILayout.HelpBox($"��⵽���ݲ�һ��: SerializedProperty��С({arraySize}) != �б��С({listCount})", MessageType.Warning);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("ͬ������"))
                {
                    SynchronizeAudioClipsData();
                }
                if (GUILayout.Button("�ؽ��б�"))
                {
                    RebuildAudioClipsList();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private bool ShouldShowClip(AudioClipData clipData)
        {
            // ��ȫ��飺���clipDataΪnull������ʾ
            if (clipData == null) return false;

            // ��������
            if (!string.IsNullOrEmpty(searchFilter))
            {
                bool matchesId = !string.IsNullOrEmpty(clipData.id) &&
                                clipData.id.ToLower().Contains(searchFilter.ToLower());
                bool matchesClipName = clipData.clip != null &&
                                      clipData.clip.name.ToLower().Contains(searchFilter.ToLower());

                if (!matchesId && !matchesClipName)
                {
                    return false;
                }
            }

            // ���͹���
            if (useTypeFilter && clipData.type != filterType)
            {
                return false;
            }

            return true;
        }

        private void DrawAudioClipItem(SerializedProperty element, int index, AudioClipData clipData)
        {
            // ȷ��element��Ϊnull
            if (element == null) return;

            // ͷ����ID�����͡�ɾ����ť
            EditorGUILayout.BeginHorizontal();

            // ID�ֶ�
            var idProp = element.FindPropertyRelative("id");
            if (idProp != null)
            {
                EditorGUILayout.PropertyField(idProp, new GUIContent("ID"), GUILayout.MinWidth(150));
            }

            // ����ö��
            var typeProp = element.FindPropertyRelative("type");
            if (typeProp != null)
            {
                EditorGUILayout.PropertyField(typeProp, GUIContent.none, GUILayout.Width(80));
            }

            // Ԥ����ť
            bool isPlaying = currentPreviewId == clipData.id && previewSource != null && previewSource.isPlaying;
            string buttonText = isPlaying ? "ֹͣ" : "Ԥ��";
            if (GUILayout.Button(buttonText, GUILayout.Width(50)))
            {
                if (isPlaying)
                {
                    StopPreview();
                }
                else
                {
                    PreviewClip(clipData);
                }
            }

            // ɾ����ť
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("��", GUILayout.Width(25), GUILayout.Height(18)))
            {
                if (EditorUtility.DisplayDialog("ɾ��ȷ��", $"ȷ��Ҫɾ����Ƶ '{clipData.id}' ��", "ɾ��", "ȡ��"))
                {
                    audioClips.DeleteArrayElementAtIndex(index);
                    return;
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            // ��Ƶ�����ֶ�
            var clipProp = element.FindPropertyRelative("clip");
            if (clipProp != null)
            {
                EditorGUILayout.PropertyField(clipProp, new GUIContent("��Ƶ����"));
            }

            // �������ã���һ����ʾ��- �޸�������ʾ
            EditorGUILayout.BeginHorizontal();

            // �������� (0-1)
            var volumeProp = element.FindPropertyRelative("volume");
            if (volumeProp != null)
            {
                EditorGUILayout.LabelField("����", GUILayout.Width(30));
                volumeProp.floatValue = EditorGUILayout.Slider(volumeProp.floatValue, 0f, 1f, GUILayout.MinWidth(80));
            }

            // �������� (0.1-3)
            var pitchProp = element.FindPropertyRelative("pitch");
            if (pitchProp != null)
            {
                EditorGUILayout.LabelField("����", GUILayout.Width(30));
                pitchProp.floatValue = EditorGUILayout.Slider(pitchProp.floatValue, 0.1f, 3f, GUILayout.MinWidth(80));
            }

            EditorGUILayout.EndHorizontal();

            // �ڶ��У�ѭ�����ӳ�
            EditorGUILayout.BeginHorizontal();

            // ѭ������
            var loopProp = element.FindPropertyRelative("loop");
            if (loopProp != null)
            {
                loopProp.boolValue = EditorGUILayout.Toggle("ѭ��", loopProp.boolValue, GUILayout.Width(60));
            }

            // �ӳ�ʱ��
            var delayProp = element.FindPropertyRelative("delay");
            if (delayProp != null)
            {
                EditorGUILayout.LabelField("�ӳ�", GUILayout.Width(30));
                delayProp.floatValue = EditorGUILayout.FloatField(delayProp.floatValue, GUILayout.Width(50));
                EditorGUILayout.LabelField("��", GUILayout.Width(15));
            }

            EditorGUILayout.EndHorizontal();

            // 3D��Ч����
            var is3DProp = element.FindPropertyRelative("is3D");
            if (is3DProp != null)
            {
                is3DProp.boolValue = EditorGUILayout.Toggle("3D��Ч", is3DProp.boolValue);

                if (is3DProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    var maxDistanceProp = element.FindPropertyRelative("maxDistance");
                    if (maxDistanceProp != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("������", GUILayout.Width(60));
                        maxDistanceProp.floatValue = EditorGUILayout.Slider(maxDistanceProp.floatValue, 1f, 500f);
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUI.indentLevel--;
                }
            }

            // ���������
            EditorGUILayout.BeginHorizontal();

            var randomizeVolumeProp = element.FindPropertyRelative("randomizeVolume");
            if (randomizeVolumeProp != null)
            {
                randomizeVolumeProp.boolValue = EditorGUILayout.Toggle("�������", randomizeVolumeProp.boolValue, GUILayout.Width(80));

                if (randomizeVolumeProp.boolValue)
                {
                    var volumeVariationProp = element.FindPropertyRelative("volumeVariation");
                    if (volumeVariationProp != null)
                    {
                        EditorGUILayout.LabelField("��", GUILayout.Width(15));
                        volumeVariationProp.floatValue = EditorGUILayout.Slider(volumeVariationProp.floatValue, 0f, 0.5f, GUILayout.Width(80));
                    }
                }
            }

            var randomizePitchProp = element.FindPropertyRelative("randomizePitch");
            if (randomizePitchProp != null)
            {
                randomizePitchProp.boolValue = EditorGUILayout.Toggle("�������", randomizePitchProp.boolValue, GUILayout.Width(80));

                if (randomizePitchProp.boolValue)
                {
                    var pitchVariationProp = element.FindPropertyRelative("pitchVariation");
                    if (pitchVariationProp != null)
                    {
                        EditorGUILayout.LabelField("��", GUILayout.Width(15));
                        pitchVariationProp.floatValue = EditorGUILayout.Slider(pitchVariationProp.floatValue, 0f, 0.5f, GUILayout.Width(80));
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            // ��ʾ��ǰ���õ�Ԥ����Ϣ
            if (clipData.clip != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"ʱ��: {clipData.clip.length:F2}s", EditorStyles.miniLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField($"Ƶ��: {clipData.clip.frequency}Hz", EditorStyles.miniLabel, GUILayout.Width(100));
                EditorGUILayout.LabelField($"����: {clipData.clip.channels}", EditorStyles.miniLabel, GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawPreviewSection()
        {
            showPreview = EditorGUILayout.Foldout(showPreview, "Ԥ������", true);
            if (showPreview)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"��ǰԤ��: {currentPreviewId}", EditorStyles.miniLabel);

                if (GUILayout.Button("ֹͣ����Ԥ��", GUILayout.Width(100)))
                {
                    StopPreview();
                }
                EditorGUILayout.EndHorizontal();

                // ��������
                if (previewSource != null)
                {
                    float previewVolume = EditorGUILayout.Slider("Ԥ������", previewSource.volume, 0f, 1f);
                    previewSource.volume = previewVolume;
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawUtilityButtons()
        {
            EditorGUILayout.LabelField("ʵ�ù���", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("��֤����"))
            {
                ValidateConfiguration();
            }

            if (GUILayout.Button("���ɴ��볣��"))
            {
                GenerateCodeConstants();
            }

            if (GUILayout.Button("��������"))
            {
                ExportConfiguration();
            }

            if (GUILayout.Button("���������"))
            {
                CleanupEmptyReferences();
            }

            EditorGUILayout.EndHorizontal();
        }

        #region ��������

        private void SetAllVolumes(float volume)
        {
            masterVolume.floatValue = volume;
            musicVolume.floatValue = volume;
            sfxVolume.floatValue = volume;
            voiceVolume.floatValue = volume;
            uiVolume.floatValue = volume;
        }

        private void InitializeNewAudioClip(SerializedProperty element)
        {
            if (element == null) return;

            var idProp = element.FindPropertyRelative("id");
            var typeProp = element.FindPropertyRelative("type");
            var volumeProp = element.FindPropertyRelative("volume");
            var pitchProp = element.FindPropertyRelative("pitch");
            var loopProp = element.FindPropertyRelative("loop");
            var delayProp = element.FindPropertyRelative("delay");
            var is3DProp = element.FindPropertyRelative("is3D");
            var maxDistanceProp = element.FindPropertyRelative("maxDistance");
            var randomizeVolumeProp = element.FindPropertyRelative("randomizeVolume");
            var volumeVariationProp = element.FindPropertyRelative("volumeVariation");
            var randomizePitchProp = element.FindPropertyRelative("randomizePitch");
            var pitchVariationProp = element.FindPropertyRelative("pitchVariation");

            // ��ȫ������Ĭ��ֵ
            if (idProp != null)
                idProp.stringValue = $"new_audio_{System.DateTime.Now.Ticks % 1000}";
            if (typeProp != null)
                typeProp.enumValueIndex = 0;
            if (volumeProp != null)
                volumeProp.floatValue = 1f;
            if (pitchProp != null)
                pitchProp.floatValue = 1f;
            if (loopProp != null)
                loopProp.boolValue = false;
            if (delayProp != null)
                delayProp.floatValue = 0f;
            if (is3DProp != null)
                is3DProp.boolValue = false;
            if (maxDistanceProp != null)
                maxDistanceProp.floatValue = 50f;
            if (randomizeVolumeProp != null)
                randomizeVolumeProp.boolValue = false;
            if (volumeVariationProp != null)
                volumeVariationProp.floatValue = 0.1f;
            if (randomizePitchProp != null)
                randomizePitchProp.boolValue = false;
            if (pitchVariationProp != null)
                pitchVariationProp.floatValue = 0.1f;
        }

        private void BatchImportAudioClips()
        {
            string[] guids = Selection.assetGUIDs;
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("��������", "������Project������ѡ��Ҫ�������Ƶ�ļ�", "ȷ��");
                return;
            }

            try
            {
                var config = target as AudioConfig;
                if (config?.audioClips == null) return;

                int importCount = 0;
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

                    if (clip != null)
                    {
                        // ����Ƿ��Ѵ�����ͬID����Ƶ
                        string clipId = clip.name;
                        bool exists = config.audioClips.Any(data => data.id == clipId);

                        if (exists)
                        {
                            if (!EditorUtility.DisplayDialog("�ظ�ID",
                                $"��ƵID '{clipId}' �Ѵ��ڣ��Ƿ񸲸ǣ�", "����", "����"))
                            {
                                continue;
                            }

                            // �Ƴ�������
                            for (int i = config.audioClips.Count - 1; i >= 0; i--)
                            {
                                if (config.audioClips[i].id == clipId)
                                {
                                    config.audioClips.RemoveAt(i);
                                    break;
                                }
                            }
                        }

                        // �������
                        var newClipData = new AudioClipData(clipId, clip, (AudioType)GuessAudioType(clip.name));
                        config.audioClips.Add(newClipData);
                        importCount++;
                    }
                }

                // ͬ��SerializedProperty
                serializedObject.Update();
                audioClips.arraySize = config.audioClips.Count;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);

                EditorUtility.DisplayDialog("�����������", $"�ɹ����� {importCount} ����Ƶ�ļ�", "ȷ��");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("�������", $"��������ʧ��: {e.Message}", "ȷ��");
                Debug.LogError($"����������Ƶʧ��: {e}");
            }
        }

        private int GuessAudioType(string clipName)
        {
            string name = clipName.ToLower();

            if (name.Contains("music") || name.Contains("bgm") || name.Contains("background"))
                return (int)AudioType.Music;
            else if (name.Contains("ui") || name.Contains("button") || name.Contains("click"))
                return (int)AudioType.UI;
            else if (name.Contains("voice") || name.Contains("speak") || name.Contains("talk"))
                return (int)AudioType.Voice;
            else
                return (int)AudioType.SFX;
        }

        private void CreatePreviewSource()
        {
            if (previewSource == null)
            {
                var go = new GameObject("AudioConfigPreview");
                go.hideFlags = HideFlags.HideAndDontSave;
                previewSource = go.AddComponent<AudioSource>();
                previewSource.playOnAwake = false;
            }
        }

        private void PreviewClip(AudioClipData clipData)
        {
            if (previewSource != null && clipData.clip != null)
            {
                StopPreview();

                previewSource.clip = clipData.clip;
                previewSource.volume = clipData.volume;
                previewSource.pitch = clipData.pitch;
                previewSource.loop = false; // Ԥ��ʱ��ѭ��
                previewSource.Play();

                currentPreviewId = clipData.id;
            }
        }

        private void StopPreview()
        {
            if (previewSource != null && previewSource.isPlaying)
            {
                previewSource.Stop();
            }
            currentPreviewId = "";
        }

        private void ValidateConfiguration()
        {
            var config = target as AudioConfig;
            if (config?.audioClips == null) return;

            var issues = new System.Collections.Generic.List<string>();
            var ids = new System.Collections.Generic.HashSet<string>();

            for (int i = 0; i < config.audioClips.Count; i++)
            {
                var clipData = config.audioClips[i];

                // ���ID
                if (string.IsNullOrEmpty(clipData.id))
                {
                    issues.Add($"�� {i + 1} ��: IDΪ��");
                }
                else if (ids.Contains(clipData.id))
                {
                    issues.Add($"�� {i + 1} ��: ID '{clipData.id}' �ظ�");
                }
                else
                {
                    ids.Add(clipData.id);
                }

                // �����Ƶ����
                if (clipData.clip == null)
                {
                    issues.Add($"�� {i + 1} �� '{clipData.id}': ��Ƶ����Ϊ��");
                }

                // ���������Χ
                if (clipData.volume < 0 || clipData.volume > 1)
                {
                    issues.Add($"�� {i + 1} �� '{clipData.id}': ����������Χ (0-1)");
                }
            }

            if (issues.Count == 0)
            {
                EditorUtility.DisplayDialog("������֤", "������֤ͨ����û�з������⣡", "ȷ��");
            }
            else
            {
                string message = "������������:\n\n" + string.Join("\n", issues);
                EditorUtility.DisplayDialog("������֤", message, "ȷ��");
            }
        }

        private void GenerateCodeConstants()
        {
            var config = target as AudioConfig;
            if (config?.audioClips == null) return;

            var code = new System.Text.StringBuilder();
            code.AppendLine("// �Զ����ɵ���ƵID����");
            code.AppendLine("namespace Audio");
            code.AppendLine("{");
            code.AppendLine("    public static class AudioIDs");
            code.AppendLine("    {");

            foreach (var clipData in config.audioClips)
            {
                if (!string.IsNullOrEmpty(clipData.id))
                {
                    string constantName = clipData.id.ToUpper().Replace(" ", "_").Replace("-", "_");
                    code.AppendLine($"        public const string {constantName} = \"{clipData.id}\";");
                }
            }

            code.AppendLine("    }");
            code.AppendLine("}");

            string path = EditorUtility.SaveFilePanel("������ƵID����", "Assets/Scripts", "AudioIDs", "cs");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, code.ToString());
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("�������", $"��ƵID�����ѱ��浽: {path}", "ȷ��");
            }
        }

        private void ExportConfiguration()
        {
            var config = target as AudioConfig;
            if (config == null) return;

            string json = JsonUtility.ToJson(config, true);
            string path = EditorUtility.SaveFilePanel("������Ƶ����", "Assets", config.name, "json");

            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, json);
                EditorUtility.DisplayDialog("�������", $"�����ѵ�����: {path}", "ȷ��");
            }
        }

        private void CleanupEmptyReferences()
        {
            var config = target as AudioConfig;
            if (config?.audioClips == null) return;

            int removedCount = 0;
            for (int i = config.audioClips.Count - 1; i >= 0; i--)
            {
                if (config.audioClips[i].clip == null || string.IsNullOrEmpty(config.audioClips[i].id))
                {
                    config.audioClips.RemoveAt(i);
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                EditorUtility.SetDirty(config);
                EditorUtility.DisplayDialog("�������", $"������ {removedCount} ��������", "ȷ��");
            }
            else
            {
                EditorUtility.DisplayDialog("�������", "û�з��ֿ�����", "ȷ��");
            }
        }

        #region �����޸�����

        /// <summary>
        /// �޸�������Ƶ��
        /// </summary>
        private void FixAudioClipItem(int index)
        {
            var config = target as AudioConfig;
            if (config?.audioClips == null) return;

            try
            {
                // ȷ��������Ч
                if (index >= 0 && index < audioClips.arraySize)
                {
                    var element = audioClips.GetArrayElementAtIndex(index);
                    InitializeNewAudioClip(element);

                    // ����б��ж�Ӧλ�õ�����Ϊ�գ������µ�
                    if (index >= config.audioClips.Count)
                    {
                        while (config.audioClips.Count <= index)
                        {
                            config.audioClips.Add(new AudioClipData("", null, AudioType.SFX));
                        }
                    }

                    EditorUtility.SetDirty(target);
                    Debug.Log($"���޸���Ƶ�� {index}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"�޸���Ƶ�� {index} ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// ͬ����Ƶ��������
        /// </summary>
        private void SynchronizeAudioClipsData()
        {
            var config = target as AudioConfig;
            if (config?.audioClips == null) return;

            try
            {
                serializedObject.Update();

                int arraySize = audioClips.arraySize;
                int listCount = config.audioClips.Count;

                Debug.Log($"ͬ��ǰ: SerializedProperty��С={arraySize}, �б��С={listCount}");

                // ���SerializedProperty���󣬵����б��С
                if (arraySize > listCount)
                {
                    while (config.audioClips.Count < arraySize)
                    {
                        config.audioClips.Add(new AudioClipData("", null, AudioType.SFX));
                    }
                }
                // ����б���󣬵���SerializedProperty��С
                else if (listCount > arraySize)
                {
                    audioClips.arraySize = listCount;
                }

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);

                Debug.Log($"ͬ����: SerializedProperty��С={audioClips.arraySize}, �б��С={config.audioClips.Count}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ͬ������ʧ��: {e.Message}");
            }
        }

        /// <summary>
        /// �ؽ���Ƶ�����б�
        /// </summary>
        private void RebuildAudioClipsList()
        {
            if (EditorUtility.DisplayDialog("�ؽ��б�", "�⽫��յ�ǰ�б����´�����ȷ��������", "ȷ��", "ȡ��"))
            {
                var config = target as AudioConfig;
                if (config != null)
                {
                    config.audioClips.Clear();
                    audioClips.arraySize = 0;

                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);

                    Debug.Log("��Ƶ�����б����ؽ�");
                }
            }
        }
    }

        #endregion

        // ������Ƶ���õĿ�ݲ˵�
namespace Audio.Editor
    {
        public static class AudioConfigMenuItems
        {
            [MenuItem("Assets/Create/Audio/Audio Config", priority = 1)]
            public static void CreateAudioConfig()
            {
                string path = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (string.IsNullOrEmpty(path))
                    path = "Assets";
                else if (System.IO.Path.HasExtension(path))
                    path = path.Replace(System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), "");

                string configPath = AssetDatabase.GenerateUniqueAssetPath(path + "/NewAudioConfig.asset");

                var config = ScriptableObject.CreateInstance<AudioConfig>();
                AssetDatabase.CreateAsset(config, configPath);
                AssetDatabase.SaveAssets();

                EditorUtility.FocusProjectWindow();
                Selection.activeObject = config;
            }

            [MenuItem("Tools/Audio/Open Audio Manager Settings")]
            public static void OpenAudioManagerSettings()
            {
                var audioManager = Object.FindObjectOfType<AudioManager>();
                if (audioManager != null)
                {
                    Selection.activeObject = audioManager;
                    EditorGUIUtility.PingObject(audioManager);
                }
                else
                {
                    EditorUtility.DisplayDialog("Audio Manager", "������û���ҵ�AudioManager", "ȷ��");
                }
            }

            [MenuItem("Tools/Audio/Create Audio Manager")]
            public static void CreateAudioManager()
            {
                var existing = Object.FindObjectOfType<AudioManager>();
                if (existing != null)
                {
                    EditorUtility.DisplayDialog("Audio Manager", "�������Ѵ���AudioManager", "ȷ��");
                    return;
                }

                var go = new GameObject("AudioManager");
                go.AddComponent<AudioManager>();

                EditorUtility.DisplayDialog("Audio Manager", "AudioManager�Ѵ���", "ȷ��");
            }
        }
    }
}
#endregion
#endif