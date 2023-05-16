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

        List<IMyAirVent> vents = new List<IMyAirVent>();
        List<IMyDoor> interiorDoors = new List<IMyDoor>();
        List<IMySensorBlock> sensors = new List<IMySensorBlock>();
        List<IMyLightingBlock> lights = new List<IMyLightingBlock>();
        List<IMyAirtightHangarDoor> doors = new List<IMyAirtightHangarDoor>();
        List<IMyAirtightHangarDoor> doorsInGroup = new List<IMyAirtightHangarDoor>();
        List<HangerSet> hangers = new List<HangerSet>();
        List<IMyBlockGroup> groups = new List<IMyBlockGroup>();

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

            foreach (var hanger in hangers)
            {
                hanger.Update();
            }
        }

        public void Setup()
        {
            hangers.Clear();

            GridTerminalSystem.GetBlocksOfType(interiorDoors);
            GridTerminalSystem.GetBlocksOfType(sensors);
            GridTerminalSystem.GetBlocksOfType(lights);
            GridTerminalSystem.GetBlocksOfType(doors);
            GridTerminalSystem.GetBlocksOfType(vents);

            GridTerminalSystem.GetBlockGroups(groups, group => group.Name.StartsWith("HND "));
            foreach (var group in groups)
            {
                group.GetBlocksOfType(doorsInGroup);
                if (doorsInGroup.All(door => door.CustomName == group.Name)) continue;

                foreach (var door in doorsInGroup)
                {
                    door.CustomName = group.Name;
                }

                doors.AddRange(doorsInGroup);
            }

            sensors.RemoveAll(sensor => !sensor.CustomName.StartsWith("HNS ") && !sensor.CustomName.StartsWith("HNR "));
            lights.RemoveAll(light => !light.CustomName.StartsWith("HNIL ") && !light.CustomName.StartsWith("HNOL "));
            interiorDoors.RemoveAll(door => !door.CustomName.StartsWith("HNID "));
            doors.RemoveAll(door => !door.CustomName.StartsWith("HND "));
            vents.RemoveAll(vent => !vent.CustomName.StartsWith("HNV "));

            Echo($"sensors: {sensors.Count}");
            Echo($"doors: {doors.Count}");
            Echo($"lights: {lights.Count}");
            Echo($"vents: {vents.Count}");
            Echo($"interior: {interiorDoors.Count}");

            foreach (var id in sensors.Select(sensor => sensor.CustomName.Split(' ')[1]).Distinct())
            {
                hangers.Add(new HangerSet
                    (
                        sensors.Where(sensor => sensor.CustomName.StartsWith($"HNR {id}")).ToList(),
                        sensors.Where(sensor => sensor.CustomName.StartsWith($"HNS {id}")).ToList(),
                        doors.Where(door => door.CustomName.StartsWith($"HND {id}")).ToList(),
                        lights.Where(light
                                => light.CustomName.StartsWith($"HNIL {id}") ||
                                   light.CustomName.StartsWith($"HNOL {id}"))
                            .ToList(),
                        vents.Where(vent => vent.CustomName.StartsWith($"HNV {id}")).ToList(),
                        interiorDoors.Where(door => door.CustomName.StartsWith($"HNID {id}")).ToList()
                    )
                );
            }

            hangers.RemoveAll(hanger => hanger == null);

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

        public class HangerSet
        {
            public List<string> Ids = new List<string>();
            public List<IMyAirVent> Vents = new List<IMyAirVent>();
            public List<IMyDoor> InteriorDoors = new List<IMyDoor>();
            public List<IMySensorBlock> RoomSensors = new List<IMySensorBlock>();
            public List<IMyLightingBlock> InsideLights = new List<IMyLightingBlock>();
            public List<IMyLightingBlock> OutsideLights = new List<IMyLightingBlock>();
            public Dictionary<string, IMySensorBlock> DoorSensors = new Dictionary<string, IMySensorBlock>();

            public Dictionary<string, List<IMyAirtightHangarDoor>> Doors =
                new Dictionary<string, List<IMyAirtightHangarDoor>>();

            private List<MyDetectedEntityInfo> Entities = new List<MyDetectedEntityInfo>();

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

                    if (!Doors.ContainsKey(id))
                    {
                        Doors.Add(id, new List<IMyAirtightHangarDoor>());
                    }

                    Doors[id].Add(door);
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
                var isShipExist = false;

                foreach (var sensor in RoomSensors)
                {
                    sensor.DetectedEntities(Entities);
                    if (!Entities.Any()) continue;

                    isShipExist = true;
                    break;
                }

                foreach (var id in Ids)
                {
                    if (isShipExist)
                    {
                        OpenDoors(id);
                    }
                    else
                    {
                        DoorSensors[id].DetectedEntities(Entities);
                        if (Entities.Any())
                        {
                            OpenDoors(id);
                        }
                        else
                        {
                            CloseDoors(id);
                        }
                    }
                }
            }

            public void OpenDoors(string id)
            {
                foreach (var inDoor in InteriorDoors.Where(door => door.Status != DoorStatus.Closed || door.Status != DoorStatus.Closing))
                {
                    inDoor.CloseDoor();
                }

                if (Vents.Any(vent => !vent.Depressurize))
                {
                    LightsDepressurize();
                    foreach (var vent in Vents)
                    {
                        vent.Depressurize = true;
                    }
                }
                
                if (Vents.Any(vent => vent.GetOxygenLevel() > 0)) return;
                
                ManipulateDoors(id, door => door.Status == DoorStatus.Closed || door.Status == DoorStatus.Closing,
                    door => door.OpenDoor());
            }

            public void CloseDoors(string id)
            {
                if (Doors[id].Any(door => door.Status != DoorStatus.Closed))
                {
                    foreach (var inDoor in InteriorDoors.Where(door => door.Status != DoorStatus.Closed))
                    {
                        inDoor.CloseDoor();
                    }
                }
                else if (Vents.Any(vent => vent.Depressurize))
                {
                    LightsPressurize();
                    foreach (var vent in Vents)
                    {
                        vent.Depressurize = false;
                    }
                }
                
                ManipulateDoors(id, door => door.Status == DoorStatus.Open || door.Status == DoorStatus.Opening,
                    door => door.CloseDoor());
            }

            public void ManipulateDoors(string id, Func<IMyAirtightHangarDoor, bool> doorTest,
                Action<IMyAirtightHangarDoor> action)
            {
                foreach (var hangerDoor in Doors[id].Where(doorTest))
                {
                    action(hangerDoor);
                }
            }

            public void LightsDepressurize()
            {
                foreach (var inLight in InsideLights)
                {
                    inLight.Color = Color.Red;
                }

                foreach (var outLight in OutsideLights)
                {
                    outLight.Color = Color.Green;
                }
            }
            
            public void LightsPressurize()
            {
                foreach (var inLight in InsideLights)
                {
                    inLight.Color = Color.Green;
                }

                foreach (var outLight in OutsideLights)
                {
                    outLight.Color = Color.Red;
                }
            }
        }

        #region PreludeFooter

    }
}

#endregion