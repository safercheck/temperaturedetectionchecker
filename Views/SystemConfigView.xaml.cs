using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.ComponentModel;
using System.Threading.Tasks;

using yoseen = YoseenSDKCS;
using WpfMedia.BasicViews;
using OnvifClient;

using IRTool.CoreDef;
using IRTool.ConfigDef;
using IRTool.ResourceDef;

namespace IRTool.Views
{
    public partial class SystemConfigView : Window
    {
        #region Single
        static SystemConfigView __instance;
        public static SystemConfigView Instance
        {
            get
            {
                if (__instance == null)
                {
                    __instance = new SystemConfigView();
                    __instance.Owner = App.Instance.shellView;
                }
                return SystemConfigView.__instance;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            //
            if (!_forceClose)
            {
                //hack, force PropertyGrid changed
                btnRemoteConfig.Focus();

                _dcService.ChangeDevice();
                _dcService.ChangeHead();

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

        readonly DeviceConfigService _dcService;
        readonly ClsDevice _clsDevice;
        readonly ClsHead _clsHead;
        private SystemConfigView()
        {
            InitializeComponent();
            if (DesignerProperties.GetIsInDesignMode(this)) return;
            this.Owner = App.Instance.shellView;

            //
            _dcService = App.Instance.dcService;
            _clsDevice = _dcService._clsDevice;
            _clsHead = _dcService._clsHead;

            pgDevice.SelectedObject = _clsDevice;
            pgHead.SelectedObject = _clsHead;
        }

        yoseen.YoseenLoginInfo _loginInfo;
        yoseen.CameraBasicInfo _basicInfo;
        void btnRemoteConfig_Click(object sender, RoutedEventArgs e)
        {
            btnRemoteConfig.IsEnabled = false;
            _loginInfo.CameraAddr = _clsDevice.IR_CameraIp;
            _loginInfo.Username = "";
            _loginInfo.Password = "";
            Task task = Task.Factory.StartNew(() =>
            {
                int ret = yoseen.YoseenSDK.Yoseen_Login(ref _loginInfo, ref _basicInfo);
                return ret;
            }).ContinueWith(x =>
            {
                int userHandle = x.Result;
                btnRemoteConfig.IsEnabled = true;
                if (userHandle >= 0)
                {
                    RemoteConfigView.Instance.Owner = App.Instance.shellView;
                    RemoteConfigView.Instance.ShowConfig(userHandle, ref _basicInfo);
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        void btnRemoteDebug_Click(object sender, RoutedEventArgs e)
        {
            btnRemoteDebug.IsEnabled = false;
            _loginInfo.CameraAddr = _clsDevice.IR_CameraIp;
            _loginInfo.Username = "";
            _loginInfo.Password = "";
            Task task = Task.Factory.StartNew(() =>
            {
                int ret = yoseen.YoseenSDK.Yoseen_Login(ref _loginInfo, ref _basicInfo);
                return ret;
            }).ContinueWith(x =>
            {
                int userHandle = x.Result;
                btnRemoteDebug.IsEnabled = true;
                if (userHandle >= 0)
                {
                    RemoteDebugView.Instance.Owner = App.Instance.shellView;
                    RemoteDebugView.Instance.ShowDebug(userHandle, ref _basicInfo);
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void btnSearchDevice_Click(object sender, RoutedEventArgs e)
        {
            btnSearchDevice.IsEnabled = false;
            Task task = Task.Factory.StartNew(() =>
            {
                yoseen.DiscoverCameraResp2[] dcrs = yoseen.YoseenSDK.Yoseen_DiscoverCameras2(0x05);
                string[] viss = Onvif.DiscoverDevices();
                string msg = "";
                foreach (yoseen.DiscoverCameraResp2 dcr in dcrs)
                {
                    msg += string.Format("IR, {0}\n", yoseen.YoseenUtil.uint2str(dcr.CameraIp));
                }
                foreach (string vis in viss)
                {
                    msg += string.Format("VIS, {0}\n", vis);
                }

                return msg;
            }).ContinueWith(x =>
            {
                if (x.Result != "")
                {
                    MessageBox.Show(x.Result);
                }
                btnSearchDevice.IsEnabled = true;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}
