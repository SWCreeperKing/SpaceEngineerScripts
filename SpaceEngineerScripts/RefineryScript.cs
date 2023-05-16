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

        string[] ids =
        {
            "StoneOreToIngot", "IronOreToIngot", "NickelOreToIngot", "SiliconOreToIngot", "MagnesiumOreToIngot",
            "CobaltOreToIngot", "SilverOreToIngot", "GoldOreToIngot", "PlatinumOreToIngot", "UraniumOreToIngot"
        };

        string[] plane =
        {
            "Stone", "Iron", "Nickel", "Silicon", "Magnesium", "Cobalt", "Silver", "Gold", "Platinum", "Uranium"
        };

        MyItemType[] itemTypes;
        MyDefinitionId[] definitions;
        MonitorSetup refineryDisplay;
        List<IMyRefinery> refineries = new List<IMyRefinery>();
        List<IMyTerminalBlock> containers = new List<IMyTerminalBlock>();
        List<MyProductionItem> refineryQueue = new List<MyProductionItem>();
        int refineryPage;
        int resourceNumber;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            definitions = ids.Select(id => MyDefinitionId.Parse($"MyObjectBuilder_BlueprintDefinition/{id}")).ToArray();
            itemTypes = plane.Select(plane => MyItemType.MakeOre(plane)).ToArray();
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
                            resourceNumber++;
                            resourceNumber %= ids.Length;
                            break;
                        case "up":
                            resourceNumber--;
                            if (resourceNumber < 0)
                            {
                                resourceNumber = ids.Length - 1;
                            }

                            break;
                        case "toggle":
                            var refinery = refineries[refineryPage];
                            var item = itemTypes[resourceNumber];
                            var refineryInv = refinery.GetInventory();

                            foreach (var inventory in containers.Select(container => container.GetInventory())
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
                            refinery.GetQueue(refineryQueue);

                            var amount = 0f;
                            foreach (var container in containers)
                            {
                                var inv = container.GetInventory();
                                var invItem = inv.FindItem(item);

                                if (invItem == null) continue;

                                var localAmount = inv.GetItemAmount(item);
                                inv.TransferItemTo(refineryInv, invItem.Value, localAmount);
                                amount += (float) localAmount;
                            }

                            Echo($"setting {plane[resourceNumber]} to prio.1 with amount of {amount}");

                            break;
                        case "page":
                            refineryPage++;
                            refineryPage %= refineries.Count;
                            break;
                    }
                }
            }
            catch
            {
                refineryPage = 0;
                resourceNumber = 0;
            }

            if ((updateSource & (UpdateType.Terminal | UpdateType.Trigger | UpdateType.Script)) != 0)
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
            var refinery = refineries[refineryPage];
            refinery.GetQueue(refineryQueue);

            var frame = refineryDisplay.Surface.DrawFrame();
            var surfaceSize = refineryDisplay.ScreenSize;
            var pos = Vector2.Zero;

            var sprite1 = MakeText($"{refineryPage + 1}/{refineries.Count}|{refinery.CustomName}", pos, scale: 2,
                align: TextAlignment.LEFT);
            frame.Add(sprite1);

            pos.X = surfaceSize.X / 2f;
            pos.Y += 10;
            var sprite2 = MakeText("_______________________________", pos, color: Color.Cyan, scale: 2);
            frame.Add(sprite2);

            pos.Y += 60;
            pos.X = 0;
            var count = 0;

            foreach (var resource in ids)
            {
                var index = 99;

                if (refineryQueue.Any(item => item.BlueprintId.SubtypeName == resource))
                {
                    index = refineryQueue.FindIndex(item => item.BlueprintId.SubtypeName == resource) + 1;
                }

                Color color;
                if (containers.All(container => container.GetInventory().FindItem(itemTypes[count]) == null) &&
                    index == 99)
                {
                    color = count == resourceNumber ? Color.MediumVioletRed : Color.Red;
                }
                else if (index != 99)
                {
                    color = count == resourceNumber ? Color.GreenYellow : Color.Green;
                }
                else
                {
                    color = count == resourceNumber ? Color.Blue : Color.White;
                }

                var materialName = MakeText($"{index:00}   {plane[count]}", pos, TextAlignment.LEFT, color,
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
            GridTerminalSystem.GetBlocksOfType(refineries);
            //refineries.RemoveAll(refinery => refinery.CubeGrid.Name != Me.CubeGrid.Name);

            GridTerminalSystem.GetBlocksOfType(containers, SearchForCargo);
            containers.RemoveAll(container
                => container.CustomName != "Main Cargo Container" || container.CubeGrid.Name != Me.CubeGrid.Name);
            Echo($"containers: {containers.Count}");

            refineryDisplay = new MonitorSetup(GridTerminalSystem, "LCD Refinery Display");
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