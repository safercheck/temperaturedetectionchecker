using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using yoseen = YoseenSDKCS;
using OnvifClient;

using IRTool.CoreDef;
using IRTool.ResourceDef;
using IRTool.FDCanvasDef;
using System.ComponentModel;
using System.Diagnostics;

namespace IRTool.Views
{
    public partial class SystemControlView : UserControl
    {
        readonly YoseenWarden _warden;
        readonly Onvif _onvifDevice;
        readonly FDCanvas _fdCanvas;

        public SystemControlView()
        {
            InitializeComponent();
            if (DesignerProperties.GetIsInDesignMode(this)) return;

            _warden = App.Instance.warden;
            _onvifDevice = _warden.visDevice;
            _fdCanvas = App.Instance.fdCanvas;

            //
            _ptzSpeed = 1;
            //sliderPtzSpeed.Value = _ptzSpeed;
        }

        private void btnSaveJpg_Click(object sender, RoutedEventArgs e)
        {
            DateTime dt = DateTime.Now;
            string filename = string.Format("{0}\\{1:yyyyMMdd_HHmmss}.jpg", AppStatic.DataManual, dt);

            //
            _warden._structFFH.captured_time = dt.ToFileTimeUtc();
            int ret = _fdCanvas.SaveFileWithTemp(filename, ref _warden._structFFH);
            txtEcho.Text = string.Format("{0}, {1}, {2}", Loc.Preview_SaveJpg, filename, ret);
        }

        bool _savingMp4;
        string _savingMp4Filename;
        string _savingMp4FilenameVIS;
        private void btnSaveMp4_Click(object sender, RoutedEventArgs e)
        {
            if (_savingMp4)
            {
                _warden.EndSaveMp4();
                btnSaveMp4.IsChecked = false;
                _savingMp4 = false;
                txtEcho.Text = string.Format("{0}, {1}", Loc.Preview_SaveMp4, _savingMp4Filename);
            }
            else
            {
                DateTime dt = DateTime.Now;
                _savingMp4Filename = string.Format("{0}\\{1:yyyyMMdd_HHmmss}.mp4", AppStatic.DataManual, dt);
                _savingMp4FilenameVIS = string.Format("{0}\\{1:yyyyMMdd_HHmmss}V.mp4", AppStatic.DataManual, dt);
                int ret = _warden.BeginSaveMp4(_savingMp4Filename, _savingMp4FilenameVIS);
                if (0 == ret)
                {
                    btnSaveMp4.IsChecked = true;
                    _savingMp4 = true;
                }

                txtEcho.Text = string.Format("{0}, {1}, {2}", Loc.Preview_SaveMp4, _savingMp4Filename, ret);
            }
        }

        yoseen.Ctl _ctl;
        private void btnManualFFC_Click(object sender, RoutedEventArgs e)
        {
            _ctl.Type = yoseen.CtlType.CtlType_ManualFFC;
            _warden.SendControl(ref _ctl);
        }

        private void btnEnableMouseMeasure_Click(object sender, RoutedEventArgs e)
        {
            bool? isChecked = btnEnableMouseMeasure.IsChecked;
            if (isChecked.HasValue && isChecked.Value)
            {
                _fdCanvas.EnableMouseMeasure = true;
            }
            else
            {
                _fdCanvas.EnableMouseMeasure = false;
            }
        }

        #region ptz, vis
        byte _ptzSpeed;
        //private void sliderPtzSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        //{
        //    _ptzSpeed = (byte)sliderPtzSpeed.Value;
        //}

        private void ptz_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Button btn = (sender as Button);
            string ptz = btn.Tag as string;
            switch (ptz)
            {
                //vis: pan, tilt, zoom
                case "ZoomOut":
                    _onvifDevice.Ptz_ContinuousMove(0, 0, -_ptzSpeed);
                    break;
                case "ZoomIn":
                    _onvifDevice.Ptz_ContinuousMove(0, 0, _ptzSpeed);
                    break;

                case "FocusNear":
                    _onvifDevice.FocusStart(-1, 2);
                    break;
                case "FocusFar":
                    _onvifDevice.FocusStart(1, 2);
                    break;

                //
                default:
                    goto error_handled;
            }

        error_handled:
            e.Handled = true;
        }

        private void ptz_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Button btn = (sender as Button);
            string ptz = btn.Tag as string;
            switch (ptz)
            {
                //
                case "ZoomOut":
                    _onvifDevice.Ptz_Stop(false, true);
                    break;
                case "ZoomIn":
                    _onvifDevice.Ptz_Stop(false, true);
                    break;

                case "FocusNear":
                    _onvifDevice.FocusStop();
                    break;
                case "FocusFar":
                    _onvifDevice.FocusStop();
                    break;

                //
                default:
                    goto error_handled;
            }

        error_handled:
            e.Handled = true;
        }

        #endregion




    }

    public class DiscoverInfo
    {
        public string CameraType;
        public string CameraIP;

        public DiscoverInfo(string cameraType, string cameraIP)
        {
            CameraType = cameraType;
            CameraIP = cameraIP;
        }
    }
}
