//tabs=4
// --------------------------------------------------------------------------------
// TODO fill in this information for your driver, then remove this line!
//
// ASCOM Camera driver for Pictavore_4021a
//
// Description:	Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam 
//				nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam 
//				erat, sed diam voluptua. At vero eos et accusam et justo duo 
//				dolores et ea rebum. Stet clita kasd gubergren, no sea takimata 
//				sanctus est Lorem ipsum dolor sit amet.
//
// Implements:	ASCOM Camera interface version: <To be completed by driver developer>
// Author:		(XXX) Your N. Here <your@email.here>
//
// Edit Log:
//
// Date			Who	Vers	Description
// -----------	---	-----	-------------------------------------------------------
// dd-mmm-yyyy	XXX	6.0.0	Initial edit, created from ASCOM driver template
// --------------------------------------------------------------------------------
//


// This is used to define code in the template that is specific to one class implementation
// unused code canbe deleted and this definition removed.
#define Camera
#define Server

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading;
//using System.Threading.Tasks;

using ASCOM;
using ASCOM.Astrometry;
using ASCOM.Astrometry.AstroUtils;
using ASCOM.Utilities;
using ASCOM.DeviceInterface;
using System.Globalization;
using System.Collections;
using FTD2XX_NET;
using ASCOM.Pictavore_Server;


namespace ASCOM.Pictavore_4021a
{
    //
    // Your driver's DeviceID is ASCOM.Pictavore_4021a.Camera
    //
    // The Guid attribute sets the CLSID for ASCOM.Pictavore_4021a.Camera
    // The ClassInterface/None addribute prevents an empty interface called
    // _Pictavore_4021a from being created and used as the [default] interface
    //
    // TODO Replace the not implemented exceptions with code to implement the function or
    // throw the appropriate ASCOM exception.
    //

    /// <summary>
    /// ASCOM Camera Driver for Pictavore_4021a.
    /// </summary>
    [Guid("ac968d71-afea-4dcd-84c0-676773deb740")]
#if Server
    [ProgId("ASCOM.Pictavore_4021a.Camera")]
    [ServedClassName("Pictavore_4021a Camera")]
#endif
    [ClassInterface(ClassInterfaceType.None)]
#if Server
    public class Camera : ReferenceCountedObjectBase, ICameraV2
#else
    public class Camera : ICameraV2
#endif
    {
        /// <summary>
        /// ASCOM DeviceID (COM ProgID) for this driver.
        /// The DeviceID is used by ASCOM applications to load the driver at runtime.
        /// </summary>
#if !Server  
        internal static string driverID = "ASCOM.Pictavore_4021a.Camera";
        private string _ID = driverID;

#else
        private string _ID = "ASCOM.Pictavore_4021a.Camera";
#endif

        // TODO Change the descriptive string for your driver then remove this line
        /// <summary>
        /// Driver description that displays in the ASCOM Chooser.
        /// </summary>
#if !Server
        private static string driverDescription = "ASCOM Camera Driver for Pictavore_4021a.";
        private string _description = driverDescription;
#else
        private string _description =  "ASCOM Camera Driver for Pictavore_4021a";
#endif

        //internal static string comPortProfileName = "COM Port"; // Constants used for Profile persistence
        //internal static string comPortDefault = "COM1";
        internal static string traceStateProfileName = "Trace Level";
        internal static string traceStateDefault = "true";

        //internal static string comPort; // Variables to hold the currrent device configuration
        internal static bool traceState;

        /// <summary>
        /// Private variable to hold the connected state
        /// </summary>
        private bool connectedState;

        /// <summary>
        /// Private variable to hold an ASCOM Utilities object
        /// </summary>
        private Util utilities;

        /// <summary>
        /// Private variable to hold an ASCOM AstroUtilities object to provide the Range method
        /// </summary>
        private AstroUtils astroUtilities;

        /// <summary>
        /// Private variable to hold the trace logger object (creates a diagnostic log file with information that you specify)
        /// </summary>
        private TraceLogger tl;

        private bool flipX = false;         //Should image be flipped in the X direction
        private bool flipY = false;         //  or Y direction
        //bool commandBusy = false;           //Flag that a command is in process on camera

        const int rcvWaitTime = 5;          //Seconds to wait for camera to echo command

        const int maxBinX = 8;
        const int maxBinY = 8;


        public CameraStates cameraState = CameraStates.cameraIdle;
        //public FTDIio io = new FTDIio();
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Pictavore_4021a"/> class.
        /// Must be public for COM registration.
        /// </summary>
        /// 
        public Camera()
        {
            ReadProfile(); // Read device configuration from the ASCOM Profile store

#if Server
            string s_csDriverID = Marshal.GenerateProgIdForType(this.GetType());
#endif
            
            tl = new TraceLogger("", "Pictavore_4021a");
            tl.Enabled = traceState;
            tl.LogMessage("Camera", "Starting initialisation");

            connectedState = FTDIio.Connected;
            utilities = new Util(); //Initialise util object
            astroUtilities = new AstroUtils(); // Initialise astro utilities object
            //TODO: Implement your additional construction here

            tl.LogMessage("Camera", "Completed initialisation");
        }


        //
        // PUBLIC COM INTERFACE ICameraV2 IMPLEMENTATION
        //

        #region Common properties and methods.

        /// <summary>
        /// Displays the Setup Dialog form.
        /// If the user clicks the OK button to dismiss the form, then
        /// the new settings are saved, otherwise the old values are reloaded.
        /// THIS IS THE ONLY PLACE WHERE SHOWING USER INTERFACE IS ALLOWED!
        /// </summary>
        public void SetupDialog()
        {
            // consider only showing the setup dialog if not connected
            // or call a different dialog if connected
            if (IsConnected)
                System.Windows.Forms.MessageBox.Show("Already connected, just press OK");

            using (SetupDialogForm F = new SetupDialogForm())
            {
                var result = F.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    WriteProfile(); // Persist device configuration values to the ASCOM Profile store
                }
            }
        }

        public ArrayList SupportedActions
        {
            get
            {
                tl.LogMessage("SupportedActions Get", "Returning empty arraylist");
                return new ArrayList();
            }
        }

        public string Action(string actionName, string actionParameters)
        {
            throw new ASCOM.ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");
        }

        public void CommandBlind(string command, bool raw = false)
        {
            CheckConnected("CommandBlind");
            // Call CommandString and return as soon as it finishes
            this.CommandString(command, raw);
            // or
            //throw new ASCOM.MethodNotImplementedException("CommandBlind");
        }

        public bool CommandBool(string command, bool raw = false)
        {
            //CheckConnected("CommandBool");
            //string ret = CommandString(command, raw);
            // TODO decode the return string and return true or false
            // or
            throw new ASCOM.MethodNotImplementedException("CommandBool");
        }

        public string CommandString(string command, bool raw = false)
        {
            // it's a good idea to put all the low level communication with the device here,
            // then all communication calls this function
            // you need something to ensure that only one command is in progress at a time

            return FTDIio.CommandString(command, raw);
            //string response = "";

            //CheckConnected("CommandString");
            //if (!raw) command += "\r";

            //if (cameraState != CameraStates.cameraIdle || commandBusy) { };

            //commandBusy = true;
            //response = FTDIio.sendLineWaitOk2(command);
            //commandBusy = false;

            //return response;
        }


        public void CommandStringNoWait(string command, bool raw = false)
        {
            FTDIio.CommandBlind(command, raw);
            //CheckConnected("CommandStringNoWait");
            //if (!raw) command += "\r";

            //if (cameraState != CameraStates.cameraIdle || commandBusy) { };

            //commandBusy = true;
            //FTDIio.sendLine(command);
            //commandBusy = false;

            //return;
        }

        public void Dispose()
        {
            // Clean up the tracelogger and util objects
            tl.Enabled = false;
            tl.Dispose();
            tl = null;
            utilities.Dispose();
            utilities = null;
            astroUtilities.Dispose();
            astroUtilities = null;
        }

        public bool Connected
        {
            get
            {
                tl.LogMessage("Connected Get", IsConnected.ToString());
                return IsConnected;
            }
            set
            {
                tl.LogMessage("Connected Set", value.ToString());
                if (value == IsConnected)
                    return;

                if (value)
                {
                    FTDIio.CameraConnected = true;
                    connectedState = true;
                    tl.LogMessage("Connected Set", "Connecting to port ");
                }
                else
                {
                    FTDIio.CameraConnected = false;
                    connectedState = false;
                    tl.LogMessage("Connected Set", "Disconnecting from port ");
                }
            }
        }

//#if Server
//        private string driverDescription
//        {
//            get { return ServedClassName; }
//        }

//        private string driverID
//        {
//            get { return ProgId; }
//        }
//#endif


        public string Description
        {
            // TODO customise this device description
            get
            {
                string s = _description; //+ FTDIio.rnd.ToString();
                tl.LogMessage("Description Get", s);
                return s;
            }
        }

        public string DriverInfo
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                // TODO customise this driver description
                string driverInfo = "Pictavore ASCOM Server. Version: 4021a " + String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                tl.LogMessage("DriverInfo Get", driverInfo);
                return driverInfo;
            }
        }

        public string DriverVersion
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverVersion = String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                tl.LogMessage("DriverVersion Get", driverVersion);
                return driverVersion;
            }
        }

        public short InterfaceVersion
        {
            // set by the driver wizard
            get
            {
                tl.LogMessage("InterfaceVersion Get", "2");
                return Convert.ToInt16("2");
            }
        }

        public string Name
        {
            get
            {
                string name = "Pictavore 4021 Camera";
                tl.LogMessage("Name Get", name);
                return name;
            }
        }

        #endregion

        #region ICamera Implementation

        private const int ccdWidth = 2136; // Constants to define the ccd pixel dimenstions
        private const int ccdHeight = 2072;
        private const double pixelSize = 7.4; // Constant for the pixel physical dimension

        private int cameraNumX = ccdWidth; // Initialise variables to hold values required for functionality tested by Conform
        private int cameraNumY = ccdHeight;
        private int cameraStartX = 0;
        private int cameraStartY = 0;
        private DateTime exposureStart = DateTime.MinValue;
        private double cameraLastExposureDuration = 0.0;
        private bool cameraImageReady = false;
        private int[,] cameraImageArray;
        private object[,] cameraImageArrayVariant;

        private short binx = 1;
        private short biny = 1;

        private const double electronsPerADU = 0.074;
        private const double fullWellCapacity = 65535 * electronsPerADU;
        private const double minExposure = 0.8;
        private const double maxExposure = 256^4 / 1000 / 2;
        private const double exposureResolution = .001;
        private const int maxADU = 65535;
        private const string sensorName = "KAI 4021";
        private double ccdTemp = 100;
 

        public void AbortExposure()
        {
            tl.LogMessage("AbortExposure", "Not implemented");
            throw new MethodNotImplementedException("AbortExposure");
        }

        public short BayerOffsetX
        {
            get
            {
                tl.LogMessage("BayerOffsetX Get Get", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("BayerOffsetX", false);
            }
        }

        public short BayerOffsetY
        {
            get
            {
                tl.LogMessage("BayerOffsetY Get Get", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("BayerOffsetX", false);
            }
        }

        public short BinX
        {
            get
            {
                tl.LogMessage("BinX Get", binx.ToString());
                return binx;
            }
            set
            {
                tl.LogMessage("BinX Set", value.ToString());
                binx = value;
                //if (value != 1) throw new ASCOM.InvalidValueException("BinX", value.ToString(), "1"); // Only 1 is valid in this simple template
            }
        }

        public short BinY
        {
            get
            {
                tl.LogMessage("BinY Get", biny.ToString());
                return biny;
            }
            set
            {
                tl.LogMessage("BinY Set", value.ToString());
                biny = value;
                //if (value != 1) throw new ASCOM.InvalidValueException("BinY", value.ToString(), "1"); // Only 1 is valid in this simple template
            }
        }

        public double CCDTemperature
        {
            get
            {
                int temp = Convert.ToInt16(CommandString("TEMP?"));
                tl.LogMessage("CCDTemperature Get Get", temp.ToString());
                return temp;
                //throw new ASCOM.PropertyNotImplementedException("CCDTemperature", false);
            }
        }

        public CameraStates CameraState
        {
            get
            {
                tl.LogMessage("CameraState Get", CameraStates.cameraIdle.ToString());
                return cameraState;
            }
        }

        public int CameraXSize
        {
            get
            {
                cameraNumX = Convert.ToInt16(CommandString("XFRAME @ ."));
                tl.LogMessage("CameraXSize Get", cameraNumX.ToString());
                return cameraNumX;
            }
        }

        public int CameraYSize
        {
            get
            {
                cameraNumY = Convert.ToInt16(CommandString("YFRAME @ ."));
                tl.LogMessage("CameraYSize Get", cameraNumY.ToString());
                return cameraNumY;
            }
        }

        public bool CanAbortExposure
        {
            get
            {
                tl.LogMessage("CanAbortExposure Get", false.ToString());
                return false;
            }
        }

        public bool CanAsymmetricBin
        {
            get
            {
                tl.LogMessage("CanAsymmetricBin Get", false.ToString());
                return true;
            }
        }

        public bool CanFastReadout
        {
            get
            {
                tl.LogMessage("CanFastReadout Get", false.ToString());
                return false;
            }
        }

        public bool CanGetCoolerPower
        {
            get
            {
                tl.LogMessage("CanGetCoolerPower Get", true.ToString());
                return true;
            }
        }

        public bool CanPulseGuide
        {
            get
            {
                tl.LogMessage("CanPulseGuide Get", false.ToString());
                return false;
            }
        }

        public bool CanSetCCDTemperature
        {
            get
            {
                tl.LogMessage("CanSetCCDTemperature Get", true.ToString());
                return true;
            }
        }

        public bool CanStopExposure
        {
            get
            {
                tl.LogMessage("CanStopExposure Get", false.ToString());
                return false;
            }
        }

        public bool CoolerOn
        {
            get
            {
                bool response;
                

                int tecValue = Convert.ToInt16(CommandString("TECSET @ ."));

                if (tecValue == 0) response = false;
                else response = true;

                tl.LogMessage("CoolerOn Get Get", response.ToString());
                return response;
            }
            set
            {
                tl.LogMessage("CoolerOn Set Get", value.ToString());
                if (value) CommandString("TECON");
                else CommandString("TECOFF");
            }
        }

        public double CoolerPower
        {
            get
            {
                double tecValue = Convert.ToDouble(CommandString("TECSET @ .")) / 1024 * 100;

                tl.LogMessage("CoolerPower Get Get", tecValue.ToString());
                return tecValue;
            }
        }

        public double ElectronsPerADU
        {
            get
            {
                tl.LogMessage("ElectronsPerADU Get Get", electronsPerADU.ToString());
                return electronsPerADU;
                //throw new ASCOM.PropertyNotImplementedException("ElectronsPerADU", false);
            }
        }

        public double ExposureMax
        {
            get
            {
                tl.LogMessage("ExposureMax Get Get", maxExposure.ToString());
                return maxExposure;
            }                        //TODO check with George on this (int?)            }
        }

        public double ExposureMin
        {
            get
            {
                tl.LogMessage("ExposureMin Get", minExposure.ToString());
                return minExposure;
                //throw new ASCOM.PropertyNotImplementedException("ExposureMin", false);
            }
        }

        public double ExposureResolution
        {
            get
            {
                tl.LogMessage("ExposureResolution Get", exposureResolution.ToString());
                return exposureResolution;
            }
        }

        public bool FastReadout
        {
            get
            {
                tl.LogMessage("FastReadout Get", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("FastReadout", false);
            }
            set
            {
                tl.LogMessage("FastReadout Set", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("FastReadout", true);
            }
        }

        public double FullWellCapacity
        {
            get
            {
                tl.LogMessage("FullWellCapacity Get", fullWellCapacity.ToString());
                return fullWellCapacity;
                //throw new ASCOM.PropertyNotImplementedException("FullWellCapacity", false);
            }
        }

        public short Gain
        {
            get
            {
                tl.LogMessage("Gain Get", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("Gain", false);
            }
            set
            {
                tl.LogMessage("Gain Set", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("Gain", true);
            }
        }

        public short GainMax
        {
            get
            {
                tl.LogMessage("GainMax Get", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("GainMax", false);
            }
        }

        public short GainMin
        {
            get
            {
                tl.LogMessage("GainMin Get", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("GainMin", true);
            }
        }

        public ArrayList Gains
        {
            get
            {
                tl.LogMessage("Gains Get", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("Gains", true);
            }
        }

        public bool HasShutter
        {
            get
            {
                tl.LogMessage("HasShutter Get", false.ToString());
                return false;
            }
        }

        public double HeatSinkTemperature
        {
            get
            {
                tl.LogMessage("HeatSinkTemperature Get", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("HeatSinkTemperature", false);
            }
        }

        public object ImageArray
        {
            get
            {
                if (!cameraImageReady)
                {
                    tl.LogMessage("ImageArray Get", "Throwing InvalidOperationException because of a call to ImageArray before the first image has been taken!");
                    throw new ASCOM.InvalidOperationException("Call to ImageArray before the first image has been taken!");
                }

                tl.LogMessage("ImageArray Get", "Returned cameraImageArray");
                return cameraImageArray;
            }
        }

        public object ImageArrayVariant
        {
            get
            {
                if (!cameraImageReady)
                {
                    tl.LogMessage("ImageArrayVariant Get", "Throwing InvalidOperationException because of a call to ImageArrayVariant before the first image has been taken!");
                    throw new ASCOM.InvalidOperationException("Call to ImageArrayVariant before the first image has been taken!");
                }
                cameraImageArrayVariant = new object[cameraNumX, cameraNumY];
                for (int i = 0; i < cameraImageArray.GetLength(1); i++)
                {
                    for (int j = 0; j < cameraImageArray.GetLength(0); j++)
                    {
                        cameraImageArrayVariant[j, i] = cameraImageArray[j, i];
                    }

                }

                tl.LogMessage("ImageArrayVariant Get", "Returned cameraImageArrayVariant");
                return cameraImageArrayVariant;
            }
        }

        public bool ImageReady
        {
            get
            {
                tl.LogMessage("ImageReady Get", cameraImageReady.ToString());
                return cameraImageReady;
            }
        }

        public bool IsPulseGuiding
        {
            get
            {
                tl.LogMessage("IsPulseGuiding Get", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("IsPulseGuiding", false);
            }
        }

        public double LastExposureDuration
        {
            get
            {
                if (!cameraImageReady)
                {
                    tl.LogMessage("LastExposureDuration Get", "Throwing InvalidOperationException because of a call to LastExposureDuration before the first image has been taken!");
                    throw new ASCOM.InvalidOperationException("Call to LastExposureDuration before the first image has been taken!");
                }
                tl.LogMessage("LastExposureDuration Get", cameraLastExposureDuration.ToString());
                return cameraLastExposureDuration;
            }
        }

        public string LastExposureStartTime
        {
            get
            {
                if (!cameraImageReady)
                {
                    tl.LogMessage("LastExposureStartTime Get", "Throwing InvalidOperationException because of a call to LastExposureStartTime before the first image has been taken!");
                    throw new ASCOM.InvalidOperationException("Call to LastExposureStartTime before the first image has been taken!");
                }
                string exposureStartString = exposureStart.ToString("yyyy-MM-ddTHH:mm:ss");
                tl.LogMessage("LastExposureStartTime Get", exposureStartString.ToString());
                return exposureStartString;
            }
        }

        public int MaxADU
        {
            get
            {
                tl.LogMessage("MaxADU Get", maxADU.ToString());
                return maxADU;
            }
        }

        public short MaxBinX
        {
            get
            {
                tl.LogMessage("MaxBinX Get", maxBinX.ToString());
                return maxBinX;
            }
        }

        public short MaxBinY
        {
            get
            {
                tl.LogMessage("MaxBinY Get", maxBinY.ToString());
                return maxBinY;
            }
        }

        public int NumX
        {
            get
            {
                tl.LogMessage("NumX Get", cameraNumX.ToString());
                return cameraNumX;
            }
            set
            {
                cameraNumX = value;
                tl.LogMessage("NumX set", value.ToString());
            }
        }

        public int NumY
        {
            get
            {
                tl.LogMessage("NumY Get", cameraNumY.ToString());
                return cameraNumY;
            }
            set
            {
                cameraNumY = value;
                tl.LogMessage("NumY set", value.ToString());
            }
        }

        public short PercentCompleted
        {
            get
            {
                tl.LogMessage("PercentCompleted Get", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("PercentCompleted", false);
            }
        }

        public double PixelSizeX
        {
            get
            {
                tl.LogMessage("PixelSizeX Get", pixelSize.ToString());
                return pixelSize;
            }
        }

        public double PixelSizeY
        {
            get
            {
                tl.LogMessage("PixelSizeY Get", pixelSize.ToString());
                return pixelSize;
            }
        }

        public void PulseGuide(GuideDirections Direction, int Duration)
        {
            tl.LogMessage("PulseGuide", "Not implemented");
            throw new ASCOM.MethodNotImplementedException("PulseGuide");
        }

        public short ReadoutMode
        {
            get
            {
                tl.LogMessage("ReadoutMode Get", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("ReadoutMode", false);
            }
            set
            {
                tl.LogMessage("ReadoutMode Set", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("ReadoutMode", true);
            }
        }

        public ArrayList ReadoutModes
        {
            get
            {
                tl.LogMessage("ReadoutModes Get", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("ReadoutModes", false);
            }
        }

        public string SensorName
        {
            get
            {
                tl.LogMessage("SensorName Get", sensorName);
                return sensorName;
                //throw new ASCOM.PropertyNotImplementedException("SensorName", false);
            }
        }

        public SensorType SensorType
        {
            get
            {
                tl.LogMessage("SensorType Get", "0");
                return 0;                               //0 = monochrome
                //throw new ASCOM.PropertyNotImplementedException("SensorType", false);
            }
        }

        public double SetCCDTemperature
        {
            get
            {
                tl.LogMessage("SetCCDTemperature Get", ccdTemp.ToString());
                return ccdTemp;
                //throw new ASCOM.PropertyNotImplementedException("SetCCDTemperature", false);
            }
            set
            {
                tl.LogMessage("SetCCDTemperature Set", value.ToString());

                int val = (int)value;

                FTDIio.CommandString(val.ToString() + " TEC");

                //throw new ASCOM.PropertyNotImplementedException("SetCCDTemperature", true);
            }
        }

        private void SendExpose(string exposeString, double Duration)
        {
            Debug.WriteLine("enter SendExpose " + DateTime.Now);
            CommandStringNoWait(exposeString);                      //Send expose command to camera
            FTDIio.waitForOk((int)(Duration));                          //Wait for expose time and then for 'Ok'
            Debug.WriteLine("exit SendExpose " + DateTime.Now);

            cameraImageArray = (int[,])getImage();                  //Download the image
        }

        public void StartExposure(double Duration, bool Light)
        {
            double sec = Math.Floor(Duration);                      //Camera firmware requires exposure time
            double msec = Math.Floor((Duration - sec) * 1000);      // to be in seconds and mSec
            string cmd;                                             //EXPOSE or DEXPOSE

            
            cameraImageReady = false;
            cameraState = CameraStates.cameraExposing;

            if (Duration < 0.0) throw new InvalidValueException("StartExposure", Duration.ToString(), "0.0 upwards");
            if (cameraNumX > ccdWidth) throw new InvalidValueException("StartExposure", cameraNumX.ToString(), ccdWidth.ToString());
            if (cameraNumY > ccdHeight) throw new InvalidValueException("StartExposure", cameraNumY.ToString(), ccdHeight.ToString());
            if (cameraStartX > ccdWidth) throw new InvalidValueException("StartExposure", cameraStartX.ToString(), ccdWidth.ToString());
            if (cameraStartY > ccdHeight) throw new InvalidValueException("StartExposure", cameraStartY.ToString(), ccdHeight.ToString());

            cameraLastExposureDuration = Duration;
            exposureStart = DateTime.Now;

            tl.LogMessage("StartExposure", Duration.ToString() + " " + Light.ToString());

            CommandString(sec.ToString() + " XSEC !");      //Send the camera the exposure time
            CommandString(msec.ToString() + " XMSEC ! ");

            if (Light) cmd = "EXPOSE";
            else cmd = "DEXPOSE";                           //Dark expose (closed shutter)

            //Start a separate thread to do exposure and image capture so this can return to client
            // to show exposure time display
            FTDIio.GrabImageLock = true;
            Thread t = new Thread(() => SendExpose(cmd, Duration));
            t.Start();
            FTDIio.GrabImageLock = false;
        }

        public int StartX
        {
            get
            {
                tl.LogMessage("StartX Get", cameraStartX.ToString());
                return cameraStartX;
            }
            set
            {
                cameraStartX = value;
                tl.LogMessage("StartX Set", value.ToString());
            }
        }

        public int StartY
        {
            get
            {
                tl.LogMessage("StartY Get", cameraStartY.ToString());
                return cameraStartY;
            }
            set
            {
                cameraStartY = value;
                tl.LogMessage("StartY set", value.ToString());
            }
        }

        public void StopExposure()
        {
            tl.LogMessage("StopExposure", "Not implemented");
            throw new MethodNotImplementedException("StopExposure");
        }

        #endregion

        #region Private properties and methods
        // here are some useful properties and methods that can be used as required
        // to help with driver development

        private object getImage()
        {

            uint xbytesize = (uint)(cameraNumX * 2);               //Number of 8 bit (bytes) per row

            uint[,] image = new uint[cameraNumX, cameraNumY];     //Container for the retrieved image
            byte[] rowString = new byte[cameraNumX];               //Container for a single row from image

            Debug.WriteLine("enter download" + DateTime.Now);
            cameraImageReady = false;
            cameraState = CameraStates.cameraDownload;
            CommandString("PON");
            CommandString(String.Concat(cameraNumX.ToString(), " XSIZE !"));
            CommandString(String.Concat(cameraNumY.ToString(), " YSIZE !"));
            CommandString(String.Concat(binx.ToString(), " XBIN !"));
            CommandString(String.Concat(biny.ToString(), " YBIN !"));
            CommandString(String.Concat(cameraStartX.ToString(), " XOFFS !"));
            CommandString(String.Concat(cameraStartY.ToString(), " YOFFS !"));

            //Thread t = new Thread(() => doGRIMG());
            //t.Start();
            //t.Join();

            lock (FTDIio.cmdLock)
            {
                FTDIio.CommandBlind("GRIMG");                     //Requested image but don't wait for any response but echo

                int row, col, bytecol, y, x;
                for (row = 0; row < cameraNumY; row++)              //For each row
                {
                    rowString = FTDIio.readio(xbytesize);               //  read a row
                    y = flipY ? cameraNumY - row - 1 : row;         //  vertical flip data?
                    for (col = 0; col < cameraNumX; col++)          //Now translate 2x8 bit to 16 bit (Big Endian)
                    {
                        bytecol = col * 2;
                        x = flipX ? -col - 1 : col;               //  mirror data?
                        image[x, y] = (uint)((short)rowString[bytecol] << 8 | (byte)rowString[bytecol + 1]);
                    }
                    if (row % 30 == 0) Debug.WriteLine("row: " + row);
                }

                FTDIio.waitForOk();
            }
            //FTDIio.GrabImageLock = false;
            cameraImageReady = true;
            cameraState = CameraStates.cameraIdle;
            Debug.WriteLine("exit download" + DateTime.Now);


            return (object)image;
        }//end getImage(...


        private object doGRIMG()
        {
            uint xbytesize = (uint)(cameraNumX * 2);               //Number of 8 bit (bytes) per row

            uint[,] image = new uint[cameraNumX, cameraNumY];     //Container for the retrieved image
            byte[] rowString = new byte[cameraNumX];               //Container for a single row from image

            int row, col, bytecol, y, x;
            for (row = 0; row < cameraNumY; row++)              //For each row
            {
                rowString = FTDIio.readio(xbytesize);               //  read a row
                y = flipY ? cameraNumY - row - 1 : row;         //  vertical flip data?
                for (col = 0; col < cameraNumX; col++)          //Now translate 2x8 bit to 16 bit (Big Endian)
                {
                    bytecol = col * 2;
                    x = flipX ? -col - 1 : col;               //  mirror data?
                    image[x, y] = (uint)((short)rowString[bytecol] << 8 | (byte)rowString[bytecol + 1]);
                }
                if (row % 30 == 0) Debug.WriteLine("row: " + row);
            }
            cameraImageReady = true;
            cameraState = CameraStates.cameraIdle;
            Debug.WriteLine("exit download" + DateTime.Now);
            return image;
        }

#if !Server
        #region ASCOM Registration

        // Register or unregister driver for ASCOM. This is harmless if already
        // registered or unregistered. 
        //
        /// <summary>
        /// Register or unregister the driver with the ASCOM Platform.
        /// This is harmless if the driver is already registered/unregistered.
        /// </summary>
        /// <param name="bRegister">If <c>true</c>, registers the driver, otherwise unregisters it.</param>
        private static void RegUnregASCOM(bool bRegister)
        {
            using (var P = new ASCOM.Utilities.Profile())
            {
                P.DeviceType = "Camera";
                if (bRegister)
                {
                    P.Register(driverID, driverDescription);
                }
                else
                {
                    P.Unregister(driverID);
                }
            }
        }

        /// <summary>
        /// This function registers the driver with the ASCOM Chooser and
        /// is called automatically whenever this class is registered for COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is successfully built.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During setup, when the installer registers the assembly for COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually register a driver with ASCOM.
        /// </remarks>
        [ComRegisterFunction]
        public static void RegisterASCOM(Type t)
        {
            RegUnregASCOM(true);
        }

        /// <summary>
        /// This function unregisters the driver from the ASCOM Chooser and
        /// is called automatically whenever this class is unregistered from COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is cleaned or prior to rebuilding.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During uninstall, when the installer unregisters the assembly from COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually unregister a driver from ASCOM.
        /// </remarks>
        [ComUnregisterFunction]
        public static void UnregisterASCOM(Type t)
        {
            RegUnregASCOM(false);
        }

        #endregion
#endif

        /// <summary>
        /// Returns true if there is a valid connection to the driver hardware
        /// </summary>
        private bool IsConnected
        {
            get
            {
                // TODO check that the driver hardware connection exists and is connected to the hardware
                return FTDIio.CameraConnected;
            }
        }

        /// <summary>
        /// Use this function to throw an exception if we aren't connected to the hardware
        /// </summary>
        /// <param name="message"></param>
        private void CheckConnected(string message)
        {
            if (!IsConnected)
            {
                throw new ASCOM.NotConnectedException(message);
            }
        }

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        internal void ReadProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Camera";
                traceState = Convert.ToBoolean(driverProfile.GetValue(_ID, traceStateProfileName, string.Empty, traceStateDefault));
                //comPort = driverProfile.GetValue(driverID, comPortProfileName, string.Empty, comPortDefault);
            }
        }

        /// <summary>
        /// Write the device configuration to the  ASCOM  Profile store
        /// </summary>
        internal void WriteProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Camera";
                driverProfile.WriteValue(_ID, traceStateProfileName, traceState.ToString());
                //driverProfile.WriteValue(driverID, comPortProfileName, comPort.ToString());
            }
        }

        #endregion

    }
}

