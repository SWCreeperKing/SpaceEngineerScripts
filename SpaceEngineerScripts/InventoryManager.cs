#region Prelude

using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using VRageMath;
using VRage.Game;
using VRage.Collections;
using Sandbox.ModAPI.Ingame;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game.EntityComponents;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ObjectBuilders.Definitions;
using IMyButtonPanel = SpaceEngineers.Game.ModAPI.IMyButtonPanel;

// Change this namespace for each script you create.
namespace SpaceEngineers.InventoryManager
{
    public sealed class Program : MyGridProgram
    {
        // Your code goes between the next #endregion and #region

        #endregion

        MonitorSetup cargoDisplay;
        List<IMyTerminalBlock> surfaces = new List<IMyTerminalBlock>();
        List<MonitorSetup> inventoryDisplays = new List<MonitorSetup>();
        List<IMyShipConnector> connectors = new List<IMyShipConnector>();
        List<IMyTerminalBlock> containers = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> shipContainers = new List<IMyTerminalBlock>();
        Dictionary<string, float> itemCache = new Dictionary<string, float>();
        Dictionary<string, Dictionary<string, float>> allItems = new Dictionary<string, Dictionary<string, float>>();
        Color VeryDarkRed = new Color(50, 0, 0);

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            Setup();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & (UpdateType.Terminal | UpdateType.Trigger | UpdateType.Script)) != 0)
            {
                Setup();
            }

            UpdateCargoDisplay();
            UpdateInventoryDisplay();

            foreach (var blockGrid in connectors.Where(connector
                             => connector.Status == MyShipConnectorStatus.Connected)
                         .Select(connector => connector.OtherConnector.CubeGrid))
            {
                GridTerminalSystem.GetBlocksOfType(shipContainers, SearchForShipCargo);
                shipContainers.RemoveAll(container => container.CubeGrid.CustomName != blockGrid.CustomName);

                foreach (var otherInventory in shipContainers.Select(block => block.GetInventory()))
                {
                    foreach (var inventory in containers.Select(container => container.GetInventory())
                                 .Where(inv => !inv.IsFull))
                    {
                        for (var slot = otherInventory.ItemCount - 1; slot >= 0; slot--)
                        {
                            var item = otherInventory.GetItemAt(slot);
                            if (item == null) continue;
                            if (!otherInventory.CanTransferItemTo(inventory, item.Value.Type)) continue;

                            otherInventory.TransferItemTo(inventory, slot);
                        }

                        if (otherInventory.ItemCount == 0) break;
                    }
                }
            }
        }

        public void UpdateCargoDisplay()
        {
            var containerData = containers.Select(GetSingleCargoData);
            var totalContainerData = GetTotalData(containerData);
            var totalPercent = Percentify(totalContainerData);
            var frame = cargoDisplay.Surface.DrawFrame();
            var surfaceSize = cargoDisplay.SurfaceSize;

            var pos = Vector2.Zero;
            var sprite1 = MonitorSetup.MakeText($"Total: {totalPercent * 100f:0.###}% full", pos,
                color: GetPercentColor(totalPercent), scale: 2, align: TextAlignment.LEFT);
            frame.Add(sprite1);

            pos.X = surfaceSize.X / 2f;
            pos.Y += 15;
            var sprite2 = MonitorSetup.MakeText("_______________________________", pos, color: Color.Cyan, scale: 2);
            frame.Add(sprite2);

            pos.Y += 60;
            pos.X = 0;
            var numberPos = pos;

            var count = 0;
            foreach (var rawPercent in containerData)
            {
                if (rawPercent.Item1 == 0) continue;
                count++;

                var percent = Percentify(rawPercent);
                var color = GetPercentColor(percent);

                var materialName = MonitorSetup.MakeText($"Cargo #{count}: {percent * 100f:0.###}% full", pos,
                    align: TextAlignment.LEFT, color: color, scale: 1.3f);

                frame.Add(materialName);

                pos.Y += 35;
                numberPos.Y += 35;
            }

            frame.Dispose();
        }

        public void UpdateInventoryDisplay()
        {
            foreach (var key in allItems.Keys)
            {
                allItems[key].Clear();
            }

            var items = new List<MyInventoryItem>();

            foreach (var inventory in containers.Select(container => container.GetInventory()))
            {
                inventory.GetItems(items);

                foreach (var item in items)
                {
                    var type = item.Type.TypeId;
                    var subType = item.Type.SubtypeId;
                    var amount = (float) item.Amount;

                    if (!allItems.ContainsKey(type))
                    {
                        allItems.Add(type, new Dictionary<string, float>());
                    }

                    if (!allItems[type].ContainsKey(subType))
                    {
                        allItems[type][subType] = 0;
                    }

                    allItems[type][subType] += amount;
                }
            }

            foreach (var invDisplay in inventoryDisplays)
            {
                itemCache.Clear();
                if (allItems.Keys.Any(key => invDisplay.Flags.Contains(key)))
                {
                    foreach (var item in allItems.Keys.Where(key => invDisplay.Flags.Contains(key))
                                 .SelectMany(key => allItems[key]))
                    {
                        itemCache[item.Key] = item.Value;
                    }
                }
                else
                {
                    foreach (var item in allItems.Keys.SelectMany(key => allItems[key]))
                    {
                        itemCache[item.Key] = item.Value;
                    }
                }

                var orderedItems = itemCache.OrderBy(kv => kv.Key);
                invDisplay.UpdateDisplay(orderedItems.Select(kv => kv.Key).ToList(),
                    orderedItems.Select(kv => $"{kv.Value:###,###}").ToList());
            }
        }

        public void Setup()
        {
            GridTerminalSystem.GetBlocksOfType(connectors);
            connectors.RemoveAll(connector
                => connector.CustomName != "System Auto-pull Connector" || connector.CubeGrid.Name != Me.CubeGrid.Name);
            Echo($"connectors: {connectors.Count}");

            GridTerminalSystem.GetBlocksOfType(containers, SearchForCargo);
            containers.RemoveAll(container
                => container.CustomName != "Main Cargo Container" || container.CubeGrid.Name != Me.CubeGrid.Name);
            Echo($"containers: {containers.Count}");

            cargoDisplay =
                new MonitorSetup((IMyTextSurface) GridTerminalSystem.GetBlockWithName("LCD Container Display"));

            GridTerminalSystem.GetBlocksOfType(surfaces);
            surfaces.RemoveAll(surface
                =>
            {
                var block = surface;
                return !block.CustomName.StartsWith("LCD Inventory Display") || block.CubeGrid.Name != Me.CubeGrid.Name;
            });

            inventoryDisplays.Clear();
            foreach (var surface in surfaces)
            {
                inventoryDisplays.Add(new MonitorSetup((IMyTextSurface) surface));
            }

            Echo($"surfaces: {surfaces.Count} | {inventoryDisplays.Count}");
            Echo($"{string.Join(", ", allItems.Keys)}");
        }

        public bool SearchForShipCargo(IMyTerminalBlock block)
        {
            if (block is IMyCockpit) return true;
            if (block is IMyCargoContainer) return true;
            if (block is IMyShipDrill) return true;
            if (block is IMyShipGrinder) return true;
            if (block is IMyShipConnector) return true;
            return false;
        }

        public bool SearchForCargo(IMyTerminalBlock block)
        {
            if (block is IMyCargoContainer) return true;
            return false;
        }

        public MyTuple<MyFixedPoint, MyFixedPoint> GetSingleCargoData(IMyTerminalBlock block)
        {
            IMyInventory inventory = block.GetInventory();
            return MyTuple.Create(inventory.CurrentVolume, inventory.MaxVolume);
        }

        public MyTuple<MyFixedPoint, MyFixedPoint> GetTotalData(IEnumerable<MyTuple<MyFixedPoint, MyFixedPoint>> array)
        {
            MyFixedPoint used = 0;
            MyFixedPoint total = 0;

            foreach (var tuple in array)
            {
                used += tuple.Item1;
                total += tuple.Item2;
            }

            return MyTuple.Create(used, total);
        }

        public float Percentify(MyTuple<MyFixedPoint, MyFixedPoint> tuple)
        {
            return (float) tuple.Item1 / (float) tuple.Item2;
        }

        public Color GetPercentColor(float percent)
        {
            if (percent >= 1f) return VeryDarkRed;
            if (percent >= .95f) return Color.DarkRed;
            if (percent >= .9f) return Color.Red;
            if (percent >= .8f) return Color.OrangeRed;
            if (percent >= .7f) return Color.Orange;
            if (percent >= .5f) return Color.Orange;
            if (percent >= .3f) return Color.Green;
            return Color.LightGreen;
        }

        public class MonitorSetup
        {
            public const int ScrollSpeed = 10;

            public IMyTextSurface Surface;
            public Vector2 SurfaceSize;
            public Color[] ColorOptions = { Color.White, new Color(60, 60, 60) };
            public string[] Flags;

            private MySprite ClipRect;
            private bool ScrollDirection;
            private int ScrollOffset;

            public MonitorSetup(IMyTextSurface surface)
            {
                Surface = surface;
                Surface.ContentType = ContentType.SCRIPT;
                Surface.ScriptBackgroundColor = Color.Black;
                SurfaceSize = Surface.SurfaceSize;
                Flags = ((IMyTextPanel) surface).GetPublicTitle().Split(',');
                if (!Flags.Any())
                {
                    Flags = new[] { "Default Text" };
                }

                ClipRect = new MySprite(SpriteType.CLIP_RECT, "SquareSimple", new Vector2(0, 75), SurfaceSize);
            }

            public void UpdateDisplay(List<string> contents, List<string> subContents)
            {
                var frame = Surface.DrawFrame();

                var pos = new Vector2(SurfaceSize.X / 2f, 0);
                var sprite1 = MakeText(Flags[0], pos, color: Color.White, scale: 2, align: TextAlignment.CENTER);
                frame.Add(sprite1);

                pos.Y += 15;
                var sprite2 = MakeText("_______________________________", pos, color: Color.Cyan, scale: 2);
                frame.Add(sprite2);

                pos.Y += 60;
                pos.X = 0;

                var maxTextLength = 30 * contents.Count + 75;
                if (maxTextLength > SurfaceSize.Y)
                {
                    var maxOffset = maxTextLength - (int) Math.Floor(SurfaceSize.Y);
                    ScrollOffset = Math.Max(Math.Min(ScrollOffset, maxOffset), 0);

                    if (ScrollOffset <= 0 && !ScrollDirection)
                    {
                        ScrollDirection = true;
                    }
                    else if (ScrollOffset >= maxOffset && ScrollDirection)
                    {
                        ScrollDirection = false;
                    }

                    pos.Y += -ScrollOffset;
                    ScrollOffset += ScrollDirection ? ScrollSpeed : -ScrollSpeed;
                }
                else ScrollOffset = 0;

                var subPos = pos;
                subPos.X = SurfaceSize.X;

                frame.Add(ClipRect);

                for (var i = 0; i < contents.Count; i++)
                {
                    var content = contents[i];
                    var subContent = subContents[i];

                    var color = ColorOptions[i % ColorOptions.Length];

                    var materialName = MakeText(content, pos, align: TextAlignment.LEFT, color: color, scale: 1.3f);
                    var materialAmount = MakeText(subContent, subPos, align: TextAlignment.RIGHT, color: color,
                        scale: 1.3f);

                    frame.Add(materialName);
                    frame.Add(materialAmount);

                    pos.Y += 30;
                    subPos.Y = pos.Y;
                }

                frame.Dispose();
            }


            public static MySprite MakeText(string text, Vector2 pos, TextAlignment align = TextAlignment.CENTER,
                Color? color = null, float scale = 1f, string fontId = null, Vector2? size = null)
            {
                return new MySprite(SpriteType.TEXT, text, pos, size ?? Vector2.Zero, color ?? Color.White, fontId,
                    align,
                    scale);
            }
        }

        #region PreludeFooter

    }
}

#endregion