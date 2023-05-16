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

        List<IMySensorBlock> sensors = new List<IMySensorBlock>();
        List<IMyDoor> doors = new List<IMyDoor>();
        List<AutoDoor> autoDoors = new List<AutoDoor>();

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

            foreach (var autoDoor in autoDoors)
            {
                autoDoor.Update();
            }
        }

        public void Setup()
        {
            autoDoors.Clear();
            sensors.Clear();
            doors.Clear();

            GridTerminalSystem.GetBlocksOfType(sensors);
            GridTerminalSystem.GetBlocksOfType(doors);

            sensors.RemoveAll(sensor => !sensor.CustomName.StartsWith("ADS "));
            doors.RemoveAll(door => !door.CustomName.StartsWith("ADD "));

            Echo($"Auto Sensors: {sensors.Count}");
            Echo($"Auto Doors: {doors.Count}");

            foreach (var sensor in sensors)
            {
                var id = sensor.CustomName.Remove(0, 4);
                autoDoors.Add(new AutoDoor(sensor, doors.FindAll(door => door.CustomName.Remove(0, 4) == id).ToList()));
            }
        }

        public class AutoDoor
        {
            public IMySensorBlock Sensor;
            public List<IMyDoor> Doors;

            private List<MyDetectedEntityInfo> Entities = new List<MyDetectedEntityInfo>();

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