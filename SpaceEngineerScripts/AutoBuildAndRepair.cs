#region Prelude

using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using VRageMath;
using VRage.Game;
using VRage.Collections;
using Sandbox.ModAPI.Ingame;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game.EntityComponents;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ObjectBuilders.Definitions;

// Change this namespace for each script you create.
namespace SpaceEngineers.AutoBuildAndRepair
{
    public sealed class Program : MyGridProgram
    {
        // Your code goes between the next #endregion and #region

        #endregion

        IMyTextSurface panelSurface;
        Vector2 surfaceSize;
        Color[] colorOptions = new Color[] { Color.White, new Color(60, 60, 60) };
        List<IMyShipWelder> repairSystems = new List<IMyShipWelder>();
        List<IMyAssembler> assemblers = new List<IMyAssembler>();
        List<long> assemblerIds;
        Dictionary<MyDefinitionId, int> missing = new Dictionary<MyDefinitionId, int>();
        Func<IEnumerable<long>, MyDefinitionId, int, int> ensureQueued = null;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            Setup();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & (UpdateType.Terminal | UpdateType.Trigger | UpdateType.Script)) != 0)
            {
                Setup();
            }

            if (ensureQueued == null)
            {
                try
                {
                    ensureQueued = repairSystems[0]
                        .GetValue<Func<IEnumerable<long>, VRage.Game.MyDefinitionId, int, int>>(
                            "BuildAndRepair.ProductionBlock.EnsureQueued");
                }
                catch
                {
                    Echo("EnsureQueue Failed");
                }
            }
            
            var frame = panelSurface.DrawFrame();

            MissingComponents();
            if (!missing.Any())
            {
                frame.Add(MySprite.CreateText("All Good!", "Debug", Color.Green, 3f));
                frame.Dispose();
                return;
            }

            var pos = new Vector2(surfaceSize.X / 2f, 0);
            var sprite1 = MakeText("Resources Needed", pos, color: Color.Red, scale: 2);
            frame.Add(sprite1);

            pos.Y += 15;
            var sprite2 = MakeText("_______________________________", pos, color: Color.Cyan, scale: 2);
            frame.Add(sprite2);

            pos.Y += 60;
            pos.X = 0;

            var numberPos = pos;
            numberPos.X = surfaceSize.X;

            var count = 0;
            foreach (var material in missing.Keys)
            {
                var color = colorOptions[count++];
                count %= colorOptions.Length;

                var materialName = MakeText(material.SubtypeName, pos, align: TextAlignment.LEFT, color: color,
                    scale: 1.3f);
                var materialAmount = MakeText($"{missing[material]}", numberPos, align: TextAlignment.RIGHT,
                    color: color, scale: 1.3f);

                frame.Add(materialName);
                frame.Add(materialAmount);

                pos.Y += 30;
                numberPos.Y = pos.Y;
            }

            frame.Dispose();
            CheckAssemblerQueues();
        }

        public MySprite MakeText(string text, Vector2 pos, TextAlignment align = TextAlignment.CENTER,
            Color? color = null, float scale = 1f,
            string fontId = null, Vector2? size = null)
        {
            return new MySprite(SpriteType.TEXT, text, pos, size ?? Vector2.Zero, color ?? Color.White, fontId, align,
                scale);
        }

        public void MissingComponents()
        {
            missing.Clear();

            foreach (var builder in repairSystems)
            {
                var dict =
                    builder.GetValue<Dictionary<MyDefinitionId, int>>("BuildAndRepair.MissingComponents");
                if (dict == null || !dict.Any()) continue;

                int value;
                foreach (var newItem in dict)
                {
                    if (missing.TryGetValue(newItem.Key, out value))
                    {
                        if (newItem.Value > value) missing[newItem.Key] = newItem.Value;
                    }
                    else
                    {
                        missing.Add(newItem.Key, newItem.Value);
                    }
                }
            }
        }

        public void CheckAssemblerQueues()
        {
            if (ensureQueued == null) return;
            if (assemblerIds.Count <= 0) return;
            foreach (var item in missing)
            {
                ensureQueued(assemblerIds, item.Key, item.Value);
            }
        }

        public void Setup()
        {
            panelSurface = (IMyTextSurface) GridTerminalSystem.GetBlockWithName("LCD ResourceAlert");
            panelSurface.ContentType = ContentType.SCRIPT;
            panelSurface.ScriptBackgroundColor = Color.Black;

            surfaceSize = panelSurface.SurfaceSize;

            GridTerminalSystem.GetBlockGroupWithName("Construction").GetBlocksOfType(repairSystems);
            GridTerminalSystem.GetBlockGroupWithName("Crafting Slaves").GetBlocksOfType(assemblers);

            assemblerIds = assemblers.Select(asm => asm.EntityId).ToList();
        }

        #region PreludeFooter

    }
}

#endregion