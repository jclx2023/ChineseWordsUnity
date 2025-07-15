// 1. ��Ƶ����ö��
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;

namespace Audio
{
    /// <summary>
    /// ��Ƶ����ö��
    /// </summary>
    public enum AudioType
    {
        Music,      // ��������
        SFX,        // ��Ч
        Voice,      // ����
        UI          // UI��Ч
    }

    /// <summary>
    /// ��Ƶ���뵭������
    /// </summary>
    public enum FadeType
    {
        None,       // �޵��뵭��
        FadeIn,     // ����
        FadeOut,    // ����
        CrossFade   // ���浭�뵭��
    }
}


namespace Audio
{
    /// <summary>
    /// ������Ƶ��������
    /// </summary>
    [System.Serializable]
    public class AudioClipData
    {
        [Header("��������")]
        public string id;                    // ��ƵID
        public AudioClip clip;               // ��Ƶ����
        public AudioType type;               // ��Ƶ����

        [Header("��������")]
        [Range(0f, 1f)] public float volume = 1f;           // ����
        [Range(0.1f, 3f)] public float pitch = 1f;          // ����
        public bool loop = false;                            // �Ƿ�ѭ��
        public float delay = 0f;                            // �ӳٲ���

        [Header("3D��Ч����")]
        public bool is3D = false;                           // �Ƿ�Ϊ3D��Ч
        [Range(0f, 500f)] public float maxDistance = 50f;  // ������

        [Header("�����")]
        public bool randomizeVolume = false;                // ���������
        [Range(0f, 0.3f)] public float volumeVariation = 0.1f;    // �����仯��Χ
        public bool randomizePitch = false;                 // ���������
        [Range(0f, 0.3f)] public float pitchVariation = 0.1f;     // �����仯��Χ

        public AudioClipData(string id, AudioClip clip, AudioType type)
        {
            this.id = id;
            this.clip = clip;
            this.type = type;
        }
    }

    /// <summary>
    /// ��Ƶ������Դ - ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "Audio/Audio Config", order = 1)]
    public class AudioConfig : ScriptableObject
    {
        [Header("��Ƶ�����б�")]
        public List<AudioClipData> audioClips = new List<AudioClipData>();

        [Header("ȫ����������")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 0.7f;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        [Range(0f, 1f)] public float voiceVolume = 1f;
        [Range(0f, 1f)] public float uiVolume = 0.6f;

        [Header("��Ƶ������")]
        public int musicSourceCount = 2;     // ����Դ������֧�ֽ��浭�뵭����
        public int sfxSourceCount = 10;      // ��ЧԴ����
        public int voiceSourceCount = 3;     // ����Դ����
        public int uiSourceCount = 5;        // UI��ЧԴ����

        /// <summary>
        /// ����ID������Ƶ����
        /// </summary>
        public AudioClipData GetAudioData(string id)
        {
            return audioClips.Find(data => data.id == id);
        }

        /// <summary>
        /// �������ͻ�ȡ����
        /// </summary>
        public float GetVolumeByType(AudioType type)
        {
            switch (type)
            {
                case AudioType.Music: return musicVolume;
                case AudioType.SFX: return sfxVolume;
                case AudioType.Voice: return voiceVolume;
                case AudioType.UI: return uiVolume;
                default: return 1f;
            }
        }
    }
}



namespace Audio
{
    /// <summary>
    /// ȫ����Ƶ������ - ����ģʽ
    /// ʹ�÷�ʽ��AudioManager.PlaySFX("button_click");
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        #region ����
        private static AudioManager instance;
        public static AudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("AudioManager");
                    instance = go.AddComponent<AudioManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }
        #endregion

        #region ���ò���
        [Header("��Ƶ����")]
        [SerializeField] private AudioConfig audioConfig;
        [SerializeField] private bool autoLoadConfig = true;
        [SerializeField] private string configPath = "Audio/AudioConfig";

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showAudioSourcesInHierarchy = false;
        #endregion

        #region ˽�б���
        private Dictionary<AudioType, Queue<AudioSource>> audioSourcePools;
        private Dictionary<AudioType, List<AudioSource>> activeAudioSources;
        private Dictionary<string, AudioClipData> audioDatabase;

        private AudioSource currentMusicSource;
        private AudioSource fadingMusicSource;
        private Coroutine musicFadeCoroutine;

        private Transform audioSourceContainer;
        #endregion

        #region ��̬�ӿ� - ����
        public static void PlayMusic(string musicId) => Instance.PlayMusicInternal(musicId);
        public static void PlayMusic(string musicId, float fadeTime) => Instance.PlayMusicInternal(musicId, fadeTime);
        public static void StopMusic() => Instance.StopMusicInternal();
        public static void StopMusic(float fadeTime) => Instance.StopMusicInternal(fadeTime);
        public static void PauseMusic() => Instance.PauseMusicInternal();
        public static void ResumeMusic() => Instance.ResumeMusicInternal();
        public static void SetMusicVolume(float volume) => Instance.SetMusicVolumeInternal(volume);
        #endregion

        #region ��̬�ӿ� - ��Ч
        public static void PlaySFX(string sfxId) => Instance.PlaySFXInternal(sfxId);
        public static void PlaySFX(string sfxId, Vector3 position) => Instance.PlaySFX3DInternal(sfxId, position);
        public static void PlaySFX(string sfxId, float volume) => Instance.PlaySFXInternal(sfxId, volume);
        public static void PlaySFX(string sfxId, float volume, float pitch) => Instance.PlaySFXInternal(sfxId, volume, pitch);
        public static void StopAllSFX() => Instance.StopAllSFXInternal();
        public static void SetSFXVolume(float volume) => Instance.SetSFXVolumeInternal(volume);
        #endregion

        #region ��̬�ӿ� - ������UI
        public static void PlayVoice(string voiceId) => Instance.PlayVoiceInternal(voiceId);
        public static void PlayUI(string uiId) => Instance.PlayUIInternal(uiId);
        public static void StopAllVoice() => Instance.StopAllVoiceInternal();
        public static void SetVoiceVolume(float volume) => Instance.SetVoiceVolumeInternal(volume);
        public static void SetUIVolume(float volume) => Instance.SetUIVolumeInternal(volume);
        #endregion

        #region ��̬�ӿ� - ȫ�ֿ���
        public static void SetMasterVolume(float volume) => Instance.SetMasterVolumeInternal(volume);
        public static void StopAll() => Instance.StopAllInternal();
        public static void PauseAll() => Instance.PauseAllInternal();
        public static void ResumeAll() => Instance.ResumeAllInternal();
        public static bool IsPlaying(string audioId) => Instance.IsPlayingInternal(audioId);
        #endregion

        #region Unity��������
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // ��MainMenuManagerͬ����������
            SyncVolumeFromPlayerPrefs();
        }
        #endregion

        #region ��ʼ��
        private void Initialize()
        {
            LoadAudioConfig();
            CreateAudioSourceContainer();
            InitializeAudioSourcePools();
            BuildAudioDatabase();

            LogDebug("AudioManager ��ʼ�����");
        }

        private void LoadAudioConfig()
        {
            if (audioConfig == null && autoLoadConfig)
            {
                audioConfig = Resources.Load<AudioConfig>(configPath);
                if (audioConfig == null)
                {
                    LogDebug($"δ�ҵ���Ƶ�����ļ�: {configPath}��ʹ��Ĭ������");
                    CreateDefaultConfig();
                }
            }
        }

        private void CreateAudioSourceContainer()
        {
            audioSourceContainer = new GameObject("AudioSources").transform;
            audioSourceContainer.SetParent(transform);

            if (!showAudioSourcesInHierarchy)
                audioSourceContainer.gameObject.hideFlags = HideFlags.HideInHierarchy;
        }

        private void InitializeAudioSourcePools()
        {
            audioSourcePools = new Dictionary<AudioType, Queue<AudioSource>>();
            activeAudioSources = new Dictionary<AudioType, List<AudioSource>>();

            // ��ʼ����������ƵԴ��
            CreateAudioSourcePool(AudioType.Music, audioConfig?.musicSourceCount ?? 2);
            CreateAudioSourcePool(AudioType.SFX, audioConfig?.sfxSourceCount ?? 10);
            CreateAudioSourcePool(AudioType.Voice, audioConfig?.voiceSourceCount ?? 3);
            CreateAudioSourcePool(AudioType.UI, audioConfig?.uiSourceCount ?? 5);
        }

        private void CreateAudioSourcePool(AudioType type, int count)
        {
            var pool = new Queue<AudioSource>();
            var activeList = new List<AudioSource>();

            for (int i = 0; i < count; i++)
            {
                GameObject sourceGO = new GameObject($"{type}Source_{i}");
                sourceGO.transform.SetParent(audioSourceContainer);

                AudioSource source = sourceGO.AddComponent<AudioSource>();
                ConfigureAudioSource(source, type);

                pool.Enqueue(source);
            }

            audioSourcePools[type] = pool;
            activeAudioSources[type] = activeList;
        }

        private void ConfigureAudioSource(AudioSource source, AudioType type)
        {
            source.playOnAwake = false;
            source.volume = audioConfig?.GetVolumeByType(type) ?? 1f;

            // 3D��Ч����
            if (type == AudioType.SFX)
            {
                source.spatialBlend = 0f; // Ĭ��2D�����ڲ���ʱ����
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.maxDistance = 50f;
            }
        }

        private void BuildAudioDatabase()
        {
            audioDatabase = new Dictionary<string, AudioClipData>();

            if (audioConfig?.audioClips != null)
            {
                foreach (var clipData in audioConfig.audioClips)
                {
                    if (!string.IsNullOrEmpty(clipData.id))
                    {
                        audioDatabase[clipData.id] = clipData;
                    }
                }
            }

            LogDebug($"��Ƶ���ݿ⹹����ɣ��� {audioDatabase.Count} ����Ƶ");
        }

        private void CreateDefaultConfig()
        {
            // ���û�������ļ�������Ĭ������
            audioConfig = ScriptableObject.CreateInstance<AudioConfig>();
            audioConfig.name = "DefaultAudioConfig";
        }
        #endregion

        #region ��Ƶ���ź��ķ���
        private AudioSource GetAudioSource(AudioType type)
        {
            if (audioSourcePools[type].Count > 0)
            {
                var source = audioSourcePools[type].Dequeue();
                activeAudioSources[type].Add(source);
                return source;
            }

            // �������û�п���Դ�����Ի�������ɵ�Դ
            RecycleFinishedSources(type);

            if (audioSourcePools[type].Count > 0)
            {
                var source = audioSourcePools[type].Dequeue();
                activeAudioSources[type].Add(source);
                return source;
            }

            LogDebug($"���棺{type} ������ƵԴ����");
            return null;
        }

        private void RecycleFinishedSources(AudioType type)
        {
            var activeList = activeAudioSources[type];
            for (int i = activeList.Count - 1; i >= 0; i--)
            {
                if (!activeList[i].isPlaying)
                {
                    ReturnAudioSource(activeList[i], type);
                }
            }
        }

        private void ReturnAudioSource(AudioSource source, AudioType type)
        {
            source.Stop();
            source.clip = null;
            activeAudioSources[type].Remove(source);
            audioSourcePools[type].Enqueue(source);
        }

        private void PlayAudio(string audioId, AudioType? forceType = null, float? volumeOverride = null, float? pitchOverride = null, Vector3? position = null)
        {
            if (!audioDatabase.TryGetValue(audioId, out AudioClipData clipData))
            {
                LogDebug($"δ�ҵ���Ƶ: {audioId}");
                return;
            }

            AudioType audioType = forceType ?? clipData.type;
            AudioSource source = GetAudioSource(audioType);

            if (source == null) return;

            // ������ƵԴ
            source.clip = clipData.clip;
            source.loop = clipData.loop;

            // �������㣺�������� * �������� * ������ * ��������
            float volume = clipData.volume;
            volume *= audioConfig?.GetVolumeByType(audioType) ?? 1f;
            volume *= audioConfig?.masterVolume ?? 1f;
            volume *= volumeOverride ?? 1f;

            // ���������
            if (clipData.randomizeVolume)
            {
                volume *= Random.Range(1f - clipData.volumeVariation, 1f + clipData.volumeVariation);
            }

            source.volume = Mathf.Clamp01(volume);

            // ��������
            float pitch = pitchOverride ?? clipData.pitch;
            if (clipData.randomizePitch)
            {
                pitch *= Random.Range(1f - clipData.pitchVariation, 1f + clipData.pitchVariation);
            }
            source.pitch = pitch;

            // 3D��Ч����
            if (clipData.is3D && position.HasValue)
            {
                source.spatialBlend = 1f;
                source.maxDistance = clipData.maxDistance;
                source.transform.position = position.Value;
            }
            else
            {
                source.spatialBlend = 0f;
            }

            // ����
            if (clipData.delay > 0)
            {
                source.PlayDelayed(clipData.delay);
            }
            else
            {
                source.Play();
            }

            LogDebug($"������Ƶ: {audioId} ({audioType})");
        }
        #endregion

        #region �ڲ�ʵ�ַ���
        // �������
        private void PlayMusicInternal(string musicId, float fadeTime = 0f)
        {
            if (fadeTime > 0f && currentMusicSource != null && currentMusicSource.isPlaying)
            {
                StartCoroutine(CrossFadeMusic(musicId, fadeTime));
            }
            else
            {
                StopMusicInternal();
                PlayAudio(musicId, AudioType.Music);
                currentMusicSource = activeAudioSources[AudioType.Music].LastOrDefault();
            }
        }

        private void StopMusicInternal(float fadeTime = 0f)
        {
            if (currentMusicSource != null)
            {
                if (fadeTime > 0f)
                {
                    StartCoroutine(FadeOutMusic(fadeTime));
                }
                else
                {
                    currentMusicSource.Stop();
                    ReturnAudioSource(currentMusicSource, AudioType.Music);
                    currentMusicSource = null;
                }
            }
        }

        private void PauseMusicInternal()
        {
            currentMusicSource?.Pause();
        }

        private void ResumeMusicInternal()
        {
            currentMusicSource?.UnPause();
        }

        private void SetMusicVolumeInternal(float volume)
        {
            if (audioConfig != null)
            {
                audioConfig.musicVolume = Mathf.Clamp01(volume);
                UpdateAllSourcesVolume(AudioType.Music);
            }
        }

        // ��Ч���
        private void PlaySFXInternal(string sfxId, float? volume = null, float? pitch = null)
        {
            PlayAudio(sfxId, AudioType.SFX, volume, pitch);
        }

        private void PlaySFX3DInternal(string sfxId, Vector3 position)
        {
            PlayAudio(sfxId, AudioType.SFX, position: position);
        }

        private void StopAllSFXInternal()
        {
            StopAllAudioOfType(AudioType.SFX);
        }

        private void SetSFXVolumeInternal(float volume)
        {
            if (audioConfig != null)
            {
                audioConfig.sfxVolume = Mathf.Clamp01(volume);
                UpdateAllSourcesVolume(AudioType.SFX);
            }
        }

        // �������
        private void PlayVoiceInternal(string voiceId)
        {
            PlayAudio(voiceId, AudioType.Voice);
        }

        private void StopAllVoiceInternal()
        {
            StopAllAudioOfType(AudioType.Voice);
        }

        private void SetVoiceVolumeInternal(float volume)
        {
            if (audioConfig != null)
            {
                audioConfig.voiceVolume = Mathf.Clamp01(volume);
                UpdateAllSourcesVolume(AudioType.Voice);
            }
        }

        // UI��Ч���
        private void PlayUIInternal(string uiId)
        {
            PlayAudio(uiId, AudioType.UI);
        }

        private void SetUIVolumeInternal(float volume)
        {
            if (audioConfig != null)
            {
                audioConfig.uiVolume = Mathf.Clamp01(volume);
                UpdateAllSourcesVolume(AudioType.UI);
            }
        }

        // ȫ�ֿ���
        private void SetMasterVolumeInternal(float volume)
        {
            if (audioConfig != null)
            {
                audioConfig.masterVolume = Mathf.Clamp01(volume);
                UpdateAllSourcesVolume();
            }
        }

        private void StopAllInternal()
        {
            foreach (var type in System.Enum.GetValues(typeof(AudioType)).Cast<AudioType>())
            {
                StopAllAudioOfType(type);
            }
        }

        private void PauseAllInternal()
        {
            foreach (var activeList in activeAudioSources.Values)
            {
                foreach (var source in activeList)
                {
                    if (source.isPlaying) source.Pause();
                }
            }
        }

        private void ResumeAllInternal()
        {
            foreach (var activeList in activeAudioSources.Values)
            {
                foreach (var source in activeList)
                {
                    source.UnPause();
                }
            }
        }

        private bool IsPlayingInternal(string audioId)
        {
            if (!audioDatabase.TryGetValue(audioId, out AudioClipData clipData))
                return false;

            var activeList = activeAudioSources[clipData.type];
            return activeList.Any(source => source.clip == clipData.clip && source.isPlaying);
        }
        #endregion

        #region ��������
        private void StopAllAudioOfType(AudioType type)
        {
            var activeList = activeAudioSources[type];
            for (int i = activeList.Count - 1; i >= 0; i--)
            {
                ReturnAudioSource(activeList[i], type);
            }
        }

        private void UpdateAllSourcesVolume(AudioType? specificType = null)
        {
            var typesToUpdate = specificType.HasValue ?
                new[] { specificType.Value } :
                System.Enum.GetValues(typeof(AudioType)).Cast<AudioType>();

            foreach (var type in typesToUpdate)
            {
                float typeVolume = audioConfig?.GetVolumeByType(type) ?? 1f;
                float masterVolume = audioConfig?.masterVolume ?? 1f;
                float finalVolume = typeVolume * masterVolume;

                foreach (var source in activeAudioSources[type])
                {
                    source.volume = finalVolume;
                }

                foreach (var source in audioSourcePools[type])
                {
                    source.volume = finalVolume;
                }
            }
        }

        private void SyncVolumeFromPlayerPrefs()
        {
            if (audioConfig != null)
            {
                audioConfig.masterVolume = PlayerPrefs.GetFloat("MasterVolume", 0.8f);
                audioConfig.musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
                audioConfig.sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.8f);

                UpdateAllSourcesVolume();
                LogDebug("��ͬ��PlayerPrefs�е���������");
            }
        }
        #endregion

        #region ���ֵ��뵭��Э��
        private IEnumerator CrossFadeMusic(string newMusicId, float fadeTime)
        {
            // ���浱ǰ����Դ��Ϊ����Դ
            fadingMusicSource = currentMusicSource;

            // ����������
            PlayAudio(newMusicId, AudioType.Music);
            currentMusicSource = activeAudioSources[AudioType.Music].LastOrDefault();

            if (currentMusicSource != null)
                currentMusicSource.volume = 0f;

            float elapsed = 0f;
            float originalVolume = fadingMusicSource?.volume ?? 0f;
            float targetVolume = audioConfig?.musicVolume ?? 1f;
            targetVolume *= audioConfig?.masterVolume ?? 1f;

            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / fadeTime;

                // ����������
                if (fadingMusicSource != null)
                    fadingMusicSource.volume = Mathf.Lerp(originalVolume, 0f, progress);

                // ����������
                if (currentMusicSource != null)
                    currentMusicSource.volume = Mathf.Lerp(0f, targetVolume, progress);

                yield return null;
            }

            // ֹͣ����������
            if (fadingMusicSource != null)
            {
                ReturnAudioSource(fadingMusicSource, AudioType.Music);
                fadingMusicSource = null;
            }

            // ȷ��������������ȷ
            if (currentMusicSource != null)
                currentMusicSource.volume = targetVolume;
        }

        private IEnumerator FadeOutMusic(float fadeTime)
        {
            if (currentMusicSource == null) yield break;

            float startVolume = currentMusicSource.volume;
            float elapsed = 0f;

            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                currentMusicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeTime);
                yield return null;
            }

            currentMusicSource.Stop();
            ReturnAudioSource(currentMusicSource, AudioType.Music);
            currentMusicSource = null;
        }
        #endregion

        #region ���Է���
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[AudioManager] {message}");
            }
        }
        #endregion
    }
}