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

        Dictionary<string, IMySensorBlock> autoSensors = new Dictionary<string, IMySensorBlock>();
        Dictionary<string, List<IMyDoor>> autoDoorsOut = new Dictionary<string, List<IMyDoor>>();
        Dictionary<string, List<IMyDoor>> autoDoorsIn = new Dictionary<string, List<IMyDoor>>();
        Dictionary<string, List<IMyAirVent>> autoVents = new Dictionary<string, List<IMyAirVent>>();
        Dictionary<string, List<IMyTextSurface>> monitors = new Dictionary<string, List<IMyTextSurface>>();
        Dictionary<string, AirLockState> activeStates = new Dictionary<string, AirLockState>();
        List<IMySensorBlock> sensors = new List<IMySensorBlock>();
        List<IMyDoor> doors = new List<IMyDoor>();
        List<IMyAirVent> vents = new List<IMyAirVent>();
        List<IMyTextSurface> surfaces = new List<IMyTextSurface>();
        List<MyDetectedEntityInfo> entities = new List<MyDetectedEntityInfo>();

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
            if (updateSource == UpdateType.Terminal || updateSource == UpdateType.Trigger ||
                updateSource == UpdateType.Script)
            {
                Setup();
            }

            foreach (var sensor in sensors)
            {
                var id = sensor.CustomName.Remove(0, 4);
                sensor.DetectedEntities(entities);

                switch (activeStates[id])
                {
                    case AirLockState.Depressurize:
                        if (entities.Any()) continue;

                        if (DoorStatusAnyNot(autoDoorsIn[id], DoorStatus.Closed) ||
                            DoorStatusAnyNot(autoDoorsOut[id], DoorStatus.Closed))
                        {
                            UpdateDoors(autoDoorsIn[id], false);
                            UpdateDoors(autoDoorsOut[id], false);
                            SetText(monitors[id], "Depressurizing", Color.Cyan);
                            continue;
                        }

                        if (!VentStatusAllDepressurizing(autoVents[id]))
                        {
                            UpdateVents(autoVents[id], false);
                        }
                        else if (VentStatusAllDepressurized(autoVents[id]))
                        {
                            activeStates[id] = AirLockState.Waiting;
                            SetText(monitors[id], "Waiting", Color.Green);
                        }

                        break;

                    case AirLockState.Waiting:
                        if (DoorStatusAny(autoDoorsIn[id], DoorStatus.Open))
                        {
                            activeStates[id] = AirLockState.GoingOut;
                        }

                        if (DoorStatusAny(autoDoorsOut[id], DoorStatus.Open))
                        {
                            activeStates[id] = AirLockState.GoingIn;
                        }

                        break;

                    case AirLockState.GoingIn:
                        if (entities.Any() && DoorStatusAny(autoDoorsOut[id], DoorStatus.Open))
                        {
                            SetText(monitors[id], "Occupied", Color.Red);
                            UpdateDoors(autoDoorsOut[id], false);
                        }
                        else if (DoorStatusAll(autoDoorsOut[id], DoorStatus.Closed))
                        {
                            activeStates[id] = AirLockState.GoingInWaiting;
                        }

                        break;

                    case AirLockState.GoingOut:
                        if (entities.Any() && DoorStatusAny(autoDoorsIn[id], DoorStatus.Open))
                        {
                            SetText(monitors[id], "Occupied", Color.Red);
                            UpdateDoors(autoDoorsIn[id], false);
                        }
                        else if (DoorStatusAll(autoDoorsIn[id], DoorStatus.Closed) &&
                                 !VentStatusAllDepressurizing(autoVents[id]))
                        {
                            UpdateVents(autoVents[id], false);
                        }
                        else if (DoorStatusAll(autoDoorsIn[id], DoorStatus.Closed) &&
                                 VentStatusAllDepressurized(autoVents[id]))
                        {
                            activeStates[id] = AirLockState.GoingOutWaiting;
                        }

                        break;

                    case AirLockState.GoingInWaiting:
                        if (DoorStatusAny(autoDoorsIn[id], DoorStatus.Closed))
                        {
                            UpdateDoors(autoDoorsIn[id], true);
                        }
                        else if (DoorStatusAll(autoDoorsIn[id], DoorStatus.Open) && !entities.Any())
                        {
                            activeStates[id] = AirLockState.Depressurize;
                        }

                        break;

                    case AirLockState.GoingOutWaiting:
                        if (DoorStatusAny(autoDoorsOut[id], DoorStatus.Closed))
                        {
                            UpdateDoors(autoDoorsOut[id], true);
                        }
                        else if (DoorStatusAll(autoDoorsOut[id], DoorStatus.Open) && !entities.Any())
                        {
                            activeStates[id] = AirLockState.Depressurize;
                        }

                        break;
                }
            }
        }

        public void Setup()
        {
            autoSensors.Clear();
            autoDoorsIn.Clear();
            autoDoorsOut.Clear();
            autoVents.Clear();
            monitors.Clear();
            sensors.Clear();
            doors.Clear();
            vents.Clear();
            surfaces.Clear();

            GridTerminalSystem.GetBlocksOfType(sensors);
            GridTerminalSystem.GetBlocksOfType(doors);
            GridTerminalSystem.GetBlocksOfType(vents);
            GridTerminalSystem.GetBlocksOfType(surfaces);

            sensors.RemoveAll(sensor => !sensor.CustomName.StartsWith("AHS "));
            doors.RemoveAll(door => !door.CustomName.StartsWith("AHI ") && !door.CustomName.StartsWith("AHO "));
            vents.RemoveAll(vent => !vent.CustomName.StartsWith("AHV "));
            surfaces.RemoveAll(surface => !((IMyTextPanel) surface).CustomName.StartsWith("AHM "));

            Echo($"Auto Sensors: {sensors.Count}");
            Echo($"Auto Doors: {doors.Count}");
            Echo($"Auto Vents: {vents.Count}");
            Echo($"Monitors: {surfaces.Count}");

            foreach (var surface in surfaces)
            {
                surface.ContentType = ContentType.TEXT_AND_IMAGE;
                surface.FontSize = 6.2f;
                surface.Alignment = TextAlignment.CENTER;
                surface.TextPadding = 20f;
            }

            ;

            foreach (var sensor in sensors)
            {
                var id = sensor.CustomName.Remove(0, 4);
                if (autoSensors.ContainsKey(id)) continue;

                autoSensors.Add(id, sensor);
                autoDoorsIn.Add(id,
                    doors.FindAll(door => door.CustomName.StartsWith("AHI ") && door.CustomName.Remove(0, 4) == id)
                        .ToList());
                autoDoorsOut.Add(id,
                    doors.FindAll(door => door.CustomName.StartsWith("AHO ") && door.CustomName.Remove(0, 4) == id)
                        .ToList());
                autoVents.Add(id, vents.FindAll(vent => vent.CustomName.Remove(0, 4) == id).ToList());
                monitors.Add(id,
                    surfaces.FindAll(surface => ((IMyTextPanel) surface).CustomName.Remove(0, 4) == id).ToList());
            }

            RunStateCheck();
        }

        public void RunStateCheck()
        {
            activeStates.Clear();
            foreach (var id in autoSensors.Keys)
            {
                activeStates.Add(id, AirLockState.Depressurize);

                UpdateDoors(autoDoorsOut[id], false);
                UpdateDoors(autoDoorsIn[id], true);
                UpdateVents(autoVents[id], false);
                SetText(monitors[id], "Depressurizing", Color.Cyan);
            }
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
        {
            return doors.Any(door => door.Status == doorStatus);
        }

        public bool DoorStatusAnyNot(List<IMyDoor> doors, DoorStatus doorStatus)
        {
            return doors.Any(door => door.Status != doorStatus);
        }

        public bool DoorStatusAll(List<IMyDoor> doors, DoorStatus doorStatus)
        {
            return doors.All(door => door.Status == doorStatus);
        }

        public bool DoorStatusAllNot(List<IMyDoor> doors, DoorStatus doorStatus)
        {
            return doors.All(door => door.Status != doorStatus);
        }

        public bool VentStatusAllDepressurizing(List<IMyAirVent> vents)
        {
            return vents.All(vent => vent.Depressurize);
        }

        public bool VentStatusAllDepressurized(List<IMyAirVent> vents)
        {
            return vents.All(vent => vent.GetOxygenLevel() <= 0);
        }

        public void SetText(List<IMyTextSurface> surfaces, string text, Color color)
        {
            foreach (var surface in surfaces)
            {
                surface.WriteText(text);
                surface.FontColor = color;
            }
        }

        #region PreludeFooter

    }
}

#endregion