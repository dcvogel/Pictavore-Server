//
// ASCOM.Pictavore_Server Local COM Server
//
// This is the core of a managed COM Local Server, capable of serving
// multiple instances of multiple interfaces, within a single
// executable. This implementes the equivalent functionality of VB6
// which has been extensively used in ASCOM for drivers that provide
// multiple interfaces to multiple clients (e.g. Meade Telescope
// and Focuser) as well as hubs (e.g., POTH).
//
// Written by: Robert B. Denny (Version 1.0.1, 29-May-2007)
// Modified by Chris Rowland and Peter Simpson to allow use with multiple devices of the same type March 2011
//
//
using System;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using System.Collections;
using System.Runtime.InteropServices;
using System.Reflection;
using ASCOM.Utilities;
using Microsoft.Win32;
using System.Text;
using System.Threading;
using System.Security.Principal;
using System.Diagnostics;
using ASCOM;
using ASCOM.Pictavore_Server;
using FTD2XX_NET;
using System.Linq;

//namespace ASCOM.Pictavore_Server
namespace ASCOM.Pictavore_4021a
{
    public static class Server
    {

        #region Access to kernel32.dll, user32.dll, and ole32.dll functions
        [Flags]
        enum CLSCTX : uint
        {
            CLSCTX_INPROC_SERVER = 0x1,
            CLSCTX_INPROC_HANDLER = 0x2,
            CLSCTX_LOCAL_SERVER = 0x4,
            CLSCTX_INPROC_SERVER16 = 0x8,
            CLSCTX_REMOTE_SERVER = 0x10,
            CLSCTX_INPROC_HANDLER16 = 0x20,
            CLSCTX_RESERVED1 = 0x40,
            CLSCTX_RESERVED2 = 0x80,
            CLSCTX_RESERVED3 = 0x100,
            CLSCTX_RESERVED4 = 0x200,
            CLSCTX_NO_CODE_DOWNLOAD = 0x400,
            CLSCTX_RESERVED5 = 0x800,
            CLSCTX_NO_CUSTOM_MARSHAL = 0x1000,
            CLSCTX_ENABLE_CODE_DOWNLOAD = 0x2000,
            CLSCTX_NO_FAILURE_LOG = 0x4000,
            CLSCTX_DISABLE_AAA = 0x8000,
            CLSCTX_ENABLE_AAA = 0x10000,
            CLSCTX_FROM_DEFAULT_CONTEXT = 0x20000,
            CLSCTX_INPROC = CLSCTX_INPROC_SERVER | CLSCTX_INPROC_HANDLER,
            CLSCTX_SERVER = CLSCTX_INPROC_SERVER | CLSCTX_LOCAL_SERVER | CLSCTX_REMOTE_SERVER,
            CLSCTX_ALL = CLSCTX_SERVER | CLSCTX_INPROC_HANDLER
        }

        [Flags]
        enum COINIT : uint
        {
            /// Initializes the thread for multi-threaded object concurrency.
            COINIT_MULTITHREADED = 0x0,
            /// Initializes the thread for apartment-threaded object concurrency. 
            COINIT_APARTMENTTHREADED = 0x2,
            /// Disables DDE for Ole1 support.
            COINIT_DISABLE_OLE1DDE = 0x4,
            /// Trades memory for speed.
            COINIT_SPEED_OVER_MEMORY = 0x8
        }

        [Flags]
        enum REGCLS : uint
        {
            REGCLS_SINGLEUSE = 0,
            REGCLS_MULTIPLEUSE = 1,
            REGCLS_MULTI_SEPARATE = 2,
            REGCLS_SUSPENDED = 4,
            REGCLS_SURROGATE = 8
        }


        // CoInitializeEx() can be used to set the apartment model
        // of individual threads.
        [DllImport("ole32.dll")]
        static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

        // CoUninitialize() is used to uninitialize a COM thread.
        [DllImport("ole32.dll")]
        static extern void CoUninitialize();

        // PostThreadMessage() allows us to post a Windows Message to
        // a specific thread (identified by its thread id).
        // We will need this API to post a WM_QUIT message to the main 
        // thread in order to terminate this application.
        [DllImport("user32.dll")]
        static extern bool PostThreadMessage(uint idThread, uint Msg, UIntPtr wParam,
            IntPtr lParam);

        // GetCurrentThreadId() allows us to obtain the thread id of the
        // calling thread. This allows us to post the WM_QUIT message to
        // the main thread.
        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();
        #endregion

        #region Private Data
        private static int objsInUse;						// Keeps a count on the total number of objects alive.
        private static int serverLocks;						// Keeps a lock count on this application.
        private static frmMain s_MainForm = null;				// Reference to our main form
        private static ArrayList s_ComObjectAssys;				// Dynamically loaded assemblies containing served COM objects
        private static ArrayList s_ComObjectTypes;				// Served COM object types
        private static ArrayList s_ClassFactories;				// Served COM object class factories
        private static string s_appId = "{74ffb3ab-7266-45ba-bec4-e471de04d54b}";	// Our AppId
        private static readonly Object lockObject = new object();
        #endregion

        // This property returns the main thread's id.
        public static uint MainThreadId { get; private set; }	// Stores the main thread's thread id.

        // Used to tell if started by COM or manually
        public static bool StartedByCOM { get; private set; }   // True if server started by COM (-embedding)


        #region Server Lock, Object Counting, and AutoQuit on COM startup
        // Returns the total number of objects alive currently.
        public static int ObjectsCount
        {
            get
            {
                lock (lockObject)
                {
                    return objsInUse;
                }
            }
        }

        // This method performs a thread-safe incrementation of the objects count.
        public static int CountObject()
        {
            // Increment the global count of objects.
            return Interlocked.Increment(ref objsInUse);
        }

        // This method performs a thread-safe decrementation the objects count.
        public static int UncountObject()
        {
            // Decrement the global count of objects.
            return Interlocked.Decrement(ref objsInUse);
        }

        // Returns the current server lock count.
        public static int ServerLockCount
        {
            get
            {
                lock (lockObject)
                {
                    return serverLocks;
                }
            }
        }

        // This method performs a thread-safe incrementation the 
        // server lock count.
        public static int CountLock()
        {
            // Increment the global lock count of this server.
            return Interlocked.Increment(ref serverLocks);
        }

        // This method performs a thread-safe decrementation the 
        // server lock count.
        public static int UncountLock()
        {
            // Decrement the global lock count of this server.
            return Interlocked.Decrement(ref serverLocks);
        }

        // AttemptToTerminateServer() will check to see if the objects count and the server 
        // lock count have both dropped to zero.
        //
        // If so, and if we were started by COM, we post a WM_QUIT message to the main thread's
        // message loop. This will cause the message loop to exit and hence the termination 
        // of this application. If hand-started, then just trace that it WOULD exit now.
        //
        public static void ExitIf()
        {
            lock (lockObject)
            {
                if ((ObjectsCount <= 0) && (ServerLockCount <= 0))
                {
                    if (StartedByCOM)
                    {
                        UIntPtr wParam = new UIntPtr(0);
                        IntPtr lParam = new IntPtr(0);
                        PostThreadMessage(MainThreadId, 0x0012, wParam, lParam);
                    }
                }
            }
        }
        #endregion

        // -----------------
        // PRIVATE FUNCTIONS
        // -----------------

        #region Dynamic Driver Assembly Loader
        //
        // Load the assemblies that contain the classes that we will serve
        // via COM. These will be located in the same folder as
        // our executable.
        //
        private static bool LoadComObjectAssemblies()
        {
            s_ComObjectAssys = new ArrayList();
            s_ComObjectTypes = new ArrayList();

            // put everything into one folder, the same as the server.
            string assyPath = Assembly.GetEntryAssembly().Location;
            assyPath = Path.GetDirectoryName(assyPath);

            DirectoryInfo d = new DirectoryInfo(assyPath);
            foreach (FileInfo fi in d.GetFiles("*.dll"))
            {
                string aPath = fi.FullName;
                //MessageBox.Show(aPath);
                //
                // First try to load the assembly and get the types for
                // the class and the class factory. If this doesn't work ????
                //
                try
                {
                    //MessageBox.Show("apath: " + aPath);
                    Assembly so = Assembly.LoadFrom(aPath);
                    //PWGS Get the types in the assembly
                    //MessageBox.Show("so: " + so.ToString());
                    //MessageBox.Show("apath: " + aPath);
                    Type[] types = so.GetTypes();
                    foreach (Type type in types)
                    {
                        // PWGS Now checks the type rather than the assembly
                        // Check to see if the type has the ServedClassName attribute, only use it if it does.
                        MemberInfo info = type;

                        object[] attrbutes = info.GetCustomAttributes(typeof(ServedClassNameAttribute), false);
                        if (attrbutes.Length > 0)
                        {
                            //MessageBox.Show("***Adding Type: " + type.Name + " " + type.FullName);
                            s_ComObjectTypes.Add(type); //PWGS - much simpler
                            s_ComObjectAssys.Add(so);
                        }
                    }
                }
                catch (BadImageFormatException)
                {
                    // Probably an attempt to load a Win32 DLL (i.e. not a .net assembly)
                    // Just swallow the exception and continue to the next item.
                    MessageBox.Show("error" + aPath);

                    continue;
                }
                catch (Exception e)
                {
                    MessageBox.Show("Failed to load served COM class assembly " + fi.Name + " - " + e.Message,
                        "Pictavore_Server", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                    return false;
                }

            }
            return true;
        }
        #endregion

        #region COM Registration and Unregistration
        //
        // Test if running elevated
        //
        private static bool IsAdministrator
        {
            get
            {
                WindowsIdentity i = WindowsIdentity.GetCurrent();
                WindowsPrincipal p = new WindowsPrincipal(i);
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        //
        // Elevate by re-running ourselves with elevation dialog
        //
        private static void ElevateSelf(string arg)
        {
            ProcessStartInfo si = new ProcessStartInfo();
            si.Arguments = arg;
            si.WorkingDirectory = Environment.CurrentDirectory;
            si.FileName = Application.ExecutablePath;
            si.Verb = "runas";
            try { Process.Start(si); }
            catch (System.ComponentModel.Win32Exception)
            {
                MessageBox.Show("The Pictavore_Server was not " + (arg == "/register" ? "registered" : "unregistered") +
                    " because you did not allow it.", "Pictavore_Server", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Pictavore_Server", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            }
            return;
        }

        //
        // Do everything to register this for COM. Never use REGASM on
        // this exe assembly! It would create InProcServer32 entries 
        // which would prevent proper activation!
        //
        // Using the list of COM object types generated during dynamic
        // assembly loading, it registers each one for COM as served by our
        // exe/local server, as well as registering it for ASCOM. It also
        // adds DCOM info for the local server itself, so it can be activated
        // via an outboiud connection from TheSky.
        //
        private static void RegisterObjects()
        {
            if (!IsAdministrator)
            {
                ElevateSelf("/register");
                return;
            }
            //MessageBox.Show("after elevate");
            //
            // If reached here, we're running elevated
            //

            Assembly assy = Assembly.GetExecutingAssembly();
            Attribute attr = Attribute.GetCustomAttribute(assy, typeof(AssemblyTitleAttribute));
            string assyTitle = ((AssemblyTitleAttribute)attr).Title;
            attr = Attribute.GetCustomAttribute(assy, typeof(AssemblyDescriptionAttribute));
            string assyDescription = ((AssemblyDescriptionAttribute)attr).Description;
            //MessageBox.Show("assyDescription: " + assyDescription);
            //
            // Local server's DCOM/AppID information
            //
            try
            {
                //
                // HKCR\APPID\appid
                //
                using (RegistryKey key = Registry.ClassesRoot.CreateSubKey("APPID\\" + s_appId))
                {
                    key.SetValue(null, assyDescription);
                    key.SetValue("AppID", s_appId);
                    key.SetValue("AuthenticationLevel", 1, RegistryValueKind.DWord);
                }
                //
                // HKCR\APPID\exename.ext
                //
                using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(string.Format("APPID\\{0}",
                            Application.ExecutablePath.Substring(Application.ExecutablePath.LastIndexOf('\\') + 1))))
                {
                    key.SetValue("AppID", s_appId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while registering the server:\n" + ex.ToString(),
                        "Pictavore_Server", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }
            finally
            {
                //MessageBox.Show("finally");
            }

            //
            // For each of the driver assemblies
            //
            foreach (Type type in s_ComObjectTypes)
            {
                bool bFail = false;
                try
                {
                    //
                    // HKCR\CLSID\clsid
                    //
                    //MessageBox.Show("before marshal");
                    string clsid = Marshal.GenerateGuidForType(type).ToString("B");
                    string progid = Marshal.GenerateProgIdForType(type);
                    //PWGS Generate device type from the Class name
                    string deviceType = type.Name;
                    //MessageBox.Show(clsid + "\n" + progid + "\n" + deviceType);

                    using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(string.Format("CLSID\\{0}", clsid)))
                    {
                        key.SetValue(null, progid);						// Could be assyTitle/Desc??, but .NET components show ProgId here
                        key.SetValue("AppId", s_appId);
                        using (RegistryKey key2 = key.CreateSubKey("Implemented Categories"))
                        {
                            key2.CreateSubKey("{62C8FE65-4EBB-45e7-B440-6E39B2CDBF29}");
                        }
                        using (RegistryKey key2 = key.CreateSubKey("ProgId"))
                        {
                            key2.SetValue(null, progid);
                        }
                        key.CreateSubKey("Programmable");
                        using (RegistryKey key2 = key.CreateSubKey("LocalServer32"))
                        {
                            key2.SetValue(null, Application.ExecutablePath);
                        }
                    }
                    //
                    // HKCR\CLSID\progid
                    //
                    using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(progid))
                    {
                        key.SetValue(null, assyTitle);
                        using (RegistryKey key2 = key.CreateSubKey("CLSID"))
                        {
                            key2.SetValue(null, clsid);
                        }
                    }
                    //
                    // ASCOM 
                    //
                    assy = type.Assembly;

                    // Pull the display name from the ServedClassName attribute.
                    attr = Attribute.GetCustomAttribute(type, typeof(ServedClassNameAttribute)); //PWGS Changed to search type for attribute rather than assembly
                    string chooserName = ((ServedClassNameAttribute)attr).DisplayName ?? "MultiServer";
                    using (var P = new ASCOM.Utilities.Profile())
                    {
                        P.DeviceType = deviceType;
                        P.Register(progid, chooserName);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error while registering the server:\n" + ex.ToString(),
                            "Pictavore_Server", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                    bFail = true;
                }
                finally
                {
                }
                if (bFail) break;
            }
        }

        //
        // Remove all traces of this from the registry. 
        //
        // **TODO** If the above does AppID/DCOM stuff, this would have
        // to remove that stuff too.
        //
        private static void UnregisterObjects()
        {
            if (!IsAdministrator)
            {
                ElevateSelf("/unregister");
                return;
            }

            //
            // Local server's DCOM/AppID information
            //
            Registry.ClassesRoot.DeleteSubKey(string.Format("APPID\\{0}", s_appId), false);
            Registry.ClassesRoot.DeleteSubKey(string.Format("APPID\\{0}",
                    Application.ExecutablePath.Substring(Application.ExecutablePath.LastIndexOf('\\') + 1)), false);

            //
            // For each of the driver assemblies
            //
            foreach (Type type in s_ComObjectTypes)
            {
                string clsid = Marshal.GenerateGuidForType(type).ToString("B");
                string progid = Marshal.GenerateProgIdForType(type);
                string deviceType = type.Name;
                //
                // Best efforts
                //
                //
                // HKCR\progid
                //
                Registry.ClassesRoot.DeleteSubKey(String.Format("{0}\\CLSID", progid), false);
                Registry.ClassesRoot.DeleteSubKey(progid, false);
                //
                // HKCR\CLSID\clsid
                //
                Registry.ClassesRoot.DeleteSubKey(String.Format("CLSID\\{0}\\Implemented Categories\\{{62C8FE65-4EBB-45e7-B440-6E39B2CDBF29}}", clsid), false);
                Registry.ClassesRoot.DeleteSubKey(String.Format("CLSID\\{0}\\Implemented Categories", clsid), false);
                Registry.ClassesRoot.DeleteSubKey(String.Format("CLSID\\{0}\\ProgId", clsid), false);
                Registry.ClassesRoot.DeleteSubKey(String.Format("CLSID\\{0}\\LocalServer32", clsid), false);
                Registry.ClassesRoot.DeleteSubKey(String.Format("CLSID\\{0}\\Programmable", clsid), false);
                Registry.ClassesRoot.DeleteSubKey(String.Format("CLSID\\{0}", clsid), false);
                try
                {
                    //
                    // ASCOM
                    //
                    using (var P = new ASCOM.Utilities.Profile())
                    {
                        P.DeviceType = deviceType;
                        P.Unregister(progid);
                    }
                }
                catch (Exception) { }
            }
        }
        #endregion

        #region Class Factory Support
        //
        // On startup, we register the class factories of the COM objects
        // that we serve. This requires the class facgtory name to be
        // equal to the served class name + "ClassFactory".
        //
        private static bool RegisterClassFactories()
        {
            s_ClassFactories = new ArrayList();
            foreach (Type type in s_ComObjectTypes)
            {
                ClassFactory factory = new ClassFactory(type);					// Use default context & flags
                s_ClassFactories.Add(factory);
                if (!factory.RegisterClassObject())
                {
                    MessageBox.Show("Failed to register class factory for " + type.Name,
                        "Pictavore_Server", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                    return false;
                }
            }
            ClassFactory.ResumeClassObjects();									// Served objects now go live
            return true;
        }

        private static void RevokeClassFactories()
        {
            ClassFactory.SuspendClassObjects();									// Prevent race conditions
            foreach (ClassFactory factory in s_ClassFactories)
                factory.RevokeClassObject();
        }
        #endregion

        #region Command Line Arguments
        //
        // ProcessArguments() will process the command-line arguments
        // If the return value is true, we carry on and start this application.
        // If the return value is false, we terminate this application immediately.
        //
        private static bool ProcessArguments(string[] args)
        {
            bool bRet = true;

            //
            //**TODO** -Embedding is "ActiveX start". Prohibit non_AX starting?
            //
            Console.Write("args: " + args.Length + args[0]);
            if (args.Length > 0)
            {

                switch (args[0].ToLower())
                {
                    case "-embedding":
                        StartedByCOM = true;										// Indicate COM started us
                        break;

                    case "-register":
                    case @"/register":
                    case "-regserver":											// Emulate VB6
                    case @"/regserver":
                        RegisterObjects();										// Register each served object
                        bRet = false;
                        break;

                    case "-unregister":
                    case @"/unregister":
                    case "-unregserver":										// Emulate VB6
                    case @"/unregserver":
                        UnregisterObjects();									//Unregister each served object
                        bRet = false;
                        break;

                    default:
                        MessageBox.Show("Unknown argument: " + args[0] + "\nValid are : -register, -unregister and -embedding",
                            "Pictavore_Server", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        break;
                }
            }
            else
                StartedByCOM = false;

            return bRet;
        }
        #endregion

        #region SERVER ENTRY POINT (main)
        //
        // ==================
        // SERVER ENTRY POINT
        // ==================
        //
        [STAThread]
        static void Main(string[] args)
        {
            if (!LoadComObjectAssemblies()) return;						// Load served COM class assemblies, get types
            //MessageBox.Show("after loadComObjectAssemblies");
            //MessageBox.Show("args: " + args.Length + args[0]);
            //Debug.Write("args: " + args.Length + args[0]);
            if (!ProcessArguments(args)) return;						// Register/Unregister
            //MessageBox.Show("after ProcessArguments");
            //Console.Write("after ProcessArguments");

            // Initialize critical member variables.
            objsInUse = 0;
            serverLocks = 0;
            MainThreadId = GetCurrentThreadId();
            Thread.CurrentThread.Name = "Main Thread";

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            s_MainForm = new frmMain();
            if (StartedByCOM) s_MainForm.WindowState = FormWindowState.Minimized;

            // Register the class factories of the served objects
            RegisterClassFactories();

            // Start up the garbage collection thread.
            GarbageCollection GarbageCollector = new GarbageCollection(1000);
            Thread GCThread = new Thread(new ThreadStart(GarbageCollector.GCWatch));
            GCThread.Name = "Garbage Collection Thread";
            GCThread.Start();

            //
            // Start the message loop. This serializes incoming calls to our
            // served COM objects, making this act like the VB6 equivalent!
            //
            try
            {
                Application.Run(s_MainForm);
            }
            finally
            {
                // Revoke the class factories immediately.
                // Don't wait until the thread has stopped before
                // we perform revocation!!!
                RevokeClassFactories();

                // Now stop the Garbage Collector thread.
                GarbageCollector.StopThread();
                GarbageCollector.WaitForThreadToStop();
            }
        }
        #endregion
    }

    public static class FTDIio
    {

        private static FTDI data = new FTDI();
        private static FTDI term = new FTDI();
        private static bool cameraConnectedState = false;
        private static bool focuserConnectedState = false;
        private static bool filterwheelConnectedState = false;
        //        public static bool commandBusy = false;                 //Flag to show still inside a command
        public static object cmdLock = new object();           //Object used to lock the CommandStrings
        private static bool grabImageLock = false;              //internal flag set to true when camera is in GRIMG

        private const int rcvWaitTime = 5;
        private const string OKrn = "OK\r\n";
        private const string CR = "\n";

        //public static int rnd = new Random().Next(1,10000);

        //public FTDIio()
        //{
        //}


        public static void open()
        {
            FTDI.FT_STATUS statusComm, statusTerm;
            statusComm = data.OpenByIndex(0);
            statusTerm = term.OpenByIndex(1);
            if (!(statusComm == FTDI.FT_STATUS.FT_OK && statusTerm == FTDI.FT_STATUS.FT_OK))
            {
                throw new Exception("Can't connect");
            }
            AddCommands.addCommands();
        }

        public static void close()
        {
            FTDI.FT_STATUS statusComm, statusTerm;
            statusComm = data.Close();
            statusTerm = term.Close();
            if (!(statusComm == FTDI.FT_STATUS.FT_OK && statusTerm == FTDI.FT_STATUS.FT_OK))
            {
                throw new Exception("Error on ftdidevice close");
            }
        }


        public static bool GrabImageLock
        {
            get { return grabImageLock; }

            set { grabImageLock = value; }
        }
        //General Connection Status
        public static bool Connected
        {
            get { return IsConnected; }

            set
            {
                lock (cmdLock)
                {
                    if (value == IsConnected)
                        return;

                    if (value)
                    {
                        open();
                        //connectedState = true;
                    }
                    else
                    {
                        close();
                        //connectedState = false;
                    }
                }
            }
        }

        private static bool IsConnected
        {
            get { return term.IsOpen && data.IsOpen; }
        }



        //Camera Connection Status
        public static bool CameraConnected
        {
            get { return CameraIsConnected; }

            set
            {
                lock (cmdLock)
                {
                    if (value == CameraIsConnected)
                        return;

                    if (value)
                    {
                        if (!IsConnected)
                        {
                            Connected = true;
                        }
                        cameraConnectedState = true;
                    }
                    else
                    {
                        if (!focuserConnectedState && !filterwheelConnectedState)
                        {
                            Connected = false;
                        }
                        cameraConnectedState = false;
                    }
                }
            }
        }

        private static bool CameraIsConnected
        {
            get { return cameraConnectedState; }
        }



        //Focuser Connection Status
        public static bool FocuserConnected
        {
            get { return FocuserIsConnected; }

            set
            {
                lock (cmdLock)
                {
                    if (value == FocuserIsConnected)
                        return;

                    if (value)
                    {
                        if (!IsConnected)
                        {
                            Connected = true;
                        }
                        focuserConnectedState = true;
                    }
                    else
                    {
                        if (!cameraConnectedState && !filterwheelConnectedState)
                        {
                            Connected = false;
                        }
                        focuserConnectedState = false;
                    }
                }
            }
        }

        private static bool FocuserIsConnected
        {
            get { return focuserConnectedState; }
        }



        //Filter Wheel Connection Status
        public static bool FilterwheelConnected
        {
            get { return FilterwheelIsConnected; }

            set
            {
                lock (cmdLock)
                {
                    if (value == FilterwheelIsConnected)
                        return;

                    if (value)
                    {
                        if (!IsConnected)
                        {
                            Connected = true;
                        }
                        filterwheelConnectedState = true;
                    }
                    else
                    {
                        if (!cameraConnectedState && !focuserConnectedState)
                        {
                            Connected = false;
                        }
                        filterwheelConnectedState = false;
                    }
                }
            }
        }

        private static bool FilterwheelIsConnected
        {
            get { return filterwheelConnectedState; }
        }



        public static byte[] readio(uint size)
        {
            /*
             * Read from data channel
             */
            byte[] line = new byte[size];
            uint numBytesRead = 0;

            data.Read(line, size, ref numBytesRead);
            return line;
        }



        public static uint inWaiting()
        /*
         * Returns the number of bytes in the camera's terminal read buffer
         */
        {
            uint bytesInBuffer = 0;

            term.GetRxBytesAvailable(ref bytesInBuffer);
            return bytesInBuffer;
        }



        public static string read(uint numBytes = 1)
        {
            /*
             * Read "numBytes" from camera's terminal input stream
             */
            string buffer = "";
            uint numBytesRead = 0;
            uint bytesInBuffer = 0;
            bool timeOut = false;
            DateTime startTime = DateTime.Now;

            while (!timeOut && bytesInBuffer < numBytes)
            {
                bytesInBuffer = inWaiting();
                timeOut = ((DateTime.Now - startTime).TotalSeconds > rcvWaitTime);
            }
            if (bytesInBuffer > 0)
            {
                term.Read(out buffer, numBytes, ref numBytesRead);
            }

            return buffer;
        }



        public static void write(string s)
        {
            /*
             * Write string s to camera's terminal channel.
             */
            uint numBytesWritten = 0;

            term.Write(s, s.Length, ref numBytesWritten);
        }


        public static void sendLine(string s)
        /*
         * Send a command and wait for the echo of the command, but nothing else
         * Return nothing
         *   (probably should be private)
        */
        {
            term.Purge(FTDI.FT_PURGE.FT_PURGE_RX);                          //Make sure RCV buffer is empty
            write(s);                                                       //Write string to camera
            Debug.WriteLine("sendLine: " + s);
            cameraResponse(s.Trim());                                       //Wait just for echo
        }//end sendLine(...



        public static string sendLineWaitOk(string s)
        {
            /*
             * Send string s to camera's terminal channel
             * Return the camera's response
             *   (should probably be private)
             */
            string response;

            term.Purge(FTDI.FT_PURGE.FT_PURGE_RX);                          //Make sure RCV buffer is empty
            write(s);
            Debug.WriteLine("sendLine: " + s);
            response = cameraResponse(OKrn);
            Debug.WriteLine("sendLineResponse: " + response);
            return response;
        }



        public static string cameraResponse(string eol = OKrn, bool returnAll = false)
        {
            /*
             * Read from camera's terminal channel until "eol" is reached or times out
             * Returns the response from the camera without the echo and eol
             * unless "returnAll" is true where the whole response is returned.
             *  (again, probably private in Release version)
             */
            string buffer;
            //uint bytesInBuffer = 0;
            bool timeOut = false;
            StringBuilder response = new StringBuilder();
            string r;

            DateTime startTime = DateTime.Now;                              //Start time waiting for response
            while (!response.ToString().EndsWith(eol) && !timeOut)          //Continue until whole response is received or times out
            {
                //term.GetRxBytesAvailable(ref bytesInBuffer);                //Anything in buffer?
                //bytesInBuffer = inWaiting();                                //Anything in buffer?
                if (inWaiting() > 0)                                      //If yes,
                {
                    //term.Read(out buffer, bytesInBuffer, ref numBytesRead); //  get it
                    buffer = read();                                           //  read one character
                    response.Append(buffer);
                }
                timeOut = ((DateTime.Now - startTime).TotalSeconds)
                                                        > rcvWaitTime;      //Check that it hasn't timed out
            }
            Debug.Write("Whole camera response: " + response);
            if (returnAll)                                                  //Return entire response string
            {
                r = response.ToString();
            }
            else                                                            // else, just return the command response
            {
                if (!timeOut)
                    response.Remove(response.Length - eol.Length, eol.Length);  //Remove the trailing "OK\r\n"
                char[] charsToTrim = { ' ', '\r', '\n' };                       //Remove trailing spaces or EOLs (i.e. for TEMP?)
                r = response.ToString().TrimEnd(charsToTrim).Split(' ').Last(); //Now, last item after split on space
                //  is the command response
                Debug.Write("Actual command response: " + r);
            }
            return r;
        }// end cameraResponse(...




        public static void waitForOk(double Duration = 0)
        {
            /*
             * Wait for "OK\r\n" from camera after "Duration", for example, after EXPOSE
             */
            lock (cmdLock)
            {
                StringBuilder response = new StringBuilder();

                DateTime startTime = DateTime.Now;                          //Start time waiting for response
                Debug.WriteLine("Enter waitForOk " + startTime);
                if (Duration > 0) Thread.Sleep((int)(Duration * 1000));
                cameraResponse(OKrn);
                //Debug.WriteLine("Exit waitForOk " + timeOutTime);
                Debug.WriteLine("waitForOkResponse " + response);
            }
        }



        private static void CheckConnected(string message)
        {
            if (!IsConnected)
            {
                throw new Exception(message);
            }
        }


        public static string CommandString(string command, bool raw = false)
        {
            // it's a good idea to put all the low level communication with the device here,
            // then all communication calls this function
            // you need something to ensure that only one command is in progress at a time

            lock (cmdLock)
            {
                string response = "";

                CheckConnected("CommandString");
                if (!raw) command += "\r";

                response = sendLineWaitOk(command);

                return response;
            }
        }


        public static void CommandBlind(string command, bool raw = false)
        {
            lock (cmdLock)
            {
                CheckConnected("CommandStringNoWait");
                if (!raw) command += "\r";

                FTDIio.sendLine(command);
            }
        }
    }

}
