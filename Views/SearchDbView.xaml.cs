using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Media;

using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using winform = System.Windows.Forms;

using yoseen = YoseenSDKCS;
using WpfBase;
using wpfmedia = WpfMedia;

using IRTool.ResourceDef;
using IRTool.CoreDef;
using IRTool.ConfigDef;
using IRTool.BusiDef;

namespace IRTool.Views
{
    public partial class SearchDbView : Window
    {
        #region Single
        static SearchDbView __instance;
        public static SearchDbView Instance
        {
            get
            {
                if (__instance == null)
                {
                    __instance = new SearchDbView();
                    __instance.Owner = App.Instance.shellView;
                }
                return SearchDbView.__instance;
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
        #endregion

        readonly DeviceConfigService _dcService;
        readonly DataRecordService _serviceRecord;
        readonly FilePager _filePager;

        readonly SearchInfo _searchInfo;
        List<DataRecord> _allRecords;

        SearchDbView()
        {
            InitializeComponent();
            if (DesignerProperties.GetIsInDesignMode(this)) return;

            //
            _dcService = App.Instance.dcService;
            _serviceRecord = App.Instance.dataAccess.ServiceDataRecord;
            _searchInfo = new SearchInfo();
            _allRecords = new List<DataRecord>();

            //
            _filePager = new FilePager();
            pageControl.PageContract = _filePager;

            //
            gridSearch.DataContext = _searchInfo;
            cmbLastTime.ItemsSource = LastTimeInfo.Infos;
            cmbLastTime.SelectedIndex = 0;
        }

        private void lvi_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ListViewItem item = sender as ListViewItem;
        }

        private void pageControl_PreviewPageChange(object sender, PageChangedEventArgs args)
        {
        }

        private void pageControl_PageChanged(object sender, PageChangedEventArgs args)
        {
        }

        private void btnQueryData_Click(object sender, RoutedEventArgs e)
        {
            //
            bool? b = cbAlarmOnly.IsChecked;
            bool alarmOnly = b ?? b.Value;

            //
            LastTimeInfo info = cmbLastTime.SelectedItem as LastTimeInfo;
            DateTime dt = _searchInfo.StartTime;
            DateTime dt2 = _searchInfo.EndTime;
            if (info.Hours > 0)
            {
                dt2 = DateTime.Now;
                dt = dt2.Subtract(TimeSpan.FromHours(info.Hours));
            }
            _allRecords = _serviceRecord.Retrive(dt, dt2, alarmOnly);

            //
            _filePager.InitDir(_allRecords);
            pageControl.RefreshContract();
            txtTotalCount.Text = _allRecords.Count.ToString();
            txtAlarmCount.Text = _allRecords.Count(x => x.TargetIndex < 0).ToString();
        }

        private void btnDeleteData_Click(object sender, RoutedEventArgs e)
        {
            if (_allRecords == null) return;
            int count = _allRecords.Count;
            if (count <= 0) return;

            string msg = string.Format("{0}, {1} ?", Loc.CRUD_Delete, count);
            MessageBoxResult mbr = MessageBox.Show(msg, Loc.CRUD_Delete, MessageBoxButton.YesNo);
            if (mbr != MessageBoxResult.Yes) return;

            //
            _allRecords.ForEach(r =>
            {
                _serviceRecord.Delete(r);
            });
            _allRecords.Clear();

            //
            _filePager.InitDir(_allRecords);
            pageControl.RefreshContract();
            txtTotalCount.Text = "";
            txtAlarmCount.Text = "";
        }
    }

    class FilePager : IPageControlContract, IDisposable
    {
        #region static
        #endregion

        public FilePager()
        {
            _records = new List<DataRecord>();
        }

        bool _isDisposed;
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = false;
            }
        }

        List<DataRecord> _records;

        public void InitDir(List<DataRecord> records)
        {
            try
            {
                _records = records;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        public int GetTotalCount()
        {
            return _records.Count;
        }

        public ICollection<object> GetRecordsBy(int StartingIndex, int NumberOfRecords, object FilterTag)
        {
            List<object> result = new List<object>();
            int i = StartingIndex;
            int iend = StartingIndex + NumberOfRecords;
            if (iend > _records.Count) iend = _records.Count;
            for (; i < iend; i++)
            {
                DataRecord record = _records[i];
                result.Add(record);
            }
            return result;
        }

    }

    class SearchInfo : BaseModel
    {
        DateTime _startTime;
        [PropertyOrder(0), DisplayName(Loc.StartTime), Description("")]
        public DateTime StartTime
        {
            get { return _startTime; }
            set
            {
                if (value != _startTime)
                {
                    _startTime = value;
                    OnPropertyChanged("StartTime");
                }
            }
        }

        DateTime _endTime;
        [PropertyOrder(1), DisplayName(Loc.EndTime), Description("")]
        public DateTime EndTime
        {
            get { return _endTime; }
            set
            {
                if (value != _endTime)
                {
                    _endTime = value;
                    OnPropertyChanged("EndTime");
                }
            }
        }

        public SearchInfo()
        {
            _endTime = DateTime.Now;
            _endTime = new DateTime(_endTime.Year, _endTime.Month, _endTime.Day, 23, 0, 0);
            _startTime = _endTime.Subtract(TimeSpan.FromDays(3));
        }
    }

    class LastTimeInfo : BaseModel
    {
        static LastTimeInfo[] __infos;
        public static LastTimeInfo[] Infos
        {
            get
            {
                if (__infos == null)
                {
                    string hours = Loc.Hour;
                    __infos = new LastTimeInfo[6];
                    __infos[0] = new LastTimeInfo(0, Loc.ManualTime);
                    __infos[1] = new LastTimeInfo(2, string.Format("2 {0}", hours));
                    __infos[2] = new LastTimeInfo(4, string.Format("4 {0}", hours));
                    __infos[3] = new LastTimeInfo(8, string.Format("8 {0}", hours));
                    __infos[4] = new LastTimeInfo(12, string.Format("12 {0}", hours));
                    __infos[5] = new LastTimeInfo(24, string.Format("24 {0}", hours));
                }
                return __infos;
            }
        }

        public int Hours { get; private set; }
        public string Name { get; private set; }

        public LastTimeInfo(int hours, string name)
        {
            Hours = hours;
            Name = name;
        }
    }
}
