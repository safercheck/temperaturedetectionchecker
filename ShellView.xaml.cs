using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.Windows.Media;

using yoseen = YoseenSDKCS;
using OnvifClient;
using WpfBase;

using IRTool.CoreDef;
using IRTool.ConfigDef;
using IRTool.ResourceDef;
using IRTool.Views;
using IRTool.SecurityDef;
using IRTool.BusiDef;

namespace IRTool
{
    public partial class ShellView : Window
    {
        //
        readonly DeviceConfigService _dcService;
        readonly ClsHead _clsHead;
        readonly ClsDevice _clsDevice;
        readonly ClsFDConfig _clsFDConfig;

        //
        readonly DataRecordService _recordService;
        readonly ObservableCollection<DataRecord> _records;
        int _recordTotalCount;

        //
        readonly BusiManager _busiManager;

        //
        readonly YoseenWarden _warden;
        readonly Onvif _onvifDevice;
        readonly DispatcherTimer _timer;

        readonly IntToBrushConverter _intToBrushConverter;
        readonly IntToImageSourceConverter _intToImageSourceConverter;

        bool _isDisposed;
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _warden.StopPreview();
            }
        }

        public ShellView()
        {
            InitializeComponent();

            //
            _dcService = App.Instance.dcService;
            _clsHead = _dcService._clsHead;
            _clsDevice = _dcService._clsDevice;
            _clsFDConfig = _dcService._clsFDConfig;

            //
            _recordService = App.Instance.dataAccess.ServiceDataRecord;
            _records = new ObservableCollection<DataRecord>();
            lvRecord.ItemsSource = _records;
            _recordTotalCount = 0;

            //
            _busiManager = App.Instance.busiManager;

            //
            _warden = App.Instance.warden;
            _onvifDevice = _warden.visDevice;

            //
            _delFrameRecved_UI = _funcFrameRecved_UI;
            _visDelFrameRecved_UI = _visFuncFrameRecved_UI;
            fdCanvas._delUpdateIR = _funcUpdateIR;
            fdCanvas._delEndUpdate = _funcEndUpdate;
            setFaceDispType();

            _intToBrushConverter = (IntToBrushConverter)this.Resources["intToBrushConverter"];
            _intToBrushConverter.BrushNormal = Brushes.Green;
            _intToImageSourceConverter= (IntToImageSourceConverter)this.Resources["intToImageSourceConverter"];
            _intToImageSourceConverter.ImageNormal= (ImageSource)this.Resources["normalDrawingImage"];
            _intToImageSourceConverter.ImageAbnormal = (ImageSource)this.Resources["abnormalDrawingImage"];

            //
            _clsHead.PropertyChanged += _clsHead_PropertyChanged;
            _clsDevice.PropertyChanged += _clsDevice_PropertyChanged;

            //
            _timer = new DispatcherTimer();
            _timer.Interval = App.Const_UpdateDelta;
            _timer.Tick += _timer_Tick;
            _timer.Start();
            imgLogo.Source = AppStatic.BmpLogo;

            //
            this.MouseDoubleClick += ShellView_MouseDoubleClick;
            this.Loaded += ShellView_Loaded;
        }


        void setFaceDispType()
        {
            FaceDispType snapType = _clsHead.Misc_FaceDispType;

            if (snapType == FaceDispType.FaceWithTemp)
            {
                Grid.SetRowSpan(viewbox, 1);
                viewbox.Stretch = Stretch.Fill;
                lvRecord.Visibility = Visibility.Visible;
                lvRecord.ItemTemplate = (DataTemplate)this.Resources["templateRecordHorz"];
                fdCanvas._showTemp = true;
            }
            else if (snapType == FaceDispType.Face)
            {
                Grid.SetRowSpan(viewbox, 1);
                viewbox.Stretch = Stretch.Fill;
                lvRecord.Visibility = Visibility.Visible;
                lvRecord.ItemTemplate = (DataTemplate)this.Resources["templateRecordHorz2"];
                fdCanvas._showTemp = false;
            }
            else
            {
                Grid.SetRowSpan(viewbox, 2);
                viewbox.Stretch = Stretch.Uniform;
                lvRecord.Visibility = Visibility.Hidden;
                fdCanvas._showTemp = false;
            }
        }

        private void _clsHead_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Misc_FaceDispType")
            {
                setFaceDispType();
            }
        }

        #region timer
        DateTime _lastDT = DateTime.MinValue;
        private void _timer_Tick(object sender, EventArgs e)
        {
            DateTime dtNow = DateTime.Now;
            if (dtNow.Date != _lastDT.Date)
            {
                txtDate.Text = string.Format("{0:yyyy-MM-dd} {1}", dtNow, Loc.DayOfWeekArray[(int)dtNow.DayOfWeek]);
                _lastDT = dtNow;
                btnClearData_Click(null, null);
            }
            txtTime.Text = string.Format("{0:HH:mm:ss}", dtNow);
        }
        #endregion

        #region fullscreen
        bool _isFullscreen;
        private void ShellView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_isFullscreen)
            {
                _isFullscreen = false;
                this.ResizeMode = ResizeMode.CanResize;
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.WindowState = WindowState.Normal;
                this.Topmost = false;
            }
            else
            {
                _isFullscreen = true;
                bool hideRequired = this.WindowState == WindowState.Maximized;
                if (hideRequired) this.Visibility = Visibility.Collapsed;
                this.ResizeMode = ResizeMode.NoResize;
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Maximized;
                this.Topmost = true;
                if (hideRequired) this.Visibility = Visibility.Visible;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            LogoutView.Instance.ShowLogout();
            if (LogoutView.Instance.LogoutComformed)
            {
                fdCanvas.EndUpdate();
                _warden.StopPreview();
                base.OnClosing(e);
            }
            else
            {
                e.Cancel = true;
            }
        }

        bool _isLoaded;
        private void ShellView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isLoaded) return;
            btnPlay_Click(null, null);
            _isLoaded = true;
        }
        #endregion

        private void _clsDevice_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IR_ManualDelay")
            {
                _warden.SetDelay(_clsDevice.IR_ManualDelay);
            }
            else if (e.PropertyName.StartsWith("IRImage"))
            {
                configImage(ref _clsDevice._bin);
            }
        }

        #region play
        void _setUI()
        {
            btnPlay.IsEnabled = true;
            btnPlay.IsChecked = _isPlaying;
        }

        bool _isPlaying;
        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (_isPlaying)
            {
                fdCanvas.EndUpdate();
                _warden.StopPreview();
                _isPlaying = false;
                _setUI();
            }
            else
            {
                btnPlay.IsEnabled = false;
                Task.Factory.StartNew(() =>
                {
                    int ret = _warden.StartPreview(_clsDevice, _funcFrameRecved, _visFuncFrameRecved);
                    if (_warden.visPreviewHandle >= 0)
                    {
                        _onvifDevice.ChangeConfig(_clsDevice.VIS_CameraIp, _clsDevice.VIS_OnvifPort, _clsDevice.VIS_Username, _clsDevice.VIS_Password);
                    }
                    return ret;
                }).ContinueWith(x =>
                {
                    if (_warden.PreviewHandle >= 0)
                    {
                        fdCanvas.BeginUpdate(_warden.irDataWidth, _warden.irDataHeight, 0);
                        configImage(ref _clsDevice._bin);
                    }
                    if (_warden.visPreviewHandle >= 0)
                    {
                        fdCanvas.BeginUpdate(_warden.visDataWidth, _warden.visDataHeight, 1);
                    }
                    if (_warden.PreviewHandle >= 0 && _warden.visPreviewHandle >= 0)
                    {
                        fdCanvas.ChangeAlg(ref _clsFDConfig._bin);
                    }
                    _isPlaying = (0 == x.Result);
                    _setUI();


                    //
                    if (_warden.PreviewHandle >= 0 && _warden._bbConfig.enable == 0)
                    {
                        ConfigHSRPView.Instance.MyShow(_warden.UserHandle, ref _warden._basicInfo, true);
                    }

                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        #endregion

        #region update

        DateTime _updateTime;

        readonly Action<int, yoseen.DataFrame> _delFrameRecved_UI;
        void _funcFrameRecved(int errorCode, ref yoseen.DataFrame dataFrame)
        {
            this.Dispatcher.BeginInvoke(_delFrameRecved_UI, errorCode, dataFrame);
        }
        void _funcFrameRecved_UI(int errorCode, yoseen.DataFrame dataFrame)
        {
            if (_warden.PreviewHandle < 0) return;
            if (errorCode == 0)
            {
                _updateTime = DateTime.Now;
                fdCanvas.UpdateIR(ref _updateTime, ref dataFrame);
            }
            else if (errorCode == (int)yoseen.YoseenErrorType.YET_PreviewRecoverBegin)
            {
                Trace.WriteLine(string.Format("{0:yyyy-MM-dd HH:mm:ss}, IR recover begin", DateTime.Now));
            }
            else if (errorCode == (int)yoseen.YoseenErrorType.YET_PreviewRecoverEnd)
            {
                Trace.WriteLine(string.Format("{0:yyyy-MM-dd HH:mm:ss}, IR recover end", DateTime.Now));
            }
        }

        readonly Action<int, yoseen.DataFrame> _visDelFrameRecved_UI;
        void _visFuncFrameRecved(int errorCode, ref yoseen.DataFrame dataFrame)
        {
            //fix20200324
            if (_warden.visPreviewHandle < 0) return;
            if (0 == errorCode)
            {
                fdCanvas.algUpdateVIS(dataFrame.H264, dataFrame.Bmp);
            }
            this.Dispatcher.BeginInvoke(_visDelFrameRecved_UI, errorCode, dataFrame);
        }
        void _visFuncFrameRecved_UI(int errorCode, yoseen.DataFrame dataFrame)
        {
            if (_warden.visPreviewHandle < 0) return;
            if (errorCode == 0)
            {
                //
                fdCanvas.UpdateVIS(ref dataFrame);
            }
            else if (errorCode == (int)yoseen.YoseenErrorType.YET_PreviewRecoverBegin)
            {
                Trace.WriteLine(string.Format("{0:yyyy-MM-dd HH:mm:ss}, VIS recover begin", DateTime.Now));
            }
            else if (errorCode == (int)yoseen.YoseenErrorType.YET_PreviewRecoverEnd)
            {
                Trace.WriteLine(string.Format("{0:yyyy-MM-dd HH:mm:ss}, VIS recover end", DateTime.Now));
            }
        }

        void _funcEndUpdate()
        {
            AlarmEnd();
        }

        void _funcUpdateIR(ref DateTime dt, ref FDOutput algOutput)
        {
            //gnew
            AlarmCheck(ref dt, ref algOutput);
            if (algOutput.gnew == 0) return;

            //
            if (algOutput.galarmExp > 0)
            {
                AlarmConfirmView.Instance.MyShow();
                return;
            }

            //target
            string fn;
            int targetCount = algOutput.targetCount;
            FDTarget[] targetArray = algOutput.targetArray;
            int i = 0;
            for (; i < targetCount; i++)
            {
                if (targetArray[i].idNew == 0) continue;
                _recordTotalCount++;
                int index = i + 1;
                if (targetArray[i].alarm > 0)
                {
                    index = -index;
                }
                DataRecord record = new DataRecord(dt, index, targetArray[i].irMaxTemp);
                fn = record.fnTarget;
                if (_clsHead.Misc_FaceSaveMode == FaceSaveMode.VIS)
                {
                    fdCanvas.SaveTarget(ref targetArray[i], fn);
                }
                else
                {
                    fdCanvas.SaveTargetIR(ref targetArray[i], fn);
                }

                //
                _recordService.Create(record);
                if (_records.Count >= _clsHead.Misc_RecentCount) _records.RemoveAt(_records.Count - 1);
                _records.Insert(0, record);
                _busiManager.AddFace(record);
            }
            txtTotalCount.Text = _recordTotalCount.ToString();

            //ftp, whole
            fn = _busiManager.GetWhole(ref algOutput, ref dt);
            if (null != fn)
            {
                int ret = fdCanvas.SaveFile(fn);
                if (0 == ret)
                {
                    _busiManager.AddWhole(fn);
                }
            }
        }
        #endregion

        #region config

        private void btnConfigSystem_Click(object sender, RoutedEventArgs e)
        {
            SystemConfigView.Instance.Show();
        }

        private void btnConfigHSRP_Click(object sender, RoutedEventArgs e)
        {
            int userHandle = _warden.UserHandle;
            if (userHandle < 0) return;
            ConfigHSRPView.Instance.MyShow(userHandle, ref _warden._basicInfo, false);
        }

        yoseen.DataFrame _irFrame;
        yoseen.DataFrame _visFrame;
        void afterConfig(ref FDConfig binConfig)
        {
            fdCanvas.ChangeAlg(ref binConfig);
            _clsFDConfig.Bin2Cls(ref binConfig);
            _dcService.ChangeFDConfig();
        }
        private void btnConfigAlg_Click(object sender, RoutedEventArgs e)
        {
            if (fdCanvas._D2D.irPtrBfd == IntPtr.Zero || fdCanvas._D2D.visPtrH264 == IntPtr.Zero) return;
            _irFrame.Head = fdCanvas._D2D.irPtrDfh;
            _irFrame.Temp = fdCanvas._D2D.irPtrDfd;

            _visFrame.H264 = fdCanvas._D2D.visPtrH264;
            _visFrame.Bmp = fdCanvas._D2D.visPtrBfd;

            FDConfigView view = FDConfigView.Instance;
            view.ShowConfig(ref _irFrame, ref _visFrame, ref _clsFDConfig._bin, afterConfig);
        }

        void configImage(ref BinDevice bin)
        {
            bin.IRImage_StrechControl.change_type = (yoseen.StrechControlFlags.SCF_StrechType | yoseen.StrechControlFlags.SCF_Gain
                | yoseen.StrechControlFlags.SCF_DDELevel | yoseen.StrechControlFlags.SCF_Linear
                | yoseen.StrechControlFlags.SCF_ColorTemp);
            _warden.SetImage(ref bin.IRImage_StrechControl, bin.IRImage_PaletteType);
            fdCanvas.ChangePalette(bin.IRImage_PaletteType);
        }
        #endregion

        #region data
        private void btnSearchData_Click(object sender, RoutedEventArgs e)
        {
            SearchDbView.Instance.ShowDialog();
        }

        private void btnClearData_Click(object sender, RoutedEventArgs e)
        {
            _records.Clear();
            _recordTotalCount = 0;
            txtTotalCount.Text = _recordTotalCount.ToString();
        }
        #endregion

        #region alarm
        bool _alarmState;
        DateTime _alarmTime;

        void AlarmEnd()
        {
            _busiManager.PlayAlarm(false);
            _alarmState = false;
        }

        void AlarmCheck(ref DateTime dt, ref FDOutput algOutput)
        {
            bool isAlarmed = algOutput.galarm > 0;
            bool needChange = _alarmState != isAlarmed && (dt - _alarmTime) > App.Const_AlarmDelta;
            if (!needChange) return;

            _alarmState = isAlarmed;
            _alarmTime = dt;

            int ret = _warden.SetGpio(isAlarmed);

            //sound
            _busiManager.PlayAlarm(isAlarmed);
        }

        #endregion

        public void XXXConfigHSRP()
        {
            btnConfigHSRP_Click(null, null);
        }
    }

}
