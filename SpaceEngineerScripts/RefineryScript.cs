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
namespace SpaceEngineers.RefineryScript
{
    public sealed class Program : MyGridProgram
    {
        // Your code goes between the next #endregion and #region

        #endregion

        private const UpdateType UpdateFlags = UpdateType.Terminal | UpdateType.Trigger | UpdateType.Script;

        private readonly string[] Ids =
        {
            "StoneOreToIngot", "IronOreToIngot", "NickelOreToIngot", "SiliconOreToIngot", "MagnesiumOreToIngot",
            "CobaltOreToIngot", "SilverOreToIngot", "GoldOreToIngot", "PlatinumOreToIngot", "UraniumOreToIngot"
        };

        private readonly string[] Plane =
        {
            "Stone", "Iron", "Nickel", "Silicon", "Magnesium", "Cobalt", "Silver", "Gold", "Platinum", "Uranium"
        };

        private readonly MyItemType[] ItemTypes;
        private MyDefinitionId[] Definitions;
        private MonitorSetup RefineryDisplay;
        private readonly List<IMyRefinery> Refineries = new List<IMyRefinery>();
        private readonly List<IMyTerminalBlock> Containers = new List<IMyTerminalBlock>();
        private readonly List<MyProductionItem> RefineryQueue = new List<MyProductionItem>();
        private int RefineryPage;
        private int ResourceNumber;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            Definitions = Ids.Select(id => MyDefinitionId.Parse($"MyObjectBuilder_BlueprintDefinition/{id}")).ToArray();
            ItemTypes = Plane.Select(plane => MyItemType.MakeOre(plane)).ToArray();
            Setup();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            try
            {
                if (argument == "down" || argument == "up" || argument == "toggle" || argument == "page")
                {
                    switch (argument)
                    {
                        case "down":
                            ResourceNumber++;
                            ResourceNumber %= Ids.Length;
                            break;
                        case "up":
                            ResourceNumber--;
                            if (ResourceNumber < 0)
                            {
                                ResourceNumber = Ids.Length - 1;
                            }

                            break;
                        case "toggle":
                            var refinery = Refineries[RefineryPage];
                            var item = ItemTypes[ResourceNumber];
                            var refineryInv = refinery.GetInventory();

                            foreach (var inventory in Containers.Select(container => container.GetInventory())
                                         .Where(inv => !inv.IsFull))
                            {
                                for (var slot = 0; slot < refineryInv.ItemCount; slot++)
                                {
                                    var itm = refineryInv.GetItemAt(slot);
                                    if (itm == null) continue;
                                    if (!refineryInv.CanTransferItemTo(inventory, itm.Value.Type)) continue;

                                    refineryInv.TransferItemTo(inventory, slot);
                                }

                                if (refineryInv.ItemCount == 0) break;
                            }

                            refinery.ClearQueue();
                            refinery.GetQueue(RefineryQueue);

                            var amount = 0f;
                            foreach (var container in Containers)
                            {
                                var inv = container.GetInventory();
                                var invItem = inv.FindItem(item);

                                if (invItem == null) continue;

                                var localAmount = inv.GetItemAmount(item);
                                inv.TransferItemTo(refineryInv, invItem.Value, localAmount);
                                amount += (float) localAmount;
                            }

                            Echo($"setting {Plane[ResourceNumber]} to prio.1 with amount of {amount}");

                            break;
                        case "page":
                            RefineryPage++;
                            RefineryPage %= Refineries.Count;
                            break;
                    }
                }
            }
            catch
            {
                RefineryPage = 0;
                ResourceNumber = 0;
            }

            if ((updateSource & UpdateFlags) != 0)
            {
                Setup();
            }

            try
            {
                UpdateRefineryDisplay();
            }
            catch
            {
                Setup();
            }
        }

        public void UpdateRefineryDisplay()
        {
            var refinery = Refineries[RefineryPage];
            refinery.GetQueue(RefineryQueue);

            var frame = RefineryDisplay.Surface.DrawFrame();
            var surfaceSize = RefineryDisplay.ScreenSize;
            var pos = Vector2.Zero;

            var sprite1 = MakeText($"{RefineryPage + 1}/{Refineries.Count}|{refinery.CustomName}", pos, scale: 2,
                align: TextAlignment.LEFT);
            frame.Add(sprite1);

            pos.X = surfaceSize.X / 2f;
            pos.Y += 10;
            var sprite2 = MakeText("_______________________________", pos, color: Color.Cyan, scale: 2);
            frame.Add(sprite2);

            pos.Y += 60;
            pos.X = 0;
            var count = 0;

            foreach (var resource in Ids)
            {
                var index = 99;

                if (RefineryQueue.Any(item => item.BlueprintId.SubtypeName == resource))
                {
                    index = RefineryQueue.FindIndex(item => item.BlueprintId.SubtypeName == resource) + 1;
                }

                Color color;
                if (Containers.All(container => container.GetInventory().FindItem(ItemTypes[count]) == null) &&
                    index == 99)
                {
                    color = count == ResourceNumber ? Color.MediumVioletRed : Color.Red;
                }
                else if (index != 99)
                {
                    color = count == ResourceNumber ? Color.GreenYellow : Color.Green;
                }
                else
                {
                    color = count == ResourceNumber ? Color.Blue : Color.White;
                }

                var materialName = MakeText($"{index:00}   {Plane[count]}", pos, TextAlignment.LEFT, color,
                    scale: 1.3f);

                frame.Add(materialName);

                pos.Y += 30;
                count++;
            }

            frame.Dispose();
        }

        public MySprite MakeText(string text, Vector2 pos, TextAlignment align = TextAlignment.CENTER,
            Color? color = null, float scale = 1f, string fontId = null, Vector2? size = null)
        {
            return new MySprite(SpriteType.TEXT, text, pos, size ?? Vector2.Zero, color ?? Color.White, fontId, align,
                scale);
        }

        public void Setup()
        {
            GridTerminalSystem.GetBlocksOfType(Refineries);
            //refineries.RemoveAll(refinery => refinery.CubeGrid.Name != Me.CubeGrid.Name);

            GridTerminalSystem.GetBlocksOfType(Containers, SearchForCargo);
            Containers.RemoveAll(container
                => container.CustomName != "Main Cargo Container" || container.CubeGrid.Name != Me.CubeGrid.Name);
            Echo($"containers: {Containers.Count}");

            RefineryDisplay = new MonitorSetup(GridTerminalSystem, "LCD Refinery Display");
        }

        public bool SearchForCargo(IMyTerminalBlock block)
        {
            if (block is IMyCargoContainer) return true;
            return false;
        }

        public class MonitorSetup
        {
            public IMyTextSurface Surface;
            public Vector2 ScreenSize;

            public MonitorSetup(IMyGridTerminalSystem system, string surfaceName)
            {
                Surface = (IMyTextSurface) system.GetBlockWithName(surfaceName);
                Surface.ContentType = ContentType.SCRIPT;
                Surface.ScriptBackgroundColor = Color.Black;
                ScreenSize = Surface.SurfaceSize;
            }
        }

        #region PreludeFooter

    }
}

#endregion