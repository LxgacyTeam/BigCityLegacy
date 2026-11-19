using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

[HarmonyPatch]
internal static class ShadowFixPatches
{
    [HarmonyPatch(typeof(PlayerSaveLoad.CurSkin), "CreateViewMesh")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> CurSkin_CreateViewMesh_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return ShadowTranspiler(instructions);
    }

    [HarmonyPatch(typeof(Anim2Shader), "SetMotionBlurUsed")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Anim2Shader_SetMotionBlurUsed_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return ShadowTranspiler(instructions);
    }

    [HarmonyPatch(typeof(SkinnedMeshData), "CreateMeshRenderer")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> SkinnedMeshData_CreateMeshRenderer_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return ShadowTranspiler(instructions);
    }

    [HarmonyPatch(typeof(HairRenderer), "AddChild")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> HairRenderer_AddChild_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return ShadowTranspiler(instructions);
    }

    private static IEnumerable<CodeInstruction> ShadowTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeInstruction previous = null;
        MethodInfo getShadowCastingMode = AccessTools.Method(typeof(LegacyShadowFix), nameof(LegacyShadowFix.GetShadowCastingMode));

        foreach (CodeInstruction instr in instructions)
        {
            if (previous != null && previous.opcode == OpCodes.Ldc_I4_0)
            {
                if (instr.opcode == OpCodes.Callvirt || instr.opcode == OpCodes.Call)
                {
                    if (instr.operand?.ToString()?.Contains("set_shadowCastingMode") == true)
                    {
                        previous.opcode = OpCodes.Call;
                        previous.operand = getShadowCastingMode;
                    }
                }
            }

            if (previous != null)
            {
                yield return previous;
            }
            previous = instr;
        }

        if (previous != null)
        {
            yield return previous;
        }
    }
}