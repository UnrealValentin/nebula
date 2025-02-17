﻿using HarmonyLib;
using NebulaWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace NebulaPatcher.Patches.Transpiler
{
    [HarmonyPatch(typeof(UIStatisticsWindow))]
    public static class UIStatisticsWindow_Transpiler
    {
        [HarmonyTranspiler]
        [HarmonyPatch(nameof(UIStatisticsWindow.RefreshAstroBox))]
        private static IEnumerable<CodeInstruction> RefreshAstroBox_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            //Change: this.gameData.factoryCount 
            //To:     GetFactoryCount()
            //Change: this.gameData.factories[i].planetId
            //To:     GetPlanetData(i).id
            //Change: this.gameData.factories[i].planet
            //To:     GetPlanetData(i)
            try
            {
                instructions = ReplaceFactoryCount(instructions);

                CodeMatcher matcher = new CodeMatcher(instructions)
                    .MatchForward(false,
                        new CodeMatch(OpCodes.Callvirt, AccessTools.DeclaredPropertyGetter(typeof(PlanetFactory), nameof(PlanetFactory.planetId)))
                    )
                    .Advance(-5);
                OpCode factoryIndexOp = matcher.InstructionAt(3).opcode;
                matcher.SetAndAdvance(factoryIndexOp, null)
                    .InsertAndAdvance(
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UIStatisticsWindow_Transpiler), nameof(UIStatisticsWindow_Transpiler.GetPlanetData))),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlanetData), nameof(PlanetData.id)))
                    )
                    .RemoveInstructions(5)
                    .MatchForward(false,
                        new CodeMatch(OpCodes.Callvirt, AccessTools.DeclaredPropertyGetter(typeof(PlanetFactory), nameof(PlanetFactory.planet)))
                    )
                    .Advance(-5);
                factoryIndexOp = matcher.InstructionAt(3).opcode;
                matcher.SetAndAdvance(factoryIndexOp, null)
                    .InsertAndAdvance(
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UIStatisticsWindow_Transpiler), nameof(UIStatisticsWindow_Transpiler.GetPlanetData)))
                    )
                    .RemoveInstructions(5);
                return matcher.InstructionEnumeration();
            }
            catch
            {
                NebulaModel.Logger.Log.Error("RefreshAstroBox_Transpiler failed. Mod version not compatible with game version.");
                return instructions;
            }
        }

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(UIStatisticsWindow.ComputeDisplayEntries))]
        private static IEnumerable<CodeInstruction> ComputeDisplayEntries_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            //Change: this.gameData.factoryCount
            //To:     GetFactoryCount()
            //Change: planetData.factoryIndex
            //To:     GetFactoryIndex(planetData)
            //Change: if (starData.planets[j].factory != null)
            //To:     if (GetFactoryIndex(starData.planets[j]) != -1)
            try
            {
                instructions = ReplaceFactoryCount(instructions);
                instructions = new CodeMatcher(instructions)
                    .MatchForward(false,
                        new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(PlanetData), nameof(PlanetData.factoryIndex)))
                    )
                    .Repeat(matcher => matcher
                        .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(UIStatisticsWindow_Transpiler), nameof(UIStatisticsWindow_Transpiler.GetFactoryIndex)))
                    )
                    .InstructionEnumeration();

                return new CodeMatcher(instructions)
                    .MatchForward(false,
                        new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(PlanetData), nameof(PlanetData.factory))),
                        new CodeMatch(OpCodes.Brfalse)
                    )
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(UIStatisticsWindow_Transpiler), nameof(UIStatisticsWindow_Transpiler.GetFactoryIndex)))
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4_M1))
                    .SetOpcodeAndAdvance(OpCodes.Beq_S)
                    .InstructionEnumeration();
            }
            catch
            {
                NebulaModel.Logger.Log.Error("ComputeDisplayEntries_Transpiler failed. Mod version not compatible with game version.");
                return instructions;
            }
        }

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(UIStatisticsWindow.ComputePowerTab))]
        private static IEnumerable<CodeInstruction> ComputePowerTab_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iLGenerator)
        {
            /* This is fix for the power statistics.
               Originally, this function is iterating through all factories and manually summing up "energyStored" values from their PowerSystems.
               Since client does not have all factories loaded it would cause exceptions.
             * This fix is basically replacing this:
             
                PowerSystem powerSystem = this.gameData.factories[i].powerSystem;
				int netCursor = powerSystem.netCursor;
				PowerNetwork[] netPool = powerSystem.netPool;
				for (int j = 1; j < netCursor; j++)
				{
					num2 += netPool[j].energyStored;
				}

                With: Multiplayer.Session.Statistics.UpdateTotalChargedEnergy(factoryIndex);
                   
             * In the UpdateTotalChargedEnergy(), the total energyStored value is being calculated no clients based on the data received from the server. */
            CodeMatcher matcher = new CodeMatcher(instructions, iLGenerator)
                .MatchForward(false,
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(i => i.opcode == OpCodes.Ldfld && ((FieldInfo)i.operand).Name == "gameData"),
                    new CodeMatch(i => i.opcode == OpCodes.Ldfld && ((FieldInfo)i.operand).Name == "factories"),
                    new CodeMatch(i => i.IsLdarg()),
                    new CodeMatch(OpCodes.Conv_Ovf_I),
                    new CodeMatch(OpCodes.Ldelem_Ref),
                    new CodeMatch(i => i.opcode == OpCodes.Ldfld && ((FieldInfo)i.operand).Name == "powerSystem"),
                    new CodeMatch(OpCodes.Dup),
                    new CodeMatch(i => i.opcode == OpCodes.Ldfld && ((FieldInfo)i.operand).Name == "netCursor"),
                    new CodeMatch(i => i.IsStloc()),
                    new CodeMatch(i => i.opcode == OpCodes.Ldfld && ((FieldInfo)i.operand).Name == "netPool"),
                    new CodeMatch(i => i.IsStloc()),
                    new CodeMatch(OpCodes.Ldc_I4_1),
                    new CodeMatch(i => i.IsStloc()),
                    new CodeMatch(OpCodes.Br)
                );

            if (matcher.IsInvalid)
            {
                NebulaModel.Logger.Log.Error("UIStatisticsWindow_Transpiler.ComputePowerTab_Transpiler failed. Mod version not compatible with game version.");
                return instructions;
            }

            int currentPos = matcher.Pos;

            CodeInstruction storeNum2Instruction = matcher.InstructionAt(-1);
            CodeInstruction loadFactoryIndexInstruction = matcher.InstructionAt(3);

            return matcher.MatchForward(true,
                           new CodeMatch(OpCodes.Blt)
                           )
                           .Advance(1)
                           .CreateLabel(out Label endLabel)
                           .Start()
                           .Advance(currentPos)
                           .InsertAndAdvance(loadFactoryIndexInstruction)
                           .InsertAndAdvance(HarmonyLib.Transpilers.EmitDelegate<Func<long, long>>((factoryIndex) =>
                           {
                               if (!Multiplayer.IsActive || Multiplayer.Session.LocalPlayer.IsHost)
                               {
                                   return 0L;
                               }

                               return Multiplayer.Session.Statistics.UpdateTotalChargedEnergy((int)factoryIndex);
                           }))
                           .InsertAndAdvance(storeNum2Instruction)
                           .InsertAndAdvance(HarmonyLib.Transpilers.EmitDelegate<Func<bool>>(() =>
                           {
                               return Multiplayer.IsActive && !Multiplayer.Session.LocalPlayer.IsHost;
                           }))
                           .InsertAndAdvance(new CodeInstruction(OpCodes.Brtrue, endLabel))
                           .InstructionEnumeration();

        }

        private static IEnumerable<CodeInstruction> ReplaceFactoryCount(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions)
                .MatchForward(false,
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(UIStatisticsWindow), nameof(UIStatisticsWindow.gameData))),
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(GameData), nameof(GameData.factoryCount)))
                )
                .Repeat(matcher => matcher
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(UIStatisticsWindow_Transpiler), nameof(UIStatisticsWindow_Transpiler.GetFactoryCount)))
                    .RemoveInstructions(2)
                )
               .InstructionEnumeration();
        }

        private static int GetFactoryCount()
        {
            if (!Multiplayer.IsActive || Multiplayer.Session.LocalPlayer.IsHost)
            {
                return GameMain.data.factoryCount;
            }
            return Multiplayer.Session.Statistics.FactoryCount;
        }

        private static PlanetData GetPlanetData(int factoryId)
        {
            if (!Multiplayer.IsActive || Multiplayer.Session.LocalPlayer.IsHost)
            {
                return GameMain.data.factories[factoryId].planet;
            }
            return Multiplayer.Session.Statistics.GetPlanetData(factoryId);
        }

        private static int GetFactoryIndex(PlanetData planet)
        {
            if (!Multiplayer.IsActive || Multiplayer.Session.LocalPlayer.IsHost)
            {
                return planet.factoryIndex;
            }
            return Multiplayer.Session.Statistics.GetFactoryIndex(planet);
        }
    }
}
