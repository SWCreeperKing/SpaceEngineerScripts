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
using IMyLightingBlock = Sandbox.ModAPI.IMyLightingBlock;

// Change this namespace for each script you create.
namespace SpaceEngineers.AutoHangerDoor
{
    public sealed class Program : MyGridProgram
    {
        // Your code goes between the next #endregion and #region

        #endregion

        private const UpdateType UpdateFlags = UpdateType.Terminal | UpdateType.Trigger | UpdateType.Script;

        private readonly List<IMyAirVent> Vents = new List<IMyAirVent>();
        private readonly List<IMyDoor> InteriorDoors = new List<IMyDoor>();
        private readonly List<IMySensorBlock> Sensors = new List<IMySensorBlock>();
        private readonly List<IMyLightingBlock> Lights = new List<IMyLightingBlock>();
        private readonly List<IMyAirtightHangarDoor> Doors = new List<IMyAirtightHangarDoor>();
        private readonly List<IMyAirtightHangarDoor> DoorsInGroup = new List<IMyAirtightHangarDoor>();
        private readonly List<HangerSet> Hangers = new List<HangerSet>();
        private readonly List<IMyBlockGroup> Groups = new List<IMyBlockGroup>();

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

            foreach (var hanger in Hangers)
            {
                hanger.Update();
            }
        }

        public void Setup()
        {
            Hangers.Clear();

            GridTerminalSystem.GetBlocksOfType(InteriorDoors);
            GridTerminalSystem.GetBlocksOfType(Sensors);
            GridTerminalSystem.GetBlocksOfType(Lights);
            GridTerminalSystem.GetBlocksOfType(Doors);
            GridTerminalSystem.GetBlocksOfType(Vents);

            GridTerminalSystem.GetBlockGroups(Groups, group => group.Name.StartsWith("HND "));
            foreach (var group in Groups)
            {
                group.GetBlocksOfType(DoorsInGroup);
                if (DoorsInGroup.All(door => door.CustomName == group.Name)) continue;

                foreach (var door in DoorsInGroup)
                {
                    door.CustomName = group.Name;
                }

                Doors.AddRange(DoorsInGroup);
            }

            Sensors.RemoveAll(sensor => !sensor.CustomName.StartsWith("HNS ") && !sensor.CustomName.StartsWith("HNR "));
            Lights.RemoveAll(light => !light.CustomName.StartsWith("HNIL ") && !light.CustomName.StartsWith("HNOL "));
            InteriorDoors.RemoveAll(door => !door.CustomName.StartsWith("HNID "));
            Doors.RemoveAll(door => !door.CustomName.StartsWith("HND "));
            Vents.RemoveAll(vent => !vent.CustomName.StartsWith("HNV "));

            Echo($"sensors: {Sensors.Count}");
            Echo($"doors: {Doors.Count}");
            Echo($"lights: {Lights.Count}");
            Echo($"vents: {Vents.Count}");
            Echo($"interior: {InteriorDoors.Count}");

            foreach (var id in Sensors.Select(sensor => sensor.CustomName.Split(' ')[1]).Distinct())
            {
                Hangers.Add(new HangerSet
                    (
                        WhereStartsWith(Sensors, $"HNR {id}"),
                        WhereStartsWith(Sensors, $"HNS {id}"),
                        WhereStartsWith(Doors, $"HND {id}"),
                        Lights.Where(light
                                => light.CustomName.StartsWith($"HNIL {id}") ||
                                   light.CustomName.StartsWith($"HNOL {id}"))
                            .ToList(),
                        WhereStartsWith(Vents, $"HNV {id}"),
                        WhereStartsWith(InteriorDoors, $"HNID {id}")
                    )
                );
            }

            Hangers.RemoveAll(hanger => hanger == null);

            /*
            sensor = (IMySensorBlock) GridTerminalSystem.GetBlockWithName("HNS Front front");
            sensor = (IMySensorBlock) GridTerminalSystem.GetBlockWithName("HNS Front left");    
            sensor = (IMySensorBlock) GridTerminalSystem.GetBlockWithName("HNS Front right");    
            roomSensor = (IMySensorBlock) GridTerminalSystem.GetBlockWithName("HNR Front");
            door = (IMyAirtightHangarDoor) GridTerminalSystem.GetBlockWithName("HND Front front");
            door = (IMyAirtightHangarDoor) GridTerminalSystem.GetBlockWithName("HND Front left");
            door = (IMyAirtightHangarDoor) GridTerminalSystem.GetBlockWithName("HND Front right");
            */
        }

        public List<T> WhereStartsWith<T>(IEnumerable<T> list, string startsWith) where T : IMyTerminalBlock
            => list.Where(item => item.CustomName.StartsWith(startsWith)).ToList();

        public class HangerSet
        {
            public static readonly Func<IMyDoor, bool> IsAndOpen = door
                => door.Status == DoorStatus.Open || door.Status == DoorStatus.Opening;

            public static readonly Func<IMyDoor, bool> IsAndClosed = door
                => door.Status == DoorStatus.Closed || door.Status == DoorStatus.Closing;

            public List<string> Ids = new List<string>();
            public List<string> DoorsToOpen = new List<string>();
            public List<string> DoorsToClose = new List<string>();
            public List<IMyAirVent> Vents = new List<IMyAirVent>();
            public List<IMyDoor> InteriorDoors = new List<IMyDoor>();
            public List<IMySensorBlock> RoomSensors = new List<IMySensorBlock>();
            public List<IMyLightingBlock> InsideLights = new List<IMyLightingBlock>();
            public List<IMyLightingBlock> OutsideLights = new List<IMyLightingBlock>();
            public Dictionary<string, IMySensorBlock> DoorSensors = new Dictionary<string, IMySensorBlock>();

            public Dictionary<string, List<IMyAirtightHangarDoor>> Doors =
                new Dictionary<string, List<IMyAirtightHangarDoor>>();

            private readonly List<MyDetectedEntityInfo> Entities = new List<MyDetectedEntityInfo>();

            public HangerSet(List<IMySensorBlock> roomSensors, List<IMySensorBlock> outdoorSensors,
                List<IMyAirtightHangarDoor> hangerDoors, List<IMyLightingBlock> lights, List<IMyAirVent> vents,
                List<IMyDoor> interiorDoors)
            {
                Vents.AddRange(vents);
                RoomSensors.AddRange(roomSensors);
                InteriorDoors.AddRange(interiorDoors);

                foreach (var sensor in outdoorSensors)
                {
                    var id = sensor.CustomName.Split(' ')[2];
                    Ids.Add(id);
                    DoorSensors[id] = sensor;
                }

                foreach (var door in hangerDoors)
                {
                    var id = door.CustomName.Split(' ')[2];
                    List<IMyAirtightHangarDoor> doorList;

                    if (!Doors.TryGetValue(id, out doorList))
                    {
                        Doors.Add(id, doorList = new List<IMyAirtightHangarDoor>());
                    }

                    doorList.Add(door);
                }

                foreach (var id in Ids)
                {
                    CloseDoors(id);
                }

                foreach (var light in lights)
                {
                    if (light.CustomName.StartsWith("HNIL"))
                    {
                        InsideLights.Add(light);
                    }
                    else
                    {
                        OutsideLights.Add(light);
                    }
                }
            }

            public void Update()
            {
                DoorsToOpen.Clear();
                DoorsToClose.Clear();
                var isShipExist = false;

                foreach (var sensor in RoomSensors)
                {
                    sensor.DetectedEntities(Entities);
                    if (!Entities.Any()) continue;

                    foreach (var id in Ids)
                    {
                        DoorsToOpen.Add(id);
                    }
                }

                if (!DoorsToOpen.Any())
                {
                    foreach (var id in Ids)
                    {
                        DoorSensors[id].DetectedEntities(Entities);
                        if (Entities.Any())
                        {
                            DoorsToOpen.Add(id);
                        }
                        else if (Doors[id].Any(door => door.Status != DoorStatus.Closed))
                        {
                            DoorsToClose.Add(id);
                        }
                    }
                }

                if (DoorsToOpen.Any())
                {
                    foreach (var id in DoorsToOpen)
                    {
                        OpenDoors(id);
                    }
                }
                else if (DoorsToClose.Any())
                {
                    foreach (var id in DoorsToClose)
                    {
                        CloseDoors(id);
                    }
                }
                else
                {
                    Depressurize(false);
                }
            }

            public void OpenDoors(string id)
            {
                foreach (var inDoor in InteriorDoors.Where(IsAndClosed))
                {
                    inDoor.CloseDoor();
                }

                if (Vents.Any(vent => !vent.Depressurize))
                {
                    Depressurize(true);
                }

                if (Vents.Any(vent => vent.GetOxygenLevel() > 0)) return;

                ManipulateDoors(id, IsAndClosed, door => door.OpenDoor());
            }

            public void CloseDoors(string id)
            {
                if (InteriorDoors.Any(door => door.Status != DoorStatus.Closed))
                {
                    foreach (var inDoor in InteriorDoors)
                    {
                        inDoor.CloseDoor();
                    }
                }

                if (Doors[id].Any(door => door.Status != DoorStatus.Closed))
                {
                    ManipulateDoors(id, IsAndOpen, door => door.CloseDoor());
                }
                else if (Vents.Any(vent => vent.Depressurize))
                {
                    Depressurize(false);
                }
            }

            public void ManipulateDoors(string id, Func<IMyDoor, bool> doorTest, Action<IMyDoor> action)
            {
                foreach (var hangerDoor in Doors[id].Where(doorTest))
                {
                    action(hangerDoor);
                }
            }

            public void Lights(bool closed)
            {
                foreach (var inLight in InsideLights)
                {
                    inLight.Color = closed ? Color.Green : Color.Red;
                }

                foreach (var outLight in OutsideLights)
                {
                    outLight.Color = closed ? Color.Red : Color.Green;
                }
            }

            public void Depressurize(bool toDepressurize)
            {
                Lights(!toDepressurize);

                foreach (var vent in Vents)
                {
                    vent.Depressurize = toDepressurize;
                }
            }
        }

        #region PreludeFooter

    }
}

#endregion