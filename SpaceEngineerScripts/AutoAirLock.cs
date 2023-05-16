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
namespace SpaceEngineers.AutoAirLock
{
    public sealed class Program : MyGridProgram
    {
        // Your code goes between the next #endregion and #region

        #endregion

        private const UpdateType UpdateFlags = UpdateType.Terminal | UpdateType.Trigger | UpdateType.Script;
        private readonly List<IMySensorBlock> Sensors = new List<IMySensorBlock>();
        private readonly List<IMyDoor> Doors = new List<IMyDoor>();
        private readonly List<IMyAirVent> Vents = new List<IMyAirVent>();
        private readonly List<IMyTextPanel> Surfaces = new List<IMyTextPanel>();
        private readonly List<AirLock> AirLocks = new List<AirLock>();

        public enum AirLockState
        {
            Depressurize,
            GoingOut,
            GoingIn,
            Waiting,
            GoingInWaiting,
            GoingOutWaiting
        }

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

            foreach (var airlock in AirLocks)
            {
                airlock.Update();
            }
        }

        public void Setup()
        {
            AirLocks.Clear();
            Sensors.Clear();
            Doors.Clear();
            Vents.Clear();
            Surfaces.Clear();

            GridTerminalSystem.GetBlocksOfType(Sensors);
            GridTerminalSystem.GetBlocksOfType(Doors);
            GridTerminalSystem.GetBlocksOfType(Vents);
            GridTerminalSystem.GetBlocksOfType(Surfaces);

            Sensors.RemoveAll(sensor => !sensor.CustomName.StartsWith("AHS "));
            Doors.RemoveAll(door => !door.CustomName.StartsWith("AHI ") && !door.CustomName.StartsWith("AHO "));
            Vents.RemoveAll(vent => !vent.CustomName.StartsWith("AHV "));
            Surfaces.RemoveAll(surface => !((IMyTextPanel) surface).CustomName.StartsWith("AHM "));

            Echo($"Auto Sensors: {Sensors.Count}");
            Echo($"Auto Doors: {Doors.Count}");
            Echo($"Auto Vents: {Vents.Count}");
            Echo($"Monitors: {Surfaces.Count}");

            foreach (var surface in Surfaces)
            {
                surface.ContentType = ContentType.TEXT_AND_IMAGE;
                surface.FontSize = 6.2f;
                surface.Alignment = TextAlignment.CENTER;
                surface.TextPadding = 20f;
            }

            foreach (var id in Sensors.Select(Name).Distinct())
            {
                AirLocks.Add(new AirLock(
                        Sensors.First(sensor => Name(sensor) == id),
                        WhereStartsWith(Doors, $"AHI {id}"),
                        WhereStartsWith(Doors, $"AHO {id}"),
                        WhereStartsWith(Vents, $"AHV {id}"),
                        Surfaces.Where(panel => Name(panel) == id).Select(panel => (IMyTextSurface) panel).ToList()
                    )
                );
            }
        }

        public string Name(IMyTerminalBlock block) => block.CustomName.Remove(0, 4);

        public List<T> WhereStartsWith<T>(IEnumerable<T> list, string startsWith) where T : IMyTerminalBlock
            => list.Where(item => item.CustomName.StartsWith(startsWith)).ToList();

        public class AirLock
        {
            public IMySensorBlock Sensor;
            public AirLockState ActiveState;
            public List<IMyDoor> DoorsIn;
            public List<IMyDoor> DoorsOut;
            public List<IMyAirVent> Vents;
            public List<IMyTextSurface> Monitors;

            private readonly List<MyDetectedEntityInfo> Entities = new List<MyDetectedEntityInfo>();

            public AirLock(IMySensorBlock sensor, List<IMyDoor> doorsIn, List<IMyDoor> doorsOut, List<IMyAirVent> vents,
                List<IMyTextSurface> monitors)
            {
                Sensor = sensor;
                DoorsIn = doorsIn;
                DoorsOut = doorsOut;
                Vents = vents;
                Monitors = monitors;
                RunStateCheck();
            }

            public void Update()
            {
                Sensor.DetectedEntities(Entities);

                switch (ActiveState)
                {
                    case AirLockState.Depressurize:
                        if (Entities.Any()) return;

                        if (DoorStatusAnyNot(DoorsIn, DoorStatus.Closed) ||
                            DoorStatusAnyNot(DoorsOut, DoorStatus.Closed))
                        {
                            UpdateDoors(DoorsIn, false);
                            UpdateDoors(DoorsOut, false);
                            SetText(Monitors, "Depressurizing", Color.Cyan);
                            return;
                        }

                        if (!VentStatusAllDepressurizing(Vents))
                        {
                            UpdateVents(Vents, false);
                        }
                        else if (VentStatusAllDepressurized(Vents))
                        {
                            ActiveState = AirLockState.Waiting;
                            SetText(Monitors, "Waiting", Color.Green);
                        }

                        break;

                    case AirLockState.Waiting:
                        if (DoorStatusAny(DoorsIn, DoorStatus.Open))
                        {
                            ActiveState = AirLockState.GoingOut;
                        }

                        if (DoorStatusAny(DoorsOut, DoorStatus.Open))
                        {
                            ActiveState = AirLockState.GoingIn;
                        }

                        break;

                    case AirLockState.GoingIn:
                        if (Entities.Any() && DoorStatusAny(DoorsOut, DoorStatus.Open))
                        {
                            SetText(Monitors, "Occupied", Color.Red);
                            UpdateDoors(DoorsOut, false);
                        }
                        else if (DoorStatusAll(DoorsOut, DoorStatus.Closed))
                        {
                            ActiveState = AirLockState.GoingInWaiting;
                        }

                        break;

                    case AirLockState.GoingOut:
                        if (Entities.Any() && DoorStatusAny(DoorsIn, DoorStatus.Open))
                        {
                            SetText(Monitors, "Occupied", Color.Red);
                            UpdateDoors(DoorsIn, false);
                        }
                        else if (DoorStatusAll(DoorsIn, DoorStatus.Closed) &&
                                 !VentStatusAllDepressurizing(Vents))
                        {
                            UpdateVents(Vents, false);
                        }
                        else if (DoorStatusAll(DoorsIn, DoorStatus.Closed) &&
                                 VentStatusAllDepressurized(Vents))
                        {
                            ActiveState = AirLockState.GoingOutWaiting;
                        }

                        break;

                    case AirLockState.GoingInWaiting:
                        if (DoorStatusAny(DoorsIn, DoorStatus.Closed))
                        {
                            UpdateDoors(DoorsIn, true);
                        }
                        else if (DoorStatusAll(DoorsIn, DoorStatus.Open) && !Entities.Any())
                        {
                            ActiveState = AirLockState.Depressurize;
                        }

                        break;

                    case AirLockState.GoingOutWaiting:
                        if (DoorStatusAny(DoorsOut, DoorStatus.Closed))
                        {
                            UpdateDoors(DoorsOut, true);
                        }
                        else if (DoorStatusAll(DoorsOut, DoorStatus.Open) && !Entities.Any())
                        {
                            ActiveState = AirLockState.Depressurize;
                        }

                        break;
                }
            }
            
            public void RunStateCheck()
            {
                ActiveState = AirLockState.Depressurize;
                UpdateDoors(DoorsOut, false);
                UpdateDoors(DoorsIn, true);
                UpdateVents(Vents, false);
                SetText(Monitors, "Depressurizing", Color.Cyan);
            }

            public void UpdateDoors(List<IMyDoor> doors, bool openDoor)
            {
                foreach (var door in doors)
                {
                    if (openDoor)
                    {
                        door.OpenDoor();
                    }
                    else
                    {
                        door.CloseDoor();
                    }
                }
            }

            public void UpdateVents(List<IMyAirVent> vents, bool pressurize)
            {
                foreach (var vent in vents)
                {
                    vent.Depressurize = !pressurize;
                }
            }

            public bool DoorStatusAny(List<IMyDoor> doors, DoorStatus doorStatus)
                => doors.Any(door => door.Status == doorStatus);

            public bool DoorStatusAnyNot(List<IMyDoor> doors, DoorStatus doorStatus)
                => doors.Any(door => door.Status != doorStatus);

            public bool DoorStatusAll(List<IMyDoor> doors, DoorStatus doorStatus)
                => doors.All(door => door.Status == doorStatus);

            public bool DoorStatusAllNot(List<IMyDoor> doors, DoorStatus doorStatus)
                => doors.All(door => door.Status != doorStatus);

            public bool VentStatusAllDepressurizing(List<IMyAirVent> vents) => vents.All(vent => vent.Depressurize);

            public bool VentStatusAllDepressurized(List<IMyAirVent> vents)
                => vents.All(vent => vent.GetOxygenLevel() <= 0);

            public void SetText(List<IMyTextSurface> surfaces, string text, Color color)
            {
                foreach (var surface in surfaces)
                {
                    surface.WriteText(text);
                    surface.FontColor = color;
                }
            }
        }

        #region PreludeFooter

    }
}

#endregion