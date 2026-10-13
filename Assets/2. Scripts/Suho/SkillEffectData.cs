using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SkillEffectData
{
    public SelectCampType selectCamp;
    public SelectTargetType selectTarget;
    public GameObject projectilePrefab;
    public string projectilePoolID;
    public List<StatBaseDamageEffect> damageEffects;
    public List<StatBaseBuffSkillEffect> buffEffects;
    public List<VFXData> skillVFX;
    
    public void AffectTargetWithSkill(IAttackable attacker, IDamageable target) // 실질적으로 영향을 끼치는 부분
    {
        if (target == null || target.IsDead) return;
        // SkillEffectData는 SO에 직렬화되어 같은 스킬을 쓰는 모든 유닛이 공유하므로, 시전자 정보는 필드가 아닌 지역변수로 다룸
        Unit owner = attacker as Unit;
        VFXController.VFXListPlay(skillVFX,VFXType.Hit,VFXSpawnReference.Target, target as IEffectProvider,true);
        VFXController.VFXListPlay(skillVFX,VFXType.Hit,VFXSpawnReference.Caster, owner as IEffectProvider,true);
        foreach (var result in buffEffects)
        {
            // 공유 데이터(result.StatusEffect)의 값을 덮어쓰지 않고, 이번 시전에서만 쓸 복사본에 시전자 스탯 기반 수치를 계산
            StatusEffectData statusEffect = CreateRuntimeStatusEffect(result.StatusEffect,
                owner.StatManager.GetValue(result.ownerStatType) * result.weight + result.value);
            target.StatusEffectManager.ApplyEffect(BuffFactory.CreateBuff(statusEffect));
            target.ChangeEmotion(result.emotionType);
        }


        foreach (var result in damageEffects)
        {
            target.ExecuteCoroutine(result.DamageEffectCoroutine(target, owner));
        }
    }

    private static StatusEffectData CreateRuntimeStatusEffect(StatusEffectData template, float value)
    {
        return new StatusEffectData
        {
            ID = template.ID,
            EffectType = template.EffectType,
            Stat = new StatData
            {
                StatType = template.Stat.StatType,
                ModifierType = template.Stat.ModifierType,
                Value = value
            },
            Duration = template.Duration,
            TickInterval = template.TickInterval,
            IsStackable = template.IsStackable,
            VFX = template.VFX
        };
    }

}




