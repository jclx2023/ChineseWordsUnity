// �򻯵���Ƶ����ö��
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System.Linq;

namespace Audio
{
    /// <summary>
    /// �򻯵���Ƶ����ö��
    /// </summary>
    public enum AudioType
    {
        Music,      // ��������
        SFX,        // ��Ч
        Voice,      // ����
        UI          // UI��Ч
    }

    /// <summary>
    /// �򻯵���Ƶ��������
    /// </summary>
    [System.Serializable]
    public class AudioClipData
    {
        public string id;                    // ��ƵID
        public AudioClip clip;               // ��Ƶ����
        public AudioType type;               // ��Ƶ����
        [Range(0f, 1f)] public float volume = 1f;           // ����
        [Range(0.1f, 3f)] public float pitch = 1f;          // ����
        public bool loop = false;                            // �Ƿ�ѭ��

        public AudioClipData(string id, AudioClip clip, AudioType type)
        {
            this.id = id;
            this.clip = clip;
            this.type = type;
        }
    }

    /// <summary>
    /// �򻯵���Ƶ���� - ʹ��Runtime����ģʽ������ScriptableObject�����л�����
    /// </summary>
    public class AudioConfig
    {
        public List<AudioClipData> audioClips = new List<AudioClipData>();

        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 0.7f;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        [Range(0f, 1f)] public float voiceVolume = 1f;
        [Range(0f, 1f)] public float uiVolume = 0.6f;

        public int musicSourceCount = 2;
        public int sfxSourceCount = 8;
        public int voiceSourceCount = 3;
        public int uiSourceCount = 4;

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

        /// <summary>
        /// ����Ƶ�����Զ���������
        /// </summary>
        public void AutoCreateFromClips(AudioClip[] clips)
        {
            audioClips.Clear();

            foreach (var clip in clips)
            {
                if (clip != null)
                {
                    var type = GuessAudioType(clip.name);
                    var clipData = new AudioClipData(clip.name, clip, type);
                    audioClips.Add(clipData);
                }
            }
        }

        /// <summary>
        /// ��ָ�����ʹ�����Ƶ����
        /// </summary>
        public void CreateFromClips(AudioClip[] clips, AudioType audioType)
        {
            foreach (var clip in clips)
            {
                if (clip != null)
                {
                    var clipData = new AudioClipData(clip.name, clip, audioType);
                    audioClips.Add(clipData);
                }
            }
        }

        private AudioType GuessAudioType(string clipName)
        {
            string name = clipName.ToLower();

            if (name.Contains("music") || name.Contains("bgm") || name.Contains("background"))
                return AudioType.Music;
            else if (name.Contains("ui") || name.Contains("button") || name.Contains("click"))
                return AudioType.UI;
            else if (name.Contains("voice") || name.Contains("speak") || name.Contains("talk"))
                return AudioType.Voice;
            else
                return AudioType.SFX;
        }
    }
}

namespace Audio
{
    /// <summary>
    /// �����ȶ�����Ƶ������
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
                    var go = new GameObject("AudioManager");
                    instance = go.AddComponent<AudioManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }
        #endregion

        #region ���ò���
        [Header("��Ƶ�ļ���·��")]
        [SerializeField] private string musicFolderPath = "Audio/Music";     // �����ļ���
        [SerializeField] private string sfxFolderPath = "Audio/SFX";         // ��Ч�ļ���
        [SerializeField] private string voiceFolderPath = "Audio/Voice";     // �����ļ���
        [SerializeField] private string uiFolderPath = "Audio/UI";           // UI��Ч�ļ���

        [Header("��������")]
        [SerializeField] private bool enableDebugLogs = true;
        #endregion

        #region ˽�б���
        private AudioConfig audioConfig;
        private Dictionary<AudioType, Queue<AudioSource>> audioSourcePools;
        private Dictionary<AudioType, List<AudioSource>> activeAudioSources;
        private Dictionary<string, AudioClipData> audioDatabase;

        private AudioSource currentMusicSource;
        private Transform audioSourceContainer;
        #endregion

        #region ��̬�ӿ�
        /// <summary>
        /// �������֣���ѡ���Ƿ�ѭ����
        /// </summary>
        public static void PlayMusic(string musicId, bool loop = true) => Instance.PlayMusicInternal(musicId, loop);

        /// <summary>
        /// ������Ч��ͨ����ѭ����
        /// </summary>
        public static void PlaySFX(string sfxId) => Instance.PlaySFXInternal(sfxId);

        /// <summary>
        /// ����������ͨ����ѭ����
        /// </summary>
        public static void PlayVoice(string voiceId) => Instance.PlayVoiceInternal(voiceId);

        /// <summary>
        /// ����UI��Ч��ͨ����ѭ����
        /// </summary>
        public static void PlayUI(string uiId) => Instance.PlayUIInternal(uiId);

        public static void StopMusic() => Instance.StopMusicInternal();
        public static void StopAllSFX() => Instance.StopAllSFXInternal();
        public static void StopAll() => Instance.StopAllInternal();

        // ��ͣ�ͻָ�����
        public static void PauseMusic() => Instance.PauseMusicInternal();
        public static void ResumeMusic() => Instance.ResumeMusicInternal();
        public static void PauseAll() => Instance.PauseAllInternal();
        public static void ResumeAll() => Instance.ResumeAllInternal();
        public static void PauseSFX() => Instance.PauseAllSFXInternal();
        public static void ResumeSFX() => Instance.ResumeAllSFXInternal();

        // ��������
        public static void SetMasterVolume(float volume) => Instance.SetMasterVolumeInternal(volume);
        public static void SetMusicVolume(float volume) => Instance.SetMusicVolumeInternal(volume);
        public static void SetSFXVolume(float volume) => Instance.SetSFXVolumeInternal(volume);

        // ״̬��ѯ
        public static bool IsMusicPlaying() => Instance.IsMusicPlayingInternal();
        public static bool IsMusicPaused() => Instance.IsMusicPausedInternal();
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
            SyncVolumeFromPlayerPrefs();
        }
        #endregion

        #region ��ʼ��
        private void Initialize()
        {
            CreateAudioConfig();
            LoadAudioClips();
            CreateAudioSourceContainer();
            InitializeAudioSourcePools();
            BuildAudioDatabase();

            LogDebug("��AudioManager��ʼ�����");
        }

        private void CreateAudioConfig()
        {
            audioConfig = new AudioConfig();
        }

        private void LoadAudioClips()
        {
            LogDebug("��ʼ�ӷ����ļ��м�����Ƶ�ļ�");

            // �Ӹ��������ļ��м�����Ƶ
            LoadAudioFromFolder(musicFolderPath, AudioType.Music);
            LoadAudioFromFolder(sfxFolderPath, AudioType.SFX);
            LoadAudioFromFolder(voiceFolderPath, AudioType.Voice);
            LoadAudioFromFolder(uiFolderPath, AudioType.UI);

            LogDebug($"��Ƶ������ɣ��ܼ� {audioConfig.audioClips.Count} ����Ƶ�ļ�");
        }

        private void LoadAudioFromFolder(string folderPath, AudioType audioType)
        {
            var audioClips = Resources.LoadAll<AudioClip>(folderPath);

            foreach (var clip in audioClips)
            {
                if (clip != null)
                {
                    var clipData = new AudioClipData(clip.name, clip, audioType);
                    audioConfig.audioClips.Add(clipData);
                }
            }

            LogDebug($"�� {folderPath} ������ {audioClips.Length} �� {audioType} ������Ƶ");
        }

        private void CreateAudioSourceContainer()
        {
            audioSourceContainer = new GameObject("AudioSources").transform;
            audioSourceContainer.SetParent(transform);
        }

        private void InitializeAudioSourcePools()
        {
            audioSourcePools = new Dictionary<AudioType, Queue<AudioSource>>();
            activeAudioSources = new Dictionary<AudioType, List<AudioSource>>();

            CreateAudioSourcePool(AudioType.Music, audioConfig.musicSourceCount);
            CreateAudioSourcePool(AudioType.SFX, audioConfig.sfxSourceCount);
            CreateAudioSourcePool(AudioType.Voice, audioConfig.voiceSourceCount);
            CreateAudioSourcePool(AudioType.UI, audioConfig.uiSourceCount);
        }

        private void CreateAudioSourcePool(AudioType type, int count)
        {
            var pool = new Queue<AudioSource>();
            var activeList = new List<AudioSource>();

            for (int i = 0; i < count; i++)
            {
                var sourceGO = new GameObject($"{type}Source_{i}");
                sourceGO.transform.SetParent(audioSourceContainer);

                var source = sourceGO.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f; // ȫ����Ϊ2D��Ч
                source.volume = audioConfig.GetVolumeByType(type);

                pool.Enqueue(source);
            }

            audioSourcePools[type] = pool;
            activeAudioSources[type] = activeList;
        }

        private void BuildAudioDatabase()
        {
            audioDatabase = new Dictionary<string, AudioClipData>();

            foreach (var clipData in audioConfig.audioClips)
            {
                if (!string.IsNullOrEmpty(clipData.id))
                {
                    audioDatabase[clipData.id] = clipData;
                }
            }

            LogDebug($"��Ƶ���ݿ⹹����ɣ��� {audioDatabase.Count} ����Ƶ");
        }
        #endregion

        #region ��Ƶ���ź��ķ���
        private AudioSource GetAudioSource(AudioType type)
        {
            // �Ȼ�������ɵ�Դ
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

        private void PlayAudio(string audioId, AudioType audioType)
        {
            if (!audioDatabase.TryGetValue(audioId, out AudioClipData clipData))
            {
                LogDebug($"δ�ҵ���Ƶ: {audioId}");
                return;
            }

            var source = GetAudioSource(audioType);
            if (source == null) return;

            source.clip = clipData.clip;
            source.loop = clipData.loop;
            source.volume = clipData.volume * audioConfig.GetVolumeByType(audioType) * audioConfig.masterVolume;
            source.pitch = clipData.pitch;

            source.Play();
            LogDebug($"������Ƶ: {audioId} ({audioType})");
        }
        #endregion

        #region �ڲ�ʵ�ַ���
        private void PlayMusicInternal(string musicId, bool loop = true)
        {
            StopMusicInternal();

            if (!audioDatabase.TryGetValue(musicId, out AudioClipData clipData))
            {
                LogDebug($"δ�ҵ�����: {musicId}");
                return;
            }

            var source = GetAudioSource(AudioType.Music);
            if (source == null) return;

            source.clip = clipData.clip;
            source.loop = loop;  // ʹ�ô����loop����
            source.volume = clipData.volume * audioConfig.GetVolumeByType(AudioType.Music) * audioConfig.masterVolume;
            source.pitch = clipData.pitch;
            source.spatialBlend = 0f;  // 2D��Ч

            source.Play();
            currentMusicSource = source;

            LogDebug($"��������: {musicId} (ѭ��: {source.loop})");
        }

        private void PlaySFXInternal(string sfxId)
        {
            PlayAudio(sfxId, AudioType.SFX);
        }

        private void PlayVoiceInternal(string voiceId)
        {
            PlayAudio(voiceId, AudioType.Voice);
        }

        private void PlayUIInternal(string uiId)
        {
            PlayAudio(uiId, AudioType.UI);
        }

        private void StopMusicInternal()
        {
            if (currentMusicSource != null)
            {
                ReturnAudioSource(currentMusicSource, AudioType.Music);
                currentMusicSource = null;
            }
        }

        private void StopAllSFXInternal()
        {
            StopAllAudioOfType(AudioType.SFX);
        }

        private void StopAllInternal()
        {
            foreach (var type in System.Enum.GetValues(typeof(AudioType)))
            {
                StopAllAudioOfType((AudioType)type);
            }
        }

        // ��ͣ�ͻָ��ڲ�ʵ��
        private void PauseMusicInternal()
        {
            if (currentMusicSource != null && currentMusicSource.isPlaying)
            {
                currentMusicSource.Pause();
                LogDebug("��������ͣ");
            }
        }

        private void ResumeMusicInternal()
        {
            if (currentMusicSource != null && !currentMusicSource.isPlaying)
            {
                currentMusicSource.UnPause();
                LogDebug("�����ѻָ�");
            }
        }

        private void PauseAllInternal()
        {
            foreach (var type in System.Enum.GetValues(typeof(AudioType)))
            {
                PauseAllAudioOfType((AudioType)type);
            }
            LogDebug("������Ƶ����ͣ");
        }

        private void ResumeAllInternal()
        {
            foreach (var type in System.Enum.GetValues(typeof(AudioType)))
            {
                ResumeAllAudioOfType((AudioType)type);
            }
            LogDebug("������Ƶ�ѻָ�");
        }

        private void PauseAllSFXInternal()
        {
            PauseAllAudioOfType(AudioType.SFX);
            LogDebug("������Ч����ͣ");
        }

        private void ResumeAllSFXInternal()
        {
            ResumeAllAudioOfType(AudioType.SFX);
            LogDebug("������Ч�ѻָ�");
        }

        // ״̬��ѯ�ڲ�ʵ��
        private bool IsMusicPlayingInternal()
        {
            return currentMusicSource != null && currentMusicSource.isPlaying;
        }

        private bool IsMusicPausedInternal()
        {
            return currentMusicSource != null && currentMusicSource.clip != null && !currentMusicSource.isPlaying;
        }

        private void SetMasterVolumeInternal(float volume)
        {
            audioConfig.masterVolume = Mathf.Clamp01(volume);
            UpdateAllSourcesVolume();
            PlayerPrefs.SetFloat("MasterVolume", volume);
        }

        private void SetMusicVolumeInternal(float volume)
        {
            audioConfig.musicVolume = Mathf.Clamp01(volume);
            UpdateAllSourcesVolume(AudioType.Music);
            PlayerPrefs.SetFloat("MusicVolume", volume);
        }

        private void SetSFXVolumeInternal(float volume)
        {
            audioConfig.sfxVolume = Mathf.Clamp01(volume);
            UpdateAllSourcesVolume(AudioType.SFX);
            PlayerPrefs.SetFloat("SFXVolume", volume);
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

        private void PauseAllAudioOfType(AudioType type)
        {
            var activeList = activeAudioSources[type];
            foreach (var source in activeList)
            {
                if (source.isPlaying)
                {
                    source.Pause();
                }
            }
        }

        private void ResumeAllAudioOfType(AudioType type)
        {
            var activeList = activeAudioSources[type];
            foreach (var source in activeList)
            {
                if (!source.isPlaying && source.clip != null)
                {
                    source.UnPause();
                }
            }
        }

        private void UpdateAllSourcesVolume(AudioType? specificType = null)
        {
            var typesToUpdate = specificType.HasValue ?
                new[] { specificType.Value } :
                new[] { AudioType.Music, AudioType.SFX, AudioType.Voice, AudioType.UI };

            foreach (var type in typesToUpdate)
            {
                float finalVolume = audioConfig.GetVolumeByType(type) * audioConfig.masterVolume;

                foreach (var source in activeAudioSources[type])
                {
                    if (source.clip != null)
                    {
                        var clipData = audioDatabase.Values.FirstOrDefault(c => c.clip == source.clip);
                        if (clipData != null)
                        {
                            source.volume = clipData.volume * finalVolume;
                        }
                    }
                }
            }
        }

        private void SyncVolumeFromPlayerPrefs()
        {
            audioConfig.masterVolume = PlayerPrefs.GetFloat("MasterVolume", 0.8f);
            audioConfig.musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
            audioConfig.sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.8f);

            UpdateAllSourcesVolume();
            LogDebug("��ͬ��PlayerPrefs�е���������");
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[AudioManager] {message}");
            }
        }
        #endregion

        #region �������߷���
        /// <summary>
        /// �ֶ������Ƶ��������
        /// </summary>
        public static void AddAudioClip(string id, AudioClip clip, AudioType type)
        {
            var clipData = new AudioClipData(id, clip, type);
            Instance.audioConfig.audioClips.Add(clipData);
            Instance.audioDatabase[id] = clipData;
            Instance.LogDebug($"�ֶ������Ƶ: {id}");
        }

        /// <summary>
        /// ��ȡ��ǰ���õ���Ƶ�б�
        /// </summary>
        public static List<string> GetAudioList()
        {
            return new List<string>(Instance.audioDatabase.Keys);
        }

        /// <summary>
        /// �����Ƶ�Ƿ����
        /// </summary>
        public static bool HasAudio(string audioId)
        {
            return Instance.audioDatabase.ContainsKey(audioId);
        }

        /// <summary>
        /// ��ȡ��ǰ���ֲ���״̬
        /// </summary>
        public static string GetMusicStatus()
        {
            if (Instance.currentMusicSource == null)
                return "������";

            if (Instance.currentMusicSource.isPlaying)
                return $"������: {Instance.currentMusicSource.clip.name}";

            if (Instance.currentMusicSource.clip != null)
                return $"����ͣ: {Instance.currentMusicSource.clip.name}";

            return "������";
        }

        /// <summary>
        /// ��ȡ��Ƶ����ͳ��
        /// </summary>
        public static AudioPlaybackStats GetPlaybackStats()
        {
            var stats = new AudioPlaybackStats();

            foreach (var kvp in Instance.activeAudioSources)
            {
                var type = kvp.Key;
                var sources = kvp.Value;

                int playingCount = sources.Count(s => s.isPlaying);
                int pausedCount = sources.Count(s => !s.isPlaying && s.clip != null);

                switch (type)
                {
                    case AudioType.Music:
                        stats.musicPlaying = playingCount;
                        stats.musicPaused = pausedCount;
                        break;
                    case AudioType.SFX:
                        stats.sfxPlaying = playingCount;
                        stats.sfxPaused = pausedCount;
                        break;
                    case AudioType.Voice:
                        stats.voicePlaying = playingCount;
                        stats.voicePaused = pausedCount;
                        break;
                    case AudioType.UI:
                        stats.uiPlaying = playingCount;
                        stats.uiPaused = pausedCount;
                        break;
                }
            }

            return stats;
        }
        #endregion

        /// <summary>
        /// ��Ƶ����ͳ������
        /// </summary>
        public struct AudioPlaybackStats
        {
            public int musicPlaying, musicPaused;
            public int sfxPlaying, sfxPaused;
            public int voicePlaying, voicePaused;
            public int uiPlaying, uiPaused;

            public int TotalPlaying => musicPlaying + sfxPlaying + voicePlaying + uiPlaying;
            public int TotalPaused => musicPaused + sfxPaused + voicePaused + uiPaused;
            public int TotalActive => TotalPlaying + TotalPaused;

            public override string ToString()
            {
                return $"������: {TotalPlaying}, ��ͣ��: {TotalPaused}, �ܼ�: {TotalActive}";
            }
        }
    }
}