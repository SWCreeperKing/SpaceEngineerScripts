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

        private const UpdateType UpdateFlags = UpdateType.Terminal | UpdateType.Trigger | UpdateType.Script;
        
        private MonitorSetup CargoDisplay;
        private readonly List<IMyTextPanel> Surfaces = new List<IMyTextPanel>();
        private readonly List<MonitorSetup> InventoryDisplays = new List<MonitorSetup>();
        private readonly List<IMyShipConnector> Connectors = new List<IMyShipConnector>();
        private readonly List<IMyTerminalBlock> Containers = new List<IMyTerminalBlock>();
        private readonly List<IMyTerminalBlock> ShipContainers = new List<IMyTerminalBlock>();
        private readonly Dictionary<string, float> ItemCache = new Dictionary<string, float>();
        private readonly Dictionary<string, Dictionary<string, float>> AllItems = new Dictionary<string, Dictionary<string, float>>();
        private readonly Color VeryDarkRed = new Color(50, 0, 0);

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            Setup();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateFlags) != 0)
            {
                Setup();
            }

            UpdateCargoDisplay();
            UpdateInventoryDisplay();

            foreach (var blockGrid in Connectors.Where(connector
                             => connector.Status == MyShipConnectorStatus.Connected)
                         .Select(connector => connector.OtherConnector.CubeGrid))
            {
                GridTerminalSystem.GetBlocksOfType(ShipContainers, SearchForShipCargo);
                ShipContainers.RemoveAll(container => container.CubeGrid.CustomName != blockGrid.CustomName);

                foreach (var otherInventory in ShipContainers.Select(block => block.GetInventory()))
                {
                    foreach (var inventory in Containers.Select(container => container.GetInventory())
                                 .Where(inv => !inv.IsFull))
                    {
                        MyInventoryItem? invItem;
                        while ((invItem = otherInventory.GetItemAt(0)) != null)
                        {
                            if (!otherInventory.CanTransferItemTo(inventory, invItem.Value.Type)) break;
                            otherInventory.TransferItemTo(inventory, 0);
                        }

                        if (otherInventory.ItemCount == 0) break;
                    }
                }
            }
        }

        public void UpdateCargoDisplay()
        {
            var containerData = Containers.Select(GetSingleCargoData);
            var totalContainerData = GetTotalData(containerData);
            var totalPercent = Percentify(totalContainerData);
            var frame = CargoDisplay.Surface.DrawFrame();
            var surfaceSize = CargoDisplay.SurfaceSize;

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
            foreach (var key in AllItems.Keys)
            {
                AllItems[key].Clear();
            }

            var items = new List<MyInventoryItem>();

            foreach (var inventory in Containers.Select(container => container.GetInventory()))
            {
                inventory.GetItems(items);

                foreach (var item in items)
                {
                    var type = item.Type.TypeId;
                    var subType = item.Type.SubtypeId;
                    var amount = (float) item.Amount;

                    if (!AllItems.ContainsKey(type))
                    {
                        AllItems.Add(type, new Dictionary<string, float>());
                    }

                    if (!AllItems[type].ContainsKey(subType))
                    {
                        AllItems[type][subType] = 0;
                    }

                    AllItems[type][subType] += amount;
                }
            }

            foreach (var invDisplay in InventoryDisplays)
            {
                ItemCache.Clear();
                if (AllItems.Keys.Any(key => invDisplay.Flags.Contains(key)))
                {
                    foreach (var item in AllItems.Keys.Where(key => invDisplay.Flags.Contains(key))
                                 .SelectMany(key => AllItems[key]))
                    {
                        ItemCache[item.Key] = item.Value;
                    }
                }
                else
                {
                    foreach (var item in AllItems.Keys.SelectMany(key => AllItems[key]))
                    {
                        ItemCache[item.Key] = item.Value;
                    }
                }

                var orderedItems = ItemCache.OrderBy(kv => kv.Key);
                invDisplay.UpdateDisplay(orderedItems.Select(kv => kv.Key).ToList(),
                    orderedItems.Select(kv => $"{kv.Value:###,###}").ToList());
            }
        }

        public void Setup()
        {
            GridTerminalSystem.GetBlocksOfType(Connectors);
            Connectors.RemoveAll(connector
                => connector.CustomName != "System Auto-pull Connector" || connector.CubeGrid.Name != Me.CubeGrid.Name);
            Echo($"connectors: {Connectors.Count}");

            GridTerminalSystem.GetBlocksOfType(Containers, SearchForCargo);
            Containers.RemoveAll(container
                => container.CustomName != "Main Cargo Container" || container.CubeGrid.Name != Me.CubeGrid.Name);
            Echo($"containers: {Containers.Count}");

            CargoDisplay =
                new MonitorSetup((IMyTextPanel) GridTerminalSystem.GetBlockWithName("LCD Container Display"));

            GridTerminalSystem.GetBlocksOfType(Surfaces);
            Surfaces.RemoveAll(surface
                => !surface.CustomName.StartsWith("LCD Inventory Display") ||
                   surface.CubeGrid.Name != Me.CubeGrid.Name);

            InventoryDisplays.Clear();
            foreach (var surface in Surfaces)
            {
                InventoryDisplays.Add(new MonitorSetup(surface));
            }

            Echo($"surfaces: {Surfaces.Count} | {InventoryDisplays.Count}");
            Echo($"{string.Join(", ", AllItems.Keys)}");
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

            private readonly MySprite ClipRect;
            private bool ScrollDirection;
            private int ScrollOffset;

            public MonitorSetup(IMyTextPanel surface)
            {
                Surface = surface;
                Surface.ContentType = ContentType.SCRIPT;
                Surface.ScriptBackgroundColor = Color.Black;
                SurfaceSize = Surface.SurfaceSize;
                Flags = surface.GetPublicTitle().Split(',');
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