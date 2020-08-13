using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using yoseen = YoseenSDKCS;

using IRTool.CoreDef;
using IRTool.ConfigDef;
using IRTool.FDCanvasDef;
using IRTool.ResourceDef;

namespace IRTool.Views
{
    public partial class FDConfigView : Window, IDisposable
    {
        #region Single
        static FDConfigView __instance;
        public static FDConfigView Instance
        {
            get
            {
                if (__instance == null)
                {
                    __instance = new FDConfigView();
                    __instance.Owner = App.Instance.shellView;
                }
                return FDConfigView.__instance;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
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

        bool _isDisposed;
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                yoseen.YoseenPlayback.YoseenPlayback_Free(ref _ptrPlayback);
            }
        }
        #endregion

        readonly ClsDevice _clsDevice;
        IntPtr _ptrPlayback;

        readonly ClsFDConfig _clsConfig;

        readonly ToggleButton[] _toolArray;

        private FDConfigView()
        {
            InitializeComponent();
            if (DesignerProperties.GetIsInDesignMode(this)) return;
            _ptrPlayback = yoseen.YoseenPlayback.YoseenPlayback_Create();
            if (_ptrPlayback == IntPtr.Zero) throw new Exception("YoseenPlayback_Create, fail");
            _clsDevice = App.Instance.dcService._clsDevice;

            //toolbar
            _toolArray = new ToggleButton[(int)ToolType.Max];
            _toolArray[(int)ToolType.Mouse] = btnMouse;
            _toolArray[(int)ToolType.Select] = btnSelect;
            //_toolArray[(int)ToolType.Shield] = btnShield;
            _toolArray[(int)ToolType.Crop] = btnCrop;

            btnMouse.PreviewMouseDown += btnTool_PreviewMouseDown;
            btnSelect.PreviewMouseDown += btnTool_PreviewMouseDown;
            //btnShield.PreviewMouseDown += btnTool_PreviewMouseDown;
            btnCrop.PreviewMouseDown += btnTool_PreviewMouseDown;
            _clsConfig = configCanvas.ClsConfig;

            pgConfig.SelectedObject = _clsConfig;
        }

        void btnTool_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            ToolType toolType = (ToolType)(sender as ToggleButton).Tag;
            configCanvas.Tool = toolType;
            e.Handled = true;

            //
            int toolType2 = (int)toolType;
            int i = 0;
            for (; i < (int)ToolType.Max; i++)
            {
                ToggleButton tb = _toolArray[i];
                if (tb != null)
                {
                    tb.IsChecked = (i == toolType2);
                }
            }
        }

        private void btnSaveAndClose_Click(object sender, RoutedEventArgs e)
        {
            SaveConfig();
            this.Close();
        }
        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveConfig();
        }
        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        void SaveConfig()
        {
            configCanvas.SaveConfig(ref _binConfig);
            _delAfterConfig(ref _binConfig);
        }

        DELAfterFDConfig _delAfterConfig;
        FDConfig _binConfig = FDConfig.Create();
        yoseen.DataFrame _irFrame;
        yoseen.DataFrameHeader _irFrameHead;

        yoseen.DataFrame _visFrame;
        yoseen.H264RtspHeader _visFrameHead;

        public void ShowConfig(ref yoseen.DataFrame irFrame, ref yoseen.DataFrame visFrame,
            ref FDConfig binConfig, DELAfterFDConfig delAfterConfig)
        {
            //
            _delAfterConfig = delAfterConfig;
            _irFrame = irFrame;
            int ret = yoseen.YoseenPlayback.YoseenPlayback_OpenMem(_ptrPlayback, _irFrame.Head, _irFrame.Temp);
            if (0 > ret) return;
            _irFrameHead = (yoseen.DataFrameHeader)Marshal.PtrToStructure(irFrame.Head, typeof(yoseen.DataFrameHeader));

            yoseen.YoseenPlayback.YoseenPlayback_SetImage(_ptrPlayback, ref _clsDevice._bin.IRImage_StrechControl, ref _clsDevice._bin.IRImage_PaletteType);
            ret = yoseen.YoseenPlayback.YoseenPlayback_ReadFrame(_ptrPlayback, 0, ref _irFrame);
            if (ret < 0) return;

            //
            _visFrame = visFrame;
            _visFrameHead = (yoseen.H264RtspHeader)Marshal.PtrToStructure(visFrame.H264, typeof(yoseen.H264RtspHeader));

            /*
             * update data
             */
            configCanvas.BeginUpdate(_irFrameHead.Width, _irFrameHead.Height, 0);
            configCanvas.BeginUpdate(_visFrameHead.width, _visFrameHead.height, 1);

            configCanvas.ChangePalette(_clsDevice._bin.IRImage_PaletteType);
            configCanvas.UpdateIR(ref _irFrame);
            configCanvas.UpdateVIS(ref _visFrame);

            configCanvas.LoadConfig(ref binConfig);
            //
            this.ShowDialog();
        }
    }

    public delegate void DELAfterFDConfig(ref FDConfig binConfig);
}
