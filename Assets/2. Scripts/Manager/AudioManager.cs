using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using System;
using UnityEngine.SceneManagement;

public enum AudioType
{
    BGM,
    SFX,
    Master
}

public class AudioManager : Singleton<AudioManager>
{
    /* 사운드 조절 기능 */
    [SerializeField] [Range(0, 1)] private float masterVolume = 1f;
    [SerializeField] [Range(0, 1)] private float soundEffectVolume = 1f;
    [SerializeField] [Range(0, 1)] private float soundEffectPitchVariance = 0.1f;
    [SerializeField] [Range(0, 1)] private float musicVolume = 0.5f;

    /* 모든 사운드 저장 */
    /* 저장된 사운드를 꺼내쓰기 쉽도록 Dictionary에 저장 */
    public Dictionary<string, AudioClip> AudioDictionary = new();
    public Dictionary<string, int> NestedDictionary = new();
    protected ObjectPoolManager objectPoolManager;
    private string sfxPlayerPoolName = "sfxSource";
    private const int MaxSameSfxCount = 3;

    [SerializeField] private AudioSource bgmAudioSource;
    [SerializeField] private GameObject sfxAudioSourcePrefab;
    

    protected override void Awake()
    {
        base.Awake();
        if (!isDuplicated)
        {
            InitializeAudioManager();
        }
    }

    private void Start()
    {
    }

    private void InitializeAudioManager()
    {
        bgmAudioSource = GetComponent<AudioSource>();
        if (bgmAudioSource == null)
        {
            bgmAudioSource = gameObject.AddComponent<AudioSource>();
        }

        LoadSceneEvent();
        LoadSceneManager.Instance.OnLoadingCompleted += LoadSceneEvent;

        bgmAudioSource.volume = musicVolume;
        bgmAudioSource.loop = true;
    }


    /* 볼륨 조절 기능. 나중에 옵션으로 사운드를 BGM,SFX 따로 조절할 수 있도록 만든 형태 */
    public void SetVolume(AudioType type, float volume)
    {
        volume = Mathf.Clamp01(volume);

        switch (type)
        {
            case AudioType.BGM:
                musicVolume = volume;
                if (bgmAudioSource != null)
                {
                    bgmAudioSource.volume = musicVolume * masterVolume;
                }
                break;
            case AudioType.SFX:
                soundEffectVolume = volume; break;
            case AudioType.Master:
                masterVolume = volume;
                if (bgmAudioSource != null)
                {
                    bgmAudioSource.volume = musicVolume * masterVolume;
                }
                break;
        }
    }

    public float GetVolume(AudioType type)
    {
        return type switch
        {
            AudioType.BGM    => musicVolume,
            AudioType.SFX    => soundEffectVolume,
            AudioType.Master => masterVolume,
            _                => 1f
        };
    }

    /* BGM은 Loop를 돌며 계속해서 반복 재생 */
    public void PlayBGM(string clipName, bool isLoop = true)
    {
        if (bgmAudioSource == null || AudioDictionary == null || clipName == "None")
        {
            Debug.LogWarning("SoundManager: BGM 재생 실패 - AudioSource 또는 AudioDictionary가 null입니다.");
            return;
        }

        if (AudioDictionary.ContainsKey(clipName))
        {
            AudioClip newClip = AudioDictionary[clipName];

            if (bgmAudioSource.isPlaying)
            {
                // 현재 재생 중인 BGM 페이드아웃
                bgmAudioSource.DOFade(0f, 1f).OnComplete(() =>
                {
                    bgmAudioSource.Stop();
                    bgmAudioSource.clip = newClip;
                    bgmAudioSource.loop = isLoop;
                    bgmAudioSource.Play();
                    bgmAudioSource.DOFade(musicVolume * masterVolume, 1f);
                });
            }
            else
            {
                // BGM이 재생 중이지 않을 때 바로 페이드인 재생
                bgmAudioSource.clip = newClip;
                bgmAudioSource.loop = isLoop;
                bgmAudioSource.volume = 0f;
                bgmAudioSource.Play();
                bgmAudioSource.DOFade(musicVolume * masterVolume, 1f);
            }
        }
        else
        {
            Debug.LogWarning($"SoundManager: PlayBGM - {clipName}은 존재하지 않는 오디오 클립입니다.");
        }
    }

    /* BGM을 정지할 때 쓰는 메서드 */
    public void StopBGM()
    {
        if (bgmAudioSource != null)
        {
            bgmAudioSource.Stop();
        }
    }

    /* 효과음 재생용 메서드 */
    public void PlaySFX(string clipName)
    {
        PlaySfxInternal(clipName);
    }

    /* 효과음 재생 후 해당 효과음 제어를 위해 만들어진 효과음 Prefab을 Return받는데 사용되는 메서드 */
    public PoolableAudioSource PlaySfxReturnSoundSource(string clipName)
    {
        return PlaySfxInternal(clipName);
    }

    /* 같은 키값의 SFX는 동시에 MaxSameSfxCount개까지만 재생 (중복 재생으로 인한 사운드 증폭 방지) */
    private PoolableAudioSource PlaySfxInternal(string clipName)
    {
        if (string.IsNullOrEmpty(clipName) || clipName == "None")
        {
            return null;
        }

        if (objectPoolManager == null)
        {
            Debug.LogWarning("SoundManager: ObjectPoolManager를 찾을 수 없습니다.");
            return null;
        }

        if (AudioDictionary == null || !AudioDictionary.TryGetValue(clipName, out AudioClip clip))
        {
            Debug.LogWarning($"SoundManager: PlaySFX - {clipName}은 존재하지 않는 오디오 클립입니다.");
            return null;
        }

        NestedDictionary.TryGetValue(clipName, out int playingCount);
        if (playingCount >= MaxSameSfxCount)
        {
            return null;
        }

        GameObject sfxPlayer = objectPoolManager.GetObject(sfxPlayerPoolName);
        if (sfxPlayer == null)
        {
            sfxPlayer = Instantiate(sfxAudioSourcePrefab);
        }

        PoolableAudioSource sfxSource = sfxPlayer.GetComponent<PoolableAudioSource>();
        if (sfxSource == null)
        {
            Debug.LogWarning("SoundManager: SoundSource 객체를 가져올 수 없습니다.");
            return null;
        }

        NestedDictionary[clipName] = playingCount + 1;
        sfxSource.Play(clip, soundEffectVolume * masterVolume, clipName);
        return sfxSource;
    }

    /* PoolableAudioSource가 풀로 돌아갈 때 호출. 재생할 때 사용한 키값으로 카운트를 감소 */
    public void OnSfxReleased(string clipName)
    {
        if (string.IsNullOrEmpty(clipName) || !NestedDictionary.TryGetValue(clipName, out int playingCount))
        {
            return;
        }

        NestedDictionary[clipName] = Mathf.Max(playingCount - 1, 0);
    }

    private void LoadSceneEvent()
    {
        objectPoolManager = ObjectPoolManager.Instance;
        //LoadAssetManager.Instance.OnLoadAssetsChangeScene(SceneManager.GetActiveScene().name);
        LoadAssetManager.Instance.LoadAudioClipAsync(SceneManager.GetActiveScene().name + "BGM",
            clip =>
            {
                PlayBGM(clip);
            });
        LoadAssetManager.Instance.LoadAssetBundle(nameof(AlwaysLoad.AlwaysLoadSound));            // 항상 로드해와야 하는 사운드
        LoadAssetManager.Instance.LoadAssetBundle(SceneManager.GetActiveScene().name + "Assets"); // 특정 씬에서 로드해와야 하는 사운드
        NestedDictionary.Clear();
    }
}