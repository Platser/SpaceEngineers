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
namespace SolarTracker
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
* 1. SolAxis from ini
* 2. Logging to Echo and separate LCD
* 3. Rename argument for multi-purpose functions
* 4. Try to implement in class
******************************************************************************/

        /*void printLine(string message)
        {
            LCD.WriteText(message + "\n", true);
        }*/

        /*
        CustomData:

        [Script]
        StatusLightName=Script Status Light

        [SolarTracker]
        Control=Gyro
        ;Control=Rotor
        CameraName=Camera
        GyroName=Gyroscope
        SolarPanelName=Solar Panel
        SolAxis=0 -1 0
        ;SolAxis=0.34 -0.70 -0.62

        */




        public class MyDebugHandler
        {
            public List<string> messages = new List<string>();
            private Program seProgram; // SpaceEngineers Program constructor

            // Constructor
            public MyDebugHandler(Program seProgram)
            {
                this.seProgram = seProgram;
            }

            public bool AddMessage(string message)
            {
                if (!this.messages.Contains(message))
                {
                    this.messages.Add(message);
                    this.Echo();
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public void Echo()
            {
                string concatenatedMessages = "";
                for (int i = 0; i < this.messages.Count; i++)
                {
                    concatenatedMessages += this.messages[i] + "\n";
                }
                this.seProgram.Echo(concatenatedMessages);
            }

            public void Reset()
            {
                this.messages.Clear();
                this.seProgram.Echo("");
            }
        }

        MyIni ini = new MyIni(); // https://github.com/malware-dev/MDK-SE/wiki/Handling-configuration-and-storage#the-basics

        // Vars for CustomData fields
        string statusLightName;
        string controlType; // Gyro|Rotor
        string cameraName;
        string gyroName;
        string solarPanelName;



        IMyCameraBlock Cam;
        IMyGyro Gyro;
        IMySolarPanel Panel;
        Vector3D V1, V2, Axis;

        MyDebugHandler debugHandler;


        public Program()
        {
            debugHandler = new MyDebugHandler(this);
            Echo("CheckPoint_01");

            /*** Read CustomData *************************************************/
            MyIniParseResult result;
            if (!ini.TryParse(Me.CustomData, out result))
                throw new Exception("  (ini.TryParse)    " + result.ToString());
            Echo("CheckPoint_02");
            statusLightName = ini.Get("Script", "StatusLightName").ToString();
            controlType = ini.Get("SolarTracker", "Control").ToString();
            if (controlType != "Gyro" && controlType != "Rotor")
                throw new Exception("ERROR: Illegal Control value in the CustomData: " + controlType + ", should be Gyro or Rotor");
            cameraName = ini.Get("SolarTracker", "CameraName").ToString();
            gyroName = ini.Get("SolarTracker", "GyroName").ToString();
            solarPanelName = ini.Get("SolarTracker", "SolarPanelName").ToString();

            /*** Get Blocks ******************************************************/
            Cam = GridTerminalSystem.GetBlockWithName(cameraName) as IMyCameraBlock;
            if (Cam == null)
                throw new Exception("ERROR: Cant get camera block with name " + cameraName);
            Gyro = GridTerminalSystem.GetBlockWithName(gyroName) as IMyGyro;
            if (Gyro == null)
                throw new Exception("ERROR: Cant get gyroscope block with name " + gyroName);
            Panel = GridTerminalSystem.GetBlockWithName(solarPanelName) as IMySolarPanel;
            if (Panel == null)
                throw new Exception("ERROR: Cant get solar panel block with name " + solarPanelName);
        }

        public void Main(string argument)
        {
            Echo("CheckPoint_03");
            switch (argument)
            {
                case "V1":
                    {
                        V1 = Cam.WorldMatrix.Forward;
                        Echo(V1.ToString());
                        break;
                    }
                case "V2":
                    {
                        V2 = Cam.WorldMatrix.Forward;
                        Echo(V2.ToString());
                        Axis = V1.Cross(V2);
                        Axis = Vector3D.Normalize(Axis);
                        Echo(Axis.ToString());
                        break;
                    }
                case "Start":
                    {
                        if (V1 == null || V2 == null)
                        {
                            Runtime.UpdateFrequency = UpdateFrequency.None;
                            Gyro.GyroOverride = false;
                            debugHandler.AddMessage("ERROR: The Sol vectors V1 and V2 should be defined first.");
                        }
                        else
                        {
                            Runtime.UpdateFrequency = UpdateFrequency.Update1;
                            Gyro.GyroOverride = true;
                            TrackSun();
                        }
                        break;
                    }
                case "Stop":
                    {
                        Runtime.UpdateFrequency = UpdateFrequency.None;
                        Gyro.GyroOverride = false;
                        break;
                    }

                default:
                    TrackSun();
                    break;
            }
        }

        float SolarOutput = 0;
        int dir = 1;
        int cnt = 0;
        void TrackSun()
        {
            cnt++;
            Gyro.Pitch = -(float)Axis.Dot(Gyro.WorldMatrix.Up) * 5;
            Gyro.Yaw = -(float)Axis.Dot(Gyro.WorldMatrix.Left) * 5;
            if (Math.Abs(Gyro.Pitch) + Math.Abs(Gyro.Yaw) < 0.05f)
            {
                if (cnt >= 180)
                {
                    float OutputGain = Panel.MaxOutput - SolarOutput;
                    if (OutputGain < 0)
                        dir *= -1;
                    SolarOutput = Panel.MaxOutput;
                    cnt = 0;
                }
                float Roll = (float)(dir * (0.04f - Panel.MaxOutput) * 100);
                Gyro.Roll = Math.Min(Math.Max(Roll, -0.02f), 0.02f);
            }
            else
            {
                Gyro.Roll = 0.0f;
            }
        }

        public void Save()
        { }


 

        //------------END--------------
    }
}