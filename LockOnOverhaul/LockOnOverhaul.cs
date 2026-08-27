using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace LockOnOverhaul
{
    [HarmonyPatch]
    public static class LockOnOverhaulPatch
    {
        // --- 目标 1: 将所有锁定参考点从胸部改为头部 ---
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(MissionMainAgentController), "FindTargetedLockableAgent")]
        //[HarmonyPatch(typeof(MissionGauntletAgentLockVisualizerView), "OnMissionScreenTick")] // 注意：若此 View 类名在旧版不同，请检查
        public static IEnumerable<CodeInstruction> ChestToEyeTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var target = AccessTools.Method(typeof(Agent), nameof(Agent.GetChestGlobalPosition));
            var replacement = AccessTools.Method(typeof(Agent), nameof(Agent.GetEyeGlobalPosition));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(target))
                {
                    codes[i] = new CodeInstruction(OpCodes.Callvirt, replacement);
                }
            }
            return codes;
        }

        //// --- 目标 2: 允许远程武器触发锁定 (解除 ControlTick 限制) ---
        //[HarmonyTranspiler]
        //[HarmonyPatch(typeof(MissionMainAgentController), "ControlTick")]
        //public static IEnumerable<CodeInstruction> UnlockRangedLockTranspiler(IEnumerable<CodeInstruction> instructions)
        //{
        //    var codes = new List<CodeInstruction>(instructions);
        //    var isRangedProperty = AccessTools.PropertyGetter(typeof(WeaponComponentData), nameof(WeaponComponentData.IsRangedWeapon));

        //    for (int i = 0; i < codes.Count; i++)
        //    {
        //        // 寻找所有判断 IsRangedWeapon 的地方，强制让结果为 false (ldc.i4.0)
        //        if (codes[i].Calls(isRangedProperty))
        //        {
        //            codes[i] = new CodeInstruction(OpCodes.Pop);
        //            codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldc_I4_0));
        //        }
        //    }
        //    return codes;
        //}

        // --- 目标 3: 远程弹道预测与注视点修正 ---
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MissionMainAgentController), "LookTick")]
        public static bool BallisticLookTickPrefix(MissionMainAgentController __instance)
        {
            // 只有在锁定目标且手持远程武器时才介入
            if (__instance.LockedAgent == null || Agent.Main == null) return true;

            var weapon = Agent.Main.WieldedWeapon;
            if (weapon.IsEmpty || !weapon.CurrentUsageItem.IsRangedWeapon) return true;

            // 1. 获取目标眼睛位置和我的眼睛位置
            Vec3 targetPos = __instance.LockedAgent.GetEyeGlobalPosition();
            Vec3 myPos = Agent.Main.GetEyeGlobalPosition();

            float distance = targetPos.Distance(myPos);
            float missileSpeed = weapon.CurrentUsageItem.MissileSpeed;

            // 2. 弹道下坠补偿计算 (h = 0.5 * g * t^2)
            if (missileSpeed > 0)
            {
                float gravity = 9.8f;
                float timeToTarget = distance / missileSpeed;
                float dropAdjustment = 0.5f * gravity * (timeToTarget * timeToTarget);

                targetPos.z += dropAdjustment;
            }

            // 3. 应用注视方向
            Vec3 finalLookDir = (targetPos - myPos).NormalizedCopy();
            Agent.Main.LookDirection = finalLookDir;

            return false; // 拦截原逻辑，防止原版 num 计算干扰
        }
    }
}
