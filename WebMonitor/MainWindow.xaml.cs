using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WebMonitor
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private int itemNo = 1;
        private FileSystemWatcher fileWatcher = new FileSystemWatcher();
        private DispatcherTimer disTimer = new DispatcherTimer();
        private int totalSecond = 0;

        public MainWindow()
        {
            InitializeComponent();

            // 对话框基本设置
            MetroDialogOptions.AffirmativeButtonText = "确定";
            MetroDialogOptions.NegativeButtonText = "取消";
            MetroDialogOptions.ColorScheme = MetroDialogColorScheme.Accented;

            // 监控时长
            disTimer.Interval = TimeSpan.FromMilliseconds(1000);
            disTimer.Tick += DisTimer_Tick;
            disTimer.Start();
        }

        #region 事件

        // 定时器事件
        private void DisTimer_Tick(object sender, EventArgs e)
        {
            totalSecond++;

            int hour = totalSecond / 3600,
                minute = (totalSecond % 3600) / 60,
                second = (totalSecond % 3600) % 60;
            siMonitorTime.Text = string.Format("{0}小时{1}分钟{2}秒", hour, minute, second);
        }

        // 双击选择web目录
        private void txtWebPath_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                System.Windows.Forms.FolderBrowserDialog folderDialog = new System.Windows.Forms.FolderBrowserDialog();
                folderDialog.Description = "选择网站根目录";
                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtWebPath.Text = folderDialog.SelectedPath;

                    dgMonitor.ItemsSource = null;
                    itemNo = 1;
                }
            }
        }

        // 监控
        private async void btnTask_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtWebPath.Text))
            {
                await this.ShowMessageAsync("Web监控", "请选择web目录", MessageDialogStyle.Affirmative);
                return;
            }

            switch (btnTask.Content.ToString())
            {
                case "开始监控":
                    await OperateMonitor(txtWebPath.Text, (bool)chkMonitorChild.IsChecked);
                    btnTask.Content = "停止监控";
                    txtWebPath.IsEnabled = false;
                    chkMonitorChild.IsEnabled = false;
                    mpbStatus.Visibility = Visibility.Visible;
                    break;
                case "停止监控":
                    fileWatcher.EnableRaisingEvents = false;
                    fileWatcher.Created -= FileWatcher_Created;
                    fileWatcher.Deleted -= FileWatcher_Deleted;
                    fileWatcher.Changed -= FileWatcher_Changed;
                    fileWatcher.Renamed -= FileWatcher_Renamed;

                    btnTask.Content = "开始监控";
                    txtWebPath.IsEnabled = true;
                    chkMonitorChild.IsEnabled = true;
                    mpbStatus.Visibility = Visibility.Hidden;
                    break;
            }
        }

        // 行加载，根据文件动作设置字体颜色，并滚动到最底部

        private void dgMonitor_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            dynamic item = e.Row.Item;

            if (item.Count > 0)
            {
                switch ((WatcherChangeTypes)item[0].OperateType)
                {
                    case WatcherChangeTypes.Created:
                        e.Row.Foreground = new SolidColorBrush(Colors.Green);
                        break;
                    case WatcherChangeTypes.Deleted:
                        e.Row.Foreground = new SolidColorBrush(Colors.Red);
                        break;
                    case WatcherChangeTypes.Changed:
                        e.Row.Foreground = new SolidColorBrush(Colors.Orange);
                        break;
                    case WatcherChangeTypes.Renamed:
                        e.Row.Foreground = new SolidColorBrush(Colors.Black);
                        break;
                }
                dgMonitor.ScrollIntoView(item); // 设置滚动条的位置
            }
        }

        // 清空列表
        private void miClear_Click(object sender, RoutedEventArgs e)
        {
            dgMonitor.Items.Clear();
            itemNo = 1;
        }

        #endregion

        #region 命令事件

        private void Open_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var items = dgMonitor.SelectedItems;

            if (items.Count == 1)
            {
                e.CanExecute = true;
                return;
            }

            e.CanExecute = false;
        }

        private async void Open_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            dynamic items = dgMonitor.SelectedItem;

            if (items.Count == 1)
            {
                await OpenFileLocal(items[0].FilePath);
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 注册文件监控事件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="isMonitorChild"></param>
        /// <returns></returns>
        private Task OperateMonitor(string path, bool isMonitorChild)
        {
            return Task.Factory.StartNew(() =>
            {
                Thread.Sleep(100);

                fileWatcher.Path = path;
                fileWatcher.IncludeSubdirectories = isMonitorChild;
                fileWatcher.EnableRaisingEvents = true;
                fileWatcher.Created += FileWatcher_Created;
                fileWatcher.Deleted += FileWatcher_Deleted;
                fileWatcher.Changed += FileWatcher_Changed;
                fileWatcher.Renamed += FileWatcher_Renamed;
            });
        }

        // 重命名
        private async void FileWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            await GetMonitors(e);
        }

        // 修改
        private async void FileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            await GetMonitors(e);
        }

        // 删除
        private async void FileWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            await GetMonitors(e);
        }

        // 创建
        private async void FileWatcher_Created(object sender, FileSystemEventArgs e)
        {
            await GetMonitors(e);
        }

        private async Task GetMonitors(dynamic fileSystem)
        {
            await dgMonitor.Dispatcher.InvokeAsync(async () =>
             {
                 var lstItem = new List<dynamic>();

                 string newFileName = fileSystem.Name;
                 var newFileArr = newFileName.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                 newFileName = newFileArr[newFileArr.Length - 1];

                 if (fileSystem.GetType().Name == "RenamedEventArgs")
                 {
                     string oldFileName = fileSystem.OldName;
                     var oldFileArr = oldFileName.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                     oldFileName = oldFileArr[oldFileArr.Length - 1];
                     newFileName = string.Format("{0}→{1}", oldFileName, newFileName);
                 }

                 string chkContent = await GetChkOperateItem(),
                   OperateName = await GetOperateType(fileSystem.ChangeType);

                 if (chkContent.Contains(OperateName)) // 选中的动作
                 {
                     lstItem.Add(new
                     {
                         ItemNo = itemNo++,
                         FileName = newFileName,
                         FilePath = fileSystem.FullPath,
                         OperateType = fileSystem.ChangeType,
                         Operate = OperateName,
                         OperaDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                     });

                     dgMonitor.Items.Add(lstItem);
                 }
             });
        }

        /// <summary>
        /// 获取文件更改类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private Task<string> GetOperateType(WatcherChangeTypes type)
        {
            return Task.Factory.StartNew(() =>
            {
                Thread.Sleep(100);

                switch (type)
                {
                    case WatcherChangeTypes.Created:
                        return "创建";
                    case WatcherChangeTypes.Deleted:
                        return "删除";
                    case WatcherChangeTypes.Changed:
                        return "修改";
                    case WatcherChangeTypes.Renamed:
                        return "重命名";
                    default:
                        return "未知";
                }
            });
        }

        /// <summary>
        /// 打开文件所在位置
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private Task OpenFileLocal(string path)
        {
            return Task.Factory.StartNew(() =>
            {
                Thread.Sleep(100);

                ProcessStartInfo startInfo = new ProcessStartInfo("explorer");
                startInfo.Arguments = string.Format("/e,/select,{0}", path);
                Process.Start(startInfo);
            });
        }

        /// <summary>
        /// 获取选中的动作
        /// </summary>
        /// <returns></returns>
        private async Task<string> GetChkOperateItem()
        {
            string chkContent = "";

            foreach (StatusBarItem siItem in statusChkItem.Items)
            {
                await siItem.Dispatcher.InvokeAsync(() =>
                {
                    var childItem = siItem.Content;

                    if (childItem is CheckBox)
                    {
                        CheckBox chkItem = childItem as CheckBox;

                        if ((bool)chkItem.IsChecked)
                        {
                            chkContent += chkItem.Content;
                        }
                    }
                });

            }

            return chkContent;
        }

        #endregion
    }
}
