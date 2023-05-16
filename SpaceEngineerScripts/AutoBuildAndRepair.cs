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

        private const UpdateType UpdateFlags = UpdateType.Terminal | UpdateType.Trigger | UpdateType.Script;
        
        private IMyTextSurface PanelSurface;
        private Vector2 SurfaceSize;
        private readonly Color[] ColorOptions = new Color[] { Color.White, new Color(60, 60, 60) };
        private readonly List<IMyShipWelder> RepairSystems = new List<IMyShipWelder>();
        private readonly List<IMyAssembler> Assemblers = new List<IMyAssembler>();
        private List<long> AssemblerIds;
        private readonly Dictionary<MyDefinitionId, int> Missing = new Dictionary<MyDefinitionId, int>();
        private Func<IEnumerable<long>, MyDefinitionId, int, int> EnsureQueued = null;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            Setup();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateFlags) != 0)
            {
                Setup();
            }

            if (EnsureQueued == null)
            {
                try
                {
                    EnsureQueued = RepairSystems[0]
                        .GetValue<Func<IEnumerable<long>, VRage.Game.MyDefinitionId, int, int>>(
                            "BuildAndRepair.ProductionBlock.EnsureQueued");
                }
                catch
                {
                    Echo("EnsureQueue Failed");
                }
            }
            
            var frame = PanelSurface.DrawFrame();

            MissingComponents();
            if (!Missing.Any())
            {
                frame.Add(MySprite.CreateText("All Good!", "Debug", Color.Green, 3f));
                frame.Dispose();
                return;
            }

            var pos = new Vector2(SurfaceSize.X / 2f, 0);
            var sprite1 = MakeText("Resources Needed", pos, color: Color.Red, scale: 2);
            frame.Add(sprite1);

            pos.Y += 15;
            var sprite2 = MakeText("_______________________________", pos, color: Color.Cyan, scale: 2);
            frame.Add(sprite2);

            pos.Y += 60;
            pos.X = 0;

            var numberPos = pos;
            numberPos.X = SurfaceSize.X;

            var count = 0;
            foreach (var material in Missing.Keys)
            {
                var color = ColorOptions[count++];
                count %= ColorOptions.Length;

                var materialName = MakeText(material.SubtypeName, pos, align: TextAlignment.LEFT, color: color,
                    scale: 1.3f);
                var materialAmount = MakeText($"{Missing[material]}", numberPos, align: TextAlignment.RIGHT,
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
            Missing.Clear();

            foreach (var builder in RepairSystems)
            {
                var dict =
                    builder.GetValue<Dictionary<MyDefinitionId, int>>("BuildAndRepair.MissingComponents");
                if (dict == null || !dict.Any()) continue;

                int value;
                foreach (var newItem in dict)
                {
                    if (Missing.TryGetValue(newItem.Key, out value))
                    {
                        if (newItem.Value > value) Missing[newItem.Key] = newItem.Value;
                    }
                    else
                    {
                        Missing.Add(newItem.Key, newItem.Value);
                    }
                }
            }
        }

        public void CheckAssemblerQueues()
        {
            if (EnsureQueued == null) return;
            if (AssemblerIds.Count <= 0) return;
            foreach (var item in Missing)
            {
                EnsureQueued(AssemblerIds, item.Key, item.Value);
            }
        }

        public void Setup()
        {
            PanelSurface = (IMyTextSurface) GridTerminalSystem.GetBlockWithName("LCD ResourceAlert");
            PanelSurface.ContentType = ContentType.SCRIPT;
            PanelSurface.ScriptBackgroundColor = Color.Black;

            SurfaceSize = PanelSurface.SurfaceSize;

            GridTerminalSystem.GetBlockGroupWithName("Construction").GetBlocksOfType(RepairSystems);
            GridTerminalSystem.GetBlockGroupWithName("Crafting Slaves").GetBlocksOfType(Assemblers);

            AssemblerIds = Assemblers.Select(asm => asm.EntityId).ToList();
        }

        #region PreludeFooter

    }
}

#endregion