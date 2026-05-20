using System;
using NightHunt.Gameplay.Character.Combat;

namespace NightHunt.Gameplay.Feedback
{
    public enum CombatHitFeedbackTargetKind : byte
    {
        None = 0,
        Player = 1,
        Deployable = 2,
        Boss = 3,
        Objective = 4,
        GenericHittable = 5
    }

    public readonly struct CombatHitFeedbackInfo
    {
        public CombatHitFeedbackInfo(DamageInfo damageInfo, CombatHitFeedbackTargetKind targetKind)
        {
            DamageInfo = damageInfo;
            TargetKind = targetKind;
        }

        public DamageInfo DamageInfo { get; }
        public CombatHitFeedbackTargetKind TargetKind { get; }
        public bool IsHeadshot => DamageInfo.IsHeadshot;
    }

    public static class CombatFeedbackEvents
    {
        public static event Action<CombatHitFeedbackInfo> LocalHitConfirmed;

        public static void PublishLocalHitConfirmed(
            DamageInfo damageInfo,
            CombatHitFeedbackTargetKind targetKind)
        {
            if (targetKind == CombatHitFeedbackTargetKind.None)
                return;

            LocalHitConfirmed?.Invoke(new CombatHitFeedbackInfo(damageInfo, targetKind));
        }
    }
}
