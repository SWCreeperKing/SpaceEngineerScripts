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
using VRage;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ObjectBuilders.Definitions;

// Change this namespace for each script you create.
namespace SpaceEngineers.CricketManager
{
    public sealed class Program : MyGridProgram
    {
        // Your code goes between the next #endregion and #region

        #endregion

        private const UpdateType UpdateFlags = UpdateType.Terminal | UpdateType.Trigger | UpdateType.Script;
        
        private readonly List<IMyTerminalBlock> Containers = new List<IMyTerminalBlock>();
        private readonly List<IMyTextSurface> Surfaces = new List<IMyTextSurface>();
        private readonly List<IMyInteriorLight> Alarms = new List<IMyInteriorLight>();
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

            var containerData = Containers.Select(GetSingleCargoData);
            var totalContainerData = GetTotalData(containerData);
            var totalPercent = Percentify(totalContainerData);
            var color = GetPercentColor(totalPercent);

            foreach (var surface in Surfaces)
            {
                surface.WriteText($"{totalPercent * 100f:0.###}% full");
                surface.FontColor = color;
            }

            foreach (var alarm in Alarms)
            {
                alarm.Color = color;
                alarm.BlinkIntervalSeconds = GetInterval(totalPercent);
            }
        }

        public void Setup()
        {
            GridTerminalSystem.GetBlocksOfType(Containers, SearchForCargo);
            Containers.RemoveAll(container => container.CubeGrid.Name != Me.CubeGrid.Name);
            Echo($"Found {Containers.Count} Inventories");
            
            GridTerminalSystem.GetBlocksOfType(Surfaces);
            Surfaces.RemoveAll(surface => ((IMyTerminalBlock) surface).CubeGrid.Name != Me.CubeGrid.Name);

            foreach (var surface in Surfaces)
            {
                surface.ContentType = ContentType.TEXT_AND_IMAGE;
                surface.FontSize = 6.2f;
                surface.Alignment = TextAlignment.CENTER;
                surface.TextPadding = 20f;
            }

            Echo($"Surfaces {Surfaces.Count} Found");
            
            GridTerminalSystem.GetBlocksOfType(Alarms);
            Alarms.RemoveAll(alarm => alarm.CubeGrid.Name != Me.CubeGrid.Name);

            foreach (var alarm in Alarms)
            {
                alarm.BlinkLength = 80f;
            }

            Echo($"Alarms {Alarms.Count} Found");
        }

        public bool SearchForCargo(IMyTerminalBlock block)
        {
            if (block is IMyCockpit) return true;
            if (block is IMyCargoContainer) return true;
            if (block is IMyShipDrill) return true;
            if (block is IMyShipGrinder) return true;
            if (block is IMyShipConnector) return true;
            return false;
        }

        public MyTuple<MyFixedPoint, MyFixedPoint> GetSingleCargoData(IMyTerminalBlock block)
        {
            var inventory = block.GetInventory();
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

        public float GetInterval(float percent)
        {
            if (percent >= 1f) return .2f;
            if (percent >= .95f) return .4f;
            if (percent >= .9f) return .8f;
            if (percent >= .8f) return 1.2f;
            if (percent >= .7f) return 1.6f;
            if (percent >= .5f) return 2f;
            if (percent >= .3f) return 3f;
            return 0;
        }

        #region PreludeFooter

    }
}

#endregion