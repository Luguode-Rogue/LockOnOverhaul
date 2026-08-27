using HarmonyLib;
using System;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;
using TaleWorlds.MountAndBlade.View.MissionViews;
using MathF = TaleWorlds.Library.MathF;
//new Vec3(-0.204f, 0.665f, -0.718f), // 默认给个初值，避免0向量
//            new Vec3(-0.864f, -0.5f, 0.056f),
//            new Vec3(0.289f, -0.093f, -0.952f)
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace LockOnOverhaul
{
    public static class MountedSettings
    {
        public static float[] Angles = { 30f * (float)Math.PI / 180f, 45f * (float)Math.PI / 180f, 60f * (float)Math.PI / 180f };
        public static int CurrentIndex = 1; // 默认 45 度
        public static float MouseAccumulator = 0f;
        public const float Threshold = 50f;
        public const float Cooldown = 0.3f;
        public static float LastSwitchTime = 0f;
        public static bool IsMountedAttacking = false;
    }

    // ============================================
    // 方案 1：使用 Prefix 劫持 LookTick（推荐）
    // ============================================
    [HarmonyPatch(typeof(MissionMainAgentController), "LookTick")]
    public static class LookTickOverridePatch
    {
        static bool Prefix(MissionMainAgentController __instance, float dt)
        {
            if (Agent.Main == null || !MountedSettings.IsMountedAttacking)
                return true; // 不是骑马攻击，走原逻辑

            // ✅ 核心修复：根据马的朝向修正攻击方向
            // 优先使用马的朝向（LookDirection），因为它更准确反映马头指向
            Vec3 horseLookDir = Agent.Main.MountAgent.LookDirection;

            // 提取水平面方向（忽略Z轴，因为我们后面会单独处理俯仰角）
            Vec3 horseForward = new Vec3(horseLookDir.x, horseLookDir.y, 0f).NormalizedCopy();

            // 处理极端情况：如果马的朝向在水平面投影为零（理论上不应该发生）
            if (horseForward.LengthSquared < 0.01f)
            {
                // 回退到移动方向
                horseForward = Agent.Main.MountAgent.GetMovementDirection().ToVec3();
                if (horseForward.LengthSquared < 0.01f)
                {
                    // 再回退到代理的朝向
                    horseForward = new Vec3(
                        Agent.Main.LookDirection.x,
                        Agent.Main.LookDirection.y,
                        0f
                    ).NormalizedCopy();
                }
            }

            // 计算俯仰角（向下看）
            float pitchAngle = MountedSettings.Angles[MountedSettings.CurrentIndex];

            // 构建最终的攻击方向向量
            // 基于马头朝向的水平方向，加上向下俯仰
            Vec3 targetLookDir = horseForward;
            targetLookDir.z = -(float)Math.Tan(pitchAngle); // 向下为负
            targetLookDir = targetLookDir.NormalizedCopy();

            // ✅ 直接设置，不需要 IsLookDirectionLocked
            Agent.Main.LookDirection = targetLookDir;
            Agent.Main.HeadCameraMode = __instance.Mission.CameraIsFirstPerson;

            // 调试信息（可选）
            // InformationManager.DisplayMessage(new InformationMessage(
            //     $"攻击方向: X={targetLookDir.x:F3}, Y={targetLookDir.y:F3}, Z={targetLookDir.z:F3}"));

            // ⚠️ 返回 false，跳过原版 LookTick 逻辑，避免被覆盖
            return false;
        }
    }

    // ============================================
    // 方案 2：Agent.Tick 检测攻击状态（辅助）
    // ============================================
    [HarmonyPatch(typeof(Agent), "Tick")]
    public static class MountedAttackDetectionPatch
    {
        static void Postfix(Agent __instance)
        {
            if (!__instance.IsMainAgent || __instance.MountAgent == null)
            {
                MountedSettings.IsMountedAttacking = false;
                return;
            }

            if (Mission.Current.CameraIsFirstPerson)
            {
                MountedSettings.IsMountedAttacking = false;
                return;
            }

            // 检测是否处于攻击状态
            var stage = __instance.GetCurrentActionStage(1);
            bool isAttacking = stage == Agent.ActionStage.AttackReady ||
                               stage == Agent.ActionStage.AttackRelease;

            if (isAttacking)
            {
                // 角度切换逻辑
                float mouseDeltaY = Input.GetMouseMoveY();
                MountedSettings.MouseAccumulator += mouseDeltaY * 60f; // 帧率归一化

                if (Mission.Current.CurrentTime - MountedSettings.LastSwitchTime > MountedSettings.Cooldown)
                {
                    if (Math.Abs(MountedSettings.MouseAccumulator) > MountedSettings.Threshold)
                    {
                        // 切换角度
                        if (MountedSettings.MouseAccumulator > 0)
                            MountedSettings.CurrentIndex = (MountedSettings.CurrentIndex + 1) % 3;
                        else
                            MountedSettings.CurrentIndex = (MountedSettings.CurrentIndex - 1 + 3) % 3;

                        MountedSettings.LastSwitchTime = Mission.Current.CurrentTime;
                        MountedSettings.MouseAccumulator = 0f;

                        // 调试信息
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"攻击角度切换: {(int)(MountedSettings.Angles[MountedSettings.CurrentIndex] * 180 / Math.PI)}°"));
                    }
                }

                MountedSettings.IsMountedAttacking = true;
            }
            else
            {
                MountedSettings.IsMountedAttacking = false;
                MountedSettings.MouseAccumulator = 0f;
            }
        }
    }
}