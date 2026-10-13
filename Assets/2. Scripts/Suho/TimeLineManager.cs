using Cinemachine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class TimeLineManager : SceneOnlySingleton<TimeLineManager>
{
    public PlayableDirector director;
    public SignalReceiver receiver;
    public bool isPlaying = false;
    public GameObject effectObject;
    private Animator effectAnimator;
    private IAttackable attacker;
    private List<PoolableAudioSource> AudioSourcePrefabs;
    public VirtualCameraController CurrentCameraController { get; set; }


    public event Action TimelineStarted;
    public event Action TimelineEnded;

    private Unit owner;

    protected override void Awake()
    {
        base.Awake();
        director = GetComponent<PlayableDirector>();
        effectAnimator = effectObject.GetComponent<Animator>();
        receiver = GetComponent<SignalReceiver>();
        director.stopped += StopTimeLine;
    }


    // private void Update()
    // {
    //     Debug.Log(effectObject.transform.position);
    // }

    public void StartVFXOnEffectObject()
    {
        foreach (SkillEffectData data in attacker.SkillController.CurrentSkillData.skillSo.effect.skillEffectDatas)
        {
            VFXController.VFXListPlayOnTransform(data.skillVFX, VFXType.Start, effectObject);
        }
    }

    public void OnAttackVFXOnEffectObject()
    {
        foreach (SkillEffectData data in attacker.SkillController.CurrentSkillData.skillSo.effect.skillEffectDatas)
        {
            VFXController.VFXListPlayOnTransform(data.skillVFX, VFXType.Hit, effectObject);
        }
    }

    public void OnAttackVFX()
    {
        CombatActionSo type = attacker.SkillController.CurrentSkillData.skillSo.skillType;
        type.PlayCastVFX(attacker, attacker.Target);
    }

    public void AffectSkillInTimeline()
    {
        attacker.SkillController.UseSkill();
    }

    public void ShakeCurrentCamera()
    {
        CurrentCameraController.ShakeCamera();
    }

    public void StopShakeCurrentCamera()
    {
        CurrentCameraController.StopShakeCamera();
    }

    public void InitializeTimeline()
    {
        Unit      attackerUnit  = attacker as Unit;
        Transform unitTransform = attackerUnit.transform;
        unitTransform.LookAt(attackerUnit.Target.Collider.bounds.center);
        unitTransform.eulerAngles = new Vector3(0, unitTransform.eulerAngles.y, 0);
        Vector3    pos = unitTransform.position;
        Quaternion rot = unitTransform.rotation;
        CurrentCameraController.Unfocus();
        CameraManager.Instance.followNextIEffectProvider = false;
        CurrentCameraController.transform.position = pos;
        CurrentCameraController.transform.rotation = rot;
        effectObject.transform.position = pos;
        effectObject.transform.rotation = rot;
    }

    public void PlayTimeLine(CinemachineBrain brain, VirtualCameraController vCamController, IAttackable user, out bool isTimeLine)
    {
        attacker = user;
        director.playableAsset = attacker.SkillController.CurrentSkillData?.skillSo.skillTimeLine;
        isPlaying = true;
        isTimeLine = director.playableAsset != null;
        if (!isTimeLine)
        {
            return;
        }

        TimelineStarted?.Invoke();

        CurrentCameraController = vCamController;
        CurrentCameraController.ChangeCamera();
        owner = attacker as Unit;
        TimelineAsset timelineAsset = director.playableAsset as TimelineAsset;
        if (attacker.SkillController.CurrentSkillData.skillSo.isSkillScene)
        {
            InitializeTimeline();
        }

        foreach (TrackAsset track in timelineAsset.GetOutputTracks())
        {
            if (track is CinemachineTrack cinemachineTrack)
            {
                IEnumerable<TimelineClip> clips = cinemachineTrack.GetClips();
                foreach (TimelineClip clip in clips)
                {
                    CinemachineShot shot = clip.asset as CinemachineShot;
                    if (shot == null)
                    {
                        continue;
                    }

                    if (shot.DisplayName == "SkillCamera")
                    {
                        director.SetReferenceValue(shot.VirtualCamera.exposedName, CameraManager.Instance.skillCameraController.vCam);
                    }
                    else if (shot.DisplayName == "MainCamera")
                    {
                        director.SetReferenceValue(shot.VirtualCamera.exposedName, CameraManager.Instance.mainCameraController.vCam);
                    }
                }
            }

            if (track is SignalTrack)
            {
                director.SetGenericBinding(track, receiver);
            }

            if (track is AudioTrack audioTrack)
            {
                PoolableAudioSource audioSourcePrefab = ObjectPoolManager.Instance.GetObject("sfxSource").GetComponent<PoolableAudioSource>();
                director.SetGenericBinding(audioTrack, audioSourcePrefab.AudioSource);
                if (AudioSourcePrefabs == null)
                {
                    AudioSourcePrefabs = new List<PoolableAudioSource>();
                }

                AudioSourcePrefabs.Add(audioSourcePrefab);
            }


            if (track is AnimationTrack animationTrack)
            {
                if (animationTrack.name == "AttackerTrack")
                {
                    director.SetGenericBinding(animationTrack, owner?.Animator);
                }
                else if (animationTrack.name == "EffectTrack")
                {
                    director.SetGenericBinding(animationTrack, effectAnimator);
                }
                else if (animationTrack.name == "CameraAnimationTrack")
                {
                    director.SetGenericBinding(animationTrack, CurrentCameraController.CameraAnimator);
                }
            }
        }

        // 뇌 (CinemachineBrain) 바인딩은 트랙 수와 관계없이 한 번만 필요
        PlayableBinding brainOutput = timelineAsset.outputs.FirstOrDefault(o => o.outputTargetType == typeof(CinemachineBrain));
        if (brainOutput.sourceObject != null)
        {
            director.SetGenericBinding(brainOutput.sourceObject, brain);
        }

        owner.IsTimeLinePlaying = true;
        director.time = 0f;
        director.Evaluate();
        director.Play();
    }

    public void StopTimeLine(PlayableDirector pd)
    {
        if (!isPlaying)
        {
            return;
        }


        isPlaying = false;
        director.Stop();

        if (owner != null)
        {
            owner.IsTimeLinePlaying = false;
        }

        TimelineEnded?.Invoke();
        owner = null;

        if (AudioSourcePrefabs != null)
        {
            foreach (PoolableAudioSource audioSourcePrefab in AudioSourcePrefabs)
            {
                audioSourcePrefab.Disable();
            }

            // 반환한 AudioSource를 비워두지 않으면 다음 타임라인 종료 시 같은 오브젝트를 풀에 또 반환하게 됨
            AudioSourcePrefabs.Clear();
        }

        CameraManager.Instance.skillCameraController.DefaultCamera();
        CurrentCameraController = null;
        director.playableAsset = null;
    }
}