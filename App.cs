using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.Runtime.Serialization;

using IRTool.CoreDef;
using IRTool.ConfigDef;
using IRTool.BusiDef;
using IRTool.ResourceDef;
using IRTool.FDCanvasDef;
using WpfMedia;

namespace IRTool
{
    class App : Application
    {
        #region static
        public static readonly TimeSpan Const_UpdateDelta = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan Const_AlarmDelta = TimeSpan.FromSeconds(3);
        #endregion

        static App __instance;
        public static App Instance
        {
            get
            {
                if (__instance == null)
                {
                    __instance = new App();
                }
                return App.__instance;
            }
        }

        ShellView _shellView;
        public ShellView shellView
        {
            get { return _shellView; }
        }

        DeviceConfigService _dcService;
        public DeviceConfigService dcService
        {
            get { return _dcService; }
        }

        YoseenWarden _warden;
        public YoseenWarden warden
        {
            get
            {
                return _warden;
            }
        }

        FDCanvas _fdCanvas;
        public FDCanvas fdCanvas
        {
            get
            {
                return _fdCanvas;
            }
        }

        BusiManager _busiManager;
        public BusiManager busiManager
        {
            get
            {
                return _busiManager;
            }
        }

        DataAccess _dataAccess;
        public DataAccess dataAccess
        {
            get { return _dataAccess; }
        }

        private App()
        {
            //
            if (Loc.AppFont != "")
            {
                FontFamily Const_FontFamily = new FontFamily(Loc.AppFont);
                TextElement.FontFamilyProperty.OverrideMetadata(typeof(TextElement), new FrameworkPropertyMetadata(Const_FontFamily));
                TextBlock.FontFamilyProperty.OverrideMetadata(typeof(TextBlock), new FrameworkPropertyMetadata(Const_FontFamily));
            }

            double Const_FontSize = 14;
            TextElement.FontSizeProperty.OverrideMetadata(typeof(TextElement), new FrameworkPropertyMetadata(Const_FontSize));
            TextBlock.FontSizeProperty.OverrideMetadata(typeof(TextBlock), new FrameworkPropertyMetadata(Const_FontSize));

            //
            this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            this.Exit += App_Exit;
        }

        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Trace.WriteLine(e.Exception.Message);
        }

        void App_Exit(object sender, ExitEventArgs e)
        {
            _dcService.SaveConfig();
            _busiManager.StopWork();
            EnableScreenCapture(false);
        }

        public void DoMain()
        {
            //
            _dcService = new DeviceConfigService();
            _warden = new YoseenWarden();

            _dataAccess = new DataAccess(AppStatic.FileDb);
            _busiManager = new BusiManager();
            _busiManager.StartWork();

            //
            _clsHead = _dcService._clsHead;
            _clsHead.PropertyChanged += _clsHead_PropertyChanged;
            EnableScreenCapture(_clsHead.Misc_EnableScreenCapture);


            //
            ShellView shellView = new ShellView();
            _fdCanvas = shellView.fdCanvas;
            _shellView = shellView;
            this.Run(_shellView);
        }


        public void XXXConfigHSRP()
        {
            _shellView.XXXConfigHSRP();
        }

        ClsHead _clsHead;
        private void _clsHead_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            //
            bool changeScreenCapture = e.PropertyName == "Misc_EnableScreenCapture";
            if (changeScreenCapture)
            {
                EnableScreenCapture(_clsHead.Misc_EnableScreenCapture);
            }
        }
        Process _scProcess;
        void EnableScreenCapture(bool enable)
        {
            try
            {
                if (enable && _scProcess == null)
                {
                    //
                    Process[] ps = Process.GetProcessesByName("ScreenCapture");
                    for (int i = 0; i < ps.Length; i++)
                    {
                        ps[i].Kill();
                    }
                    //
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.WorkingDirectory = AppStatic.Exe;
                    psi.FileName = "ScreenCapture.exe";
                    psi.Arguments = ".\\Config\\ScreenCapture.conf";
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    //psi.RedirectStandardInput = true;
                    //psi.RedirectStandardOutput = true;
                    _scProcess = Process.Start(psi);
                }
                else if (!enable && _scProcess != null)
                {
                    Process p = _scProcess;
                    _scProcess = null;
                    p.Kill();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }

        }

    }


    public class AppStatic : WpfMedia.ResourceDef.AppStatic
    {
        public const string ConstAppName = "HWJYC";

        static string _fileBin;
        public static string FileBin
        {
            get
            {
                if (null == _fileBin)
                {
                    _fileBin = string.Format("{0}\\{1}.bin", Config, ConstAppName);
                }
                return _fileBin;
            }
        }

        static string _fileLog;
        public static string FileLog
        {
            get
            {
                if (null == _fileLog)
                {
                    _fileLog = string.Format("{0}\\{1}.log", Exe, ConstAppName);
                }
                return _fileLog;
            }
        }

        static string _fileAlarmAudio;
        public static string FileAlarmAudio
        {
            get
            {
                if (null == _fileAlarmAudio)
                {
                    _fileAlarmAudio = Config + "\\alarm.wav";
                }
                return _fileAlarmAudio;
            }
        }

        static BitmapImage _bmpWarning;
        public static BitmapImage BmpWarning
        {
            get
            {
                return _bmpWarning;
            }
        }

        static BitmapImage _bmpLogo;
        public static BitmapImage BmpLogo
        {
            get
            {
                return _bmpLogo;
            }
        }

        static string _dataAlarm;
        public static string DataAlarm
        {
            get
            {
                if (_dataAlarm == null)
                {
                    _dataAlarm = Exe + "\\DataAlarm";
                }
                return _dataAlarm;
            }
        }

        static string _fileDb;
        public static string FileDb
        {
            get
            {
                if (null == _fileDb)
                {
                    _fileDb = string.Format("{0}\\{1}.db", Config, ConstAppName);
                }
                return _fileDb;
            }
        }

        static string _fileFaceDetectModel;
        public static string FileFaceDetectModel
        {
            get
            {
                if (null == _fileFaceDetectModel)
                {
                    _fileFaceDetectModel = string.Format("{0}\\FaceDetect.model", Config, ConstAppName);
                }
                return _fileFaceDetectModel;
            }
        }

        static string _fileSecurity;
        public static string fileSecurity
        {
            get
            {
                if (null == _fileSecurity)
                {
                    _fileSecurity = string.Format("{0}\\{1}.security", Config, ConstAppName);
                }
                return _fileSecurity;
            }
        }

        static bool _isInited;
        public static void Init()
        {
            if (_isInited) return;
            int ret = SDKContext.Init();
            if (0 != ret) throw new Exception(string.Format("SDKContext.Init, ret {0}", ret));
            _bmpWarning = new BitmapImage(new Uri(string.Format("{0}\\Warning.png", Config)));
            _isInited = true;

            string fn = string.Format("{0}\\Logo.png", Config);
            if (File.Exists(fn)) _bmpLogo = new BitmapImage(new Uri(fn));

        }

        public static void Free()
        {
            if (!_isInited) return;
            _isInited = false;
            SDKContext.Free();
        }
    }
}
