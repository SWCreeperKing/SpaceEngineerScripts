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
using VRage.Game.ObjectBuilders.Definitions;

// Change this namespace for each script you create.
namespace SpaceEngineers.AutoDoors
{
    public sealed class Program : MyGridProgram
    {
        // Your code goes between the next #endregion and #region

        #endregion

        private const UpdateType UpdateFlags = UpdateType.Terminal | UpdateType.Trigger | UpdateType.Script;
        
        private readonly List<IMySensorBlock> Sensors = new List<IMySensorBlock>();
        private readonly List<IMyDoor> Doors = new List<IMyDoor>();
        private readonly List<AutoDoor> AutoDoors = new List<AutoDoor>();

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

            foreach (var autoDoor in AutoDoors)
            {
                autoDoor.Update();
            }
        }

        public void Setup()
        {
            AutoDoors.Clear();
            Sensors.Clear();
            Doors.Clear();

            GridTerminalSystem.GetBlocksOfType(Sensors);
            GridTerminalSystem.GetBlocksOfType(Doors);

            Sensors.RemoveAll(sensor => !sensor.CustomName.StartsWith("ADS "));
            Doors.RemoveAll(door => !door.CustomName.StartsWith("ADD "));

            Echo($"Auto Sensors: {Sensors.Count}");
            Echo($"Auto Doors: {Doors.Count}");

            foreach (var sensor in Sensors)
            {
                var id = sensor.CustomName.Remove(0, 4);
                AutoDoors.Add(new AutoDoor(sensor, Doors.FindAll(door => door.CustomName.Remove(0, 4) == id).ToList()));
            }
        }

        public class AutoDoor
        {
            public IMySensorBlock Sensor;
            public List<IMyDoor> Doors;

            private readonly List<MyDetectedEntityInfo> Entities = new List<MyDetectedEntityInfo>();

            public AutoDoor(IMySensorBlock sensor, List<IMyDoor> doors)
            {
                Sensor = sensor;
                Doors = doors;
            }

            public void Update()
            {
                Sensor.DetectedEntities(Entities);

                if (Entities.Any() && Doors.Any(door
                        => door.Status == DoorStatus.Closed || door.Status == DoorStatus.Closing))
                {
                    ManipulateDoor();
                }
                else if (!Entities.Any() &&
                         Doors.Any(door => door.Status == DoorStatus.Open || door.Status == DoorStatus.Opening))
                {
                    ManipulateDoor(false);
                }
            }

            public void ManipulateDoor(bool open = true)
            {
                foreach (var door in Doors)
                {
                    if (open)
                    {
                        door.OpenDoor();
                    }
                    else
                    {
                        door.CloseDoor();
                    }
                }
            }
        }

        #region PreludeFooter

    }
}

#endregion