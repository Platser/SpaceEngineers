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

        SolarTracker MySolarTracker;

        public Program()
        {
            MySolarTracker = new SolarTracker(this);
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument, UpdateType updateType)
        {
            MySolarTracker.ParseCommand(argument, updateType);
        }

        public void Save()
        { }

        private class NavigationComputer
        {
            /*** Notes ********************************************************************
            *
            *   Navigation algorithms are based on: https://www.youtube.com/channel/UCBC9faYOxS0yBBSS3uOx7LQ
            *
            *****************************************************************************/
            IMyTerminalBlock vessleOrientationBlock;
            
            public NavigationComputer ( IMyTerminalBlock vessleOrientationBlock )
            {
                this.vessleOrientationBlock = myVessleOrientationBlock;
            }

            public Vector3D Forward() { return vessleOrientationBlock.WorldMatrix.Forward; }
            public Vector3D Up()      { return vessleOrientationBlock.WorldMatrix.Up;      }
            public Vector3D Left()    { return vessleOrientationBlock.WorldMatrix.Left;    }

            public double X() { return this.Forward.X; }
            public double Y() { return this.Forward.Y; }
            public double Z() { return this.Forward.Z; }

            public double Yaw ( Vector3D targetVector = null )
            {
                /* if targetVector in null, then it shouyld be calculated, but this is not yet implemented */
                Vector3D vessleLeft = vessleOrientationBlock.WorldMatrix.Left;
                double yaw = -(float)targetVector.Dot(vessleLeft);
                return yaw;
            }

            public double Pitch ( Vector3D targetVector = null )
            {
                /* if targetVector in null, then it shouyld be calculated, but this is not yet implemented */
                Vector3D vessleUp = vessleOrientationBlock.WorldMatrix.Up;
                double pitch = -(float)targetVector.Dot(vessleUp);
                return pitch;
            }

            public string ReportOrientation(int precision = 5)
            {
                return "Vessle orientation:" +
                   "\n  X: " + Math.Round(this.X, precision) +
                   "\n  Y: " + Math.Round(this.Y, precision) +
                   "\n  Z: " + Math.Round(this.Z, precision);
            }
        }

        private class SolarTracker
        {

            /*** Notes ********************************************************************
            *
            * SolarTracking algorithms are taken from: https://youtu.be/c_XTmqCV4N0
            *
            *****************************************************************************/

            // Constants (Can be changed for tuning)
            private const int operatingPeriod = 120; // In terms of game ticks, game tick = 1/60sec
            private const float rotorYawPitchMultiplier = 2f;
            private const float rotorRollSolarPowerMultiplier = 100f;
            private const float rotorSpeedLimitRPM = 1f;

            // Null Variables
            private Program seProgram;
            private IMySolarPanel SolarPanel;
            private IMyTextPanel LCD;
            private List<IMyTerminalBlock> Rotators = new List<IMyTerminalBlock>(); // Rotors or Gyros
            private NavigationComputer NavComp;
            private IMyCameraBlock polarCamera;
            private Vector3D sunAxis;
            private double previousYaw;
            private double previousPitch;

            // Variables
            private int gameTicksCount = 0;
            private float previousSolarPanelOutput = 0;
            private float maxSolarPanelOutput = 0;
            private bool status = false; // false = stop, true = work

            // Properties
            public float SolarPanelOutput { get { return this.SolarPanel.MaxOutput; } }

            private int rollDirection = 1;
            private int RollDirection
            {
                get
                {
                    if ( this.SolarPanelOutput < this.previousSolarPanelOutput )
                    {
                        this.rollDirection *= -1;
                    }
                    this.previousSolarPanelOutput = this.SolarPanelOutput;
                    return this.rollDirection;
                }
            }
            
            private string rotatorType = "rotor";
            public string RotatorType
            {
                get
                {
                    return this.rotatorType;
                }
                set
                {
                    if ( value != "rotor" && value != "gyro" )
                    {
                        throw new Exception("ERROR: Illegal RotatorType: \"" + value + "\", should be \"rotor\" or \"gyro\".");
                    }
                    this.rotatorType = value;
                }
            }
            
            private string RotatorName
            {
                get
                {
                    string rotatorName;
                    if( this.RotatorType == "rotor")
                    {
                        rotatorName="SolarTrackerRotor";
                    }
                    else if( this.RotatorType == "gyro" )
                    {
                        rotatorName="SolarTrackerGyro";
                    }
                    else
                    {
                        string msg = "ERROR: Unknown rotatorType: " + this.RotatorType;
                        this.LCDWrite(msg);
                        throw new Exception(msg);
                    }
                    return rotatorName;
                }
            }

            private string YawPitchMultiplier
            {
                get
                {
                    if( this.RotatorType == "rotor")
                    {
                        return this.rotorYawPitchMultiplier;
                    }
                    else
                    {
                        string msg = "ERROR: Unknown rotatorType: " + this.RotatorType;
                        this.LCDWrite(msg);
                        throw new Exception(msg);
                    }
                }
            }

            private string RollMultiplier
            {
                get
                {
                    if( this.RotatorType == "rotor")
                    {
                        return this.rotorRollSolarPowerMultiplier;
                    }
                    else
                    {
                        string msg = "ERROR: Unknown rotatorType: " + this.RotatorType;
                        this.LCDWrite(msg);
                        throw new Exception(msg);
                    }
                }
            }

            private string SpeedLimit
            {
                get
                {
                    if( this.RotatorType == "rotor")
                    {
                        return this.rotorSpeedLimitRPM;
                    }
                    else
                    {
                        string msg = "ERROR: Unknown rotatorType: " + this.RotatorType;
                        this.LCDWrite(msg);
                        throw new Exception(msg);
                    }
                }
            }

            // Methods
            public SolarTracker( Program seProgram, string rotatorType = "rotor", Vector3D sunAxis = new Vector3D(0, -1, 0) )
            {
                this.seProgram = seProgram;
                this.RotatorType = rotatorType;
                this.sunAxis = sunAxis;
                this.LCD = GridTerminalSystem.GetBlockWithName("SolarTrackerLCD") as IMyTextPanel;
                this.LCD.ContentType = "TEXT_AND_IMAGE";
                this.LCDOut("SolarTracker was sucessfully intitalized\nAwailable commands:\n  SolarTrackerStart\n  SolarTrackerStop");
                GridTerminalSystem.SearchBlocksOfName(this.RotatorName, this.Rotators);
                this.SolarPanel = GridTerminalSystem.GetBlockWithName("SolarTrackerSolarPanel") as IMySolarPanel;
                if (solarPanel == null)
                    throw new Exception("ERROR: Cant get solar panel block with name \"SolarTrackerSolarPanel\"");
                this.polarCamera = GridTerminalSystem.GetBlockWithName("SolarTrackerPolarCamera") as IMyCameraBlock;
                if( this.polarCamera == null )
                    throw new Exception("ERROR: Cant get camera block with name \"SolarTrackerPolarCamera\"");
                this.NavComp = new NavigationComputer(polarCamera as IMyTerminalBlock);
            }
            
            public void ParseCommand(string command, UpdateType updateType)
            {
                switch( command )
                {
                    case "SolarTrackerStart":
                    {
                        this.status = true;
                        this.gameTicksCount = 0;
                        this.LCDOut("SolarTracker is now ranning");
                        this.TrackSun();
                        break;
                    }
                    case "SolarTrackerStop":
                    {
                        this.StopRotators();
                        this.status = false;
                        this.gameTicksCount = 0;
                        this.LCDOut("SolarTracker is stopped");
                        break;
                    }
                    default:
                    {
                        if ( this.incrTicks(updateType) >= this.operatingPeriod )
                        {
                            if ( this.status ) {
                                this.LCDOut("SolarTracker is now ranning");
                                this.TrackSun();
                            } else {
                                this.LCDOut("SolarTracker is stopped");
                            }
                            this.gameTicksCount = 0;
                        }
                        break;
                    }
                }
            }

            private void StopRotators()
            {
                if ( this.RotatorType == "rotor" ) {
                    for (int i = 0; i < this.Rotators.Count; i++)
                    {
                        IMyMotorStator rotor = this.Rotators[i] as IMyMotorStator;
                        if (rotor != null)
                        {
                            rotor.TargetVelocityRPM = 0f;
                        }
                    }
                }
            }

            private void LCDOut(string text, bool append = false)
            {
                if ( this.LCD != null )
                {
                    this.LCD.WriteText(text+"\n", append);
                }
            }

            private void LCDWrite(string text)
            {
                if ( this.LCD != null )
                {
                    this.LCD.WriteText(text+"\n");
                }
            }

            private void LCDAppend(string text)
            {
                if ( this.LCD != null )
                {
                    this.LCD.WriteText(text+"\n", true);
                }
            }

            private double Yaw()
            {
                double yaw = this.NavComp.Yaw * this.YawPitchMultiplier;
                this.previousYaw ??= yaw;
                if ( Math.Abs(this.previousYaw) < Math.Abs(yaw) ) { yaw *= -1; }
                this.previousYaw == yaw;
                return Math.Min(Math.Max(yaw, -this.SpeedLimit), this.SpeedLimit);
            }

            private double Pitch()
            {
                double pitch = this.NavComp.Pitch * this.YawPitchMultiplier;
                this.previousPitch ??= pitch;
                if ( Math.Abs(this.previousPitch) < Math.Abs(pitch) ) { pitch *= -1; }
                this.previousPitch == pitch;
                return Math.Min(Math.Max(pitch, -this.SpeedLimit), this.SpeedLimit);
            }
            
            private double Roll()
            {
                double solarOutputDiff = this.maxSolarPanelOutput - this.SolarPanel.MaxOutput;
                if ( solarOutputDiff < 0 )
                {
                    this.maxSolarPanelOutput = this.SolarPanel.MaxOutput;
                }
                double roll = (float)( this.RollDirection * Math.Abs(solarOutputDiff) ) * this.RollMultiplier;
                return Math.Min(Math.Max(roll, -this.SpeedLimit), this.SpeedLimit);
            }

            private Vector3D getNavVector() {
                this.LCDAppend(this.NavComp.ReportOrientation());
                double yaw = this.Yaw;
                double pitch = 0f;
                double roll = 0f;
                if( Math.Abs(yaw) > 0.02f )
                {
                    this.LCDAppend("Aligning Yaw");
                }
                else
                {
                    pitch = this.Pitch;
                    if( Math.Abs(pitch) > 0.02f )
                    {
                        this.LCDAppend("Aligning Pitch");
                    }
                    else
                    {
                        this.LCDAppend("Tracking Sun with Roll");
                        roll = this.Roll();
                    }
                }
                this.LCDAppend("Navigation vector:" + 
                 "\n    Yaw: " + Math.Round(yaw,   5) +
                 "\n  Pitch: " + Math.Round(pitch, 5) +
                 "\n   Roll: " + Math.Round(roll,  5));
                return new Vector3D( yaw, pitch, roll );
            }

            private void TrackSun()
            {
                Vector3D target = this.getNavVector();
                if( this.RotatorType == "rotor")
                {
                    for (int i = 0; i < this.Rotators.Count; i++)
                    {
                        IMyMotorStator rotor = this.Rotators[i] as IMyMotorStator;
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
                else
                {
                    string msg = "ERROR: Unknown rotatorType: " + this.RotatorType;
                    this.LCDWrite(msg);
                    throw new Exception(msg);
                }
            }

            private int incrTicks( UpdateType updateType )
            {
                if ((updateType & UpdateType.Update1)   != 0) { this.gameTicksCount += 1;   return this.gameTicksCount; }
                if ((updateType & UpdateType.Update10)  != 0) { this.gameTicksCount += 10;  return this.gameTicksCount; }
                if ((updateType & UpdateType.Update100) != 0) { this.gameTicksCount += 100; return this.gameTicksCount; }
            }

        }

        //------------END--------------
    }
}