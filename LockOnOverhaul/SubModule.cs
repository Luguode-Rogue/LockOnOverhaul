using HarmonyLib;
using TaleWorlds.MountAndBlade;


namespace LockOnOverhaul
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();  

        }
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            // 初始化 Harmony，识别该项目下所有的 [HarmonyPatch] 标签
            var harmony = new Harmony("com.combat.overhaul.mod");
            harmony.PatchAll();
        }
    }
}