using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using yoseen = YoseenSDKCS;
using WpfBase;
using WpfMedia.BbCanvasDef;

using IRTool.ResourceDef;
using IRTool.CoreDef;

namespace IRTool.Views
{
    public partial class ConfigHSRPView : Window, IDisposable
    {
        #region Single
        static ConfigHSRPView __instance;
        public static ConfigHSRPView Instance
        {
            get
            {
                if (__instance == null)
                {
                    __instance = new ConfigHSRPView();
                    __instance.Owner = App.Instance.shellView;
                }
                return ConfigHSRPView.__instance;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            //
            if (!_forceClose)
            {
                e.Cancel = true;
                this.Hide();
            }
            base.OnClosing(e);
        }

        bool _forceClose;
        public static void ForceClose()
        {
            if (__instance == null) return;
            __instance._forceClose = true;
            __instance.Close();
            __instance = null;
        }
        #endregion

        IntPtr _ptrPlayback;
        readonly ExtBbConfig _bbConfig;
        public ConfigHSRPView()
        {
            InitializeComponent();

            _ptrPlayback = yoseen.YoseenPlayback.YoseenPlayback_Create();
            if (_ptrPlayback == IntPtr.Zero) throw new Exception("YoseenPlayback_Create, error");
            _bbConfig = bbCanvas.BbConfig;
            pgBbConfig.SelectedObject = _bbConfig;
        }

        public void Dispose()
        {
            if (_ptrPlayback == IntPtr.Zero) return;
            yoseen.YoseenPlayback.YoseenPlayback_Free(ref _ptrPlayback);
        }

        int _userHandle;
        int _dataWidth;
        int _dataHeight;
        int _dataTransform;

        public void MyShow(int userHandle, ref yoseen.CameraBasicInfo basicInfo, bool isAuto)
        {
            if (this.Visibility == Visibility.Visible) return;
            _userHandle = userHandle;
            _dataWidth = basicInfo.DataWidth;
            _dataHeight = basicInfo.DataHeight;
            _dataTransform = basicInfo.DataTransform;

            //
            txtAutoInfo.Visibility = isAuto ? Visibility.Visible : Visibility.Hidden;

            //
            btnRefresh_Click(null, null);
            this.Title = string.Format("{0}, {1}", Loc.ConfigHSRP, basicInfo.CameraId);
            this.ShowDialog();
        }

        yoseen.CtlX _ctlx;
        yoseen.DataFrameHeader _dataFrameHeader;
        yoseen.DataFrame _dataFrame;
        yoseen.TempFrameFile _tffStruct;

        void loadFrame()
        {
            int ret = yoseen.YoseenPlayback.YoseenPlayback_OpenMem(_ptrPlayback, _tffStruct.dfh, _tffStruct.dfd);
            if (0 > ret) return;
            ret = yoseen.YoseenPlayback.YoseenPlayback_ReadFrame(_ptrPlayback, 0, ref _dataFrame);
            if (0 > ret) return;
            _dataFrameHeader = (yoseen.DataFrameHeader)Marshal.PtrToStructure(_dataFrame.Head, typeof(yoseen.DataFrameHeader));

            //
            bbCanvas.BeginUpdate(ref _dataFrameHeader);
            bbCanvas.UpdateData(ref _dataFrame);

            //
            _ctlx.BbConfig.Change(_dataWidth, _dataHeight, _dataTransform);
            bbCanvas.ChangeBbConfig(ref _ctlx.BbConfig);
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            btnRefresh.IsEnabled = false;
            btnSave.IsEnabled = false;
            Task.Factory.StartNew(() =>
            {
                _ctlx.Type = yoseen.CtlXType.CtlXType_GetExtBbConfig;
                int ret = yoseen.YoseenSDK.Yoseen_SendControlX(_userHandle, ref _ctlx);
                if (0 != ret || _ctlx.Type != yoseen.CtlXType.CtlXType_GetExtBbConfig)
                {
                    ret = -1;
                }
                if (0 == ret)
                {
                    ret = yoseen.YoseenSDK.Yoseen_SaveFrameToMem(_userHandle, ref _tffStruct);
                }
                return ret;
            }).ContinueWith(x =>
            {
                int ret = x.Result;
                btnRefresh.IsEnabled = true;
                btnSave.IsEnabled = true;
                btnSave.Foreground = ret < 0 ? Brushes.Red : Brushes.Black;
                if (0 == ret)
                {
                    loadFrame();
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            _bbConfig.Cls2Bin(ref _ctlx.BbConfig);
            _ctlx.BbConfig.Change(_dataWidth, _dataHeight, _dataTransform);

            //
            btnSave.IsEnabled = false;
            Task.Factory.StartNew(() =>
            {
                _ctlx.Type = yoseen.CtlXType.CtlXType_SetExtBbConfig;
                int ret = yoseen.YoseenSDK.Yoseen_SendControlX(_userHandle, ref _ctlx);
                if (0 != ret || _ctlx.Type != yoseen.CtlXType.CtlXType_SetExtBbConfig)
                {
                    ret = -1;
                }
                return ret;
            }).ContinueWith(x =>
            {
                btnSave.IsEnabled = true;
                btnSave.Foreground = x.Result < 0 ? Brushes.Red : Brushes.Black;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}
