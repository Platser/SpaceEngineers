using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using VRageMath;
using VRage.Game;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using Sandbox.Game.EntityComponents;
using VRage.Game.Components;
using VRage.Collections;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
namespace SolarTrackerRotor
{
    public sealed class Program : MyGridProgram
    {
        //------------BEGIN--------------

        /*** Notes ********************************************************************
        *
        * Originated from: https://youtu.be/c_XTmqCV4N0
        *
        *****************************************************************************/

        /*** TODO *********************************************************************
        * - Rename argument for multi-purpose functions
        * - Try to implement in class
        ******************************************************************************/


        float solarPanelMaxOutput = 0.04f; // 0.04 for small grid, 0.12 for large grid
        float rollSpeedLimit = 2.0f;
        float rotorRollSolarPowerMultiplier = 100;
        float rotorYawPitchMultiplier = 5;
        Vector3D vectorToPolar = new Vector3D(0, -1, 0);

        IMyCameraBlock CamPolar;
        IMySolarPanel solarPanel;
        List<IMyTerminalBlock> Rotors = new List<IMyTerminalBlock>();
        IMyTextPanel textPanel;

        string solarTrackerStatus;

        void MyEcho(string text, bool append = false)
        {
            Echo(text);
            if ( textPanel != null )
            {
                textPanel.WriteText("\n"+text, append);
            }
        }

        public Program()
        {

            /*** Get Blocks ******************************************************/

            textPanel = GridTerminalSystem.GetBlockWithName("SolarTrackerTextPanel") as IMyTextPanel;
            GridTerminalSystem.SearchBlocksOfName("SolarTrackerRotor", Rotors);
            CamPolar = GridTerminalSystem.GetBlockWithName("SolarTrackerPolarCamera") as IMyCameraBlock;
            if (CamPolar == null)
                throw new Exception("ERROR: Cant get camera block with name \"SolarTrackerPolarCamera\"");
            solarPanel = GridTerminalSystem.GetBlockWithName("SolarTrackerSolarPanel") as IMySolarPanel;
            if (solarPanel == null)
                throw new Exception("ERROR: Cant get solar panel block with name \"SolarTrackerSolarPanel\"");
        }

        public void Main(string argument)
        {
            switch (argument)
            {
                case "Start":
                    {
                        Runtime.UpdateFrequency = UpdateFrequency.Update10;
                        MyEcho("Solar tracker is running");
                        TrackSunRotor(getSolTrackingVector());
                        break;
                    }
                case "Stop":
                    {
                        Runtime.UpdateFrequency = UpdateFrequency.None;
                        MyEcho("Solar tracker is stopped");
                        StopRotors();
                        break;
                    }

                default:
                    if ( solarTrackerStatus == null || solarTrackerStatus == "Stop" ) {
                        MyEcho("Solar tracker is stopped");
                    } else {
                        MyEcho("Solar tracker is running");
                    }
                    TrackSunRotor(getSolTrackingVector());
                    break;
            }
        }

        float SolarOutput = 0;
        int dir = 1;
        int cnt = 0;

        Vector3D getSolTrackingVector()
        {
            cnt++;
            Echo(cnt.ToString());
            Vector3D vectorLeft = CamPolar.WorldMatrix.Left;
            Vector3D vectorUp = CamPolar.WorldMatrix.Up;
            Vector3D vectorForward = CamPolar.WorldMatrix.Forward;
            MyEcho("Vessle orientation:" +
                   "\n  X: " + Math.Round(vectorForward.X, 5) +
                   "\n  Y: " + Math.Round(vectorForward.X, 5) +
                   "\n  Z: " + Math.Round(vectorForward.X, 5),
                   true );
            double targetYaw   = -(float)vectorToPolar.Dot(vectorLeft) * rotorYawPitchMultiplier;
            double targetPitch = -(float)vectorToPolar.Dot(vectorUp)   * rotorYawPitchMultiplier;
            double targetRoll = 0;
            if (Math.Abs(targetPitch) + Math.Abs(targetYaw) < 0.05f)
            {
                MyEcho("Tracker: Roll");
                if (cnt >= 20)
                {
                    float OutputGain = solarPanel.MaxOutput - SolarOutput;
                    if (OutputGain < 0)
                        dir *= -1;
                    SolarOutput = solarPanel.MaxOutput;
                    cnt = 0;
                }
                targetRoll = (float)(dir * (solarPanelMaxOutput - solarPanel.MaxOutput) * rotorRollSolarPowerMultiplier);
                targetRoll = Math.Min(Math.Max(targetRoll, -rollSpeedLimit), rollSpeedLimit);
            } else {
                MyEcho("Tracker: Yaw/Pitch");
            }
            MyEcho("Navigation vector:" + 
                 "\n    Yaw: " + Math.Round(targetYaw, 5) +
                 "\n  Pitch: " + Math.Round(targetPitch, 5) +
                 "\n   Roll: " + Math.Round(targetYaw, 5));
            MyEcho("Solar output: " + Math.Round(SolarOutput, 5));
            return new Vector3D(targetYaw, -targetPitch, 0);
        }
        void TrackSunRotor(Vector3D target)
        {
            for (int i = 0; i < Rotors.Count; i++)
            {
                IMyMotorStator rotor = Rotors[i] as IMyMotorStator;
                if (rotor != null)
                {
                    if (rotor.CustomName == "SolarTrackerRotorYaw")
                        rotor.TargetVelocityRPM = (float)target.GetDim(0);
                    if (rotor.CustomName == "SolarTrackerRotorPitch")
                        rotor.TargetVelocityRPM = (float)target.GetDim(1);
                    if (rotor.CustomName == "SolarTrackerRotorRoll")
                        rotor.TargetVelocityRPM = (float)target.GetDim(2);
                }
            }
        }
        void StopRotors()
        {

            for (int i = 0; i < Rotors.Count; i++)
            {
                IMyMotorStator rotor = Rotors[i] as IMyMotorStator;
                if (rotor != null)
                {
                    rotor.TargetVelocityRPM = 0f;
                }
            }
        }


        public void Save()
        { }

        //------------END--------------
    }
}