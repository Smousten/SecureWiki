using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using DynamicData;
using SecureWiki.MediaWiki;
using SecureWiki.Model;
using SecureWiki.Utilities;
using SecureWiki.ViewModels;

namespace SecureWiki.Views
{
    public class MainWindow : Window
    {
        private MasterKeyring _masterKeyring = new();
        private MountedDirMirror _mountedDirMirror = new();
        private Manager manager;
        public MainWindowViewModel _viewModel;
        public Logger logger = new();
        private bool autoscrollLogger = true;

        public static AutoResetEvent ManagerReadyEvent = new(false);
        
        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainWindowViewModel(_masterKeyring, logger, _mountedDirMirror);
            DataContext = _viewModel;
            
            // Check if fuse is already running, if yes then unmount
            IsFuseRunning();
            
            manager = new Manager(Thread.CurrentThread, _masterKeyring, logger, _mountedDirMirror);
            Thread managerThread = new(manager.Run) {IsBackground = true, Name = "ManagerThread"};
            managerThread.Start();

            // Do not show GUI window until manager is ready to handle requests
            // ManagerReadyEvent.WaitOne();
            



#if DEBUG
            this.AttachDevTools();
#endif
        }



        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            Console.WriteLine();
            Console.WriteLine("Window is closing");

            Console.WriteLine("Cleaning cache and saving to file");
            manager.CleanCache();
            manager.SaveCacheManagerToFile();
            Console.WriteLine("Saving config manager to file");
            manager.SaveConfigManagerToFile();
            Console.WriteLine("Saving keyring to file");
            // manager.SaveKeyringToFile();
            // Console.WriteLine("Saving contacts to file");
            // manager.SaveContactManagerToFile();
            Console.WriteLine("Saving master symref to file and uploading");
            manager.SaveSymRefMasterKeyringToFile();
            // Unmount mounted directory
            Unmount();

            // Remove files in root directory
            var rootdirPath = GetPathToDirectory("rootdir");
            var rootdirInfo = new DirectoryInfo(rootdirPath);
            
            foreach (FileInfo file in rootdirInfo.GetFiles())
            {
                file.Delete(); 
            }
            foreach (DirectoryInfo dir in rootdirInfo.GetDirectories())
            {
                dir.Delete(true); 
            }
        }
        
        // Get Path to fuse directory
        private static string GetPathToDirectory(string directory)
        {
            var currentDir = Directory.GetCurrentDirectory();
            var baseDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
            var outputPath = Path.Combine(baseDir, @"fuse/directories", @directory);
            return outputPath;
        }

        // Start process to unmount fuse mounted directory
        private static void Unmount()
        {
            var mountdirPath = GetPathToDirectory("mountdir");
            ProcessStartInfo start = new();
            start.FileName = "/bin/fusermount";
            start.Arguments = $"-u {mountdirPath}";

            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            var process = Process.Start(start);
            process?.WaitForExit();
            process?.Close();
        }
        
        // If mountdir is already mounted, then unmount
        private static void IsFuseRunning()
        {
            var mountdirPath = GetPathToDirectory("mountdir");
            var allDrives = DriveInfo.GetDrives().ToList();
            if (allDrives.Any(d => d.Name.Equals(mountdirPath)))
            {
                Console.WriteLine("fuse already running... Unmounting...");
                Unmount();
            }
        }

        private void MainWindow_Shown(object sender, EventArgs e)
        {
            ManagerReadyEvent.WaitOne();
            Console.WriteLine("MainWindow_Shown");
            
            // Expand root node in TreeView
            TreeView TV = this.FindControl<TreeView>("TreeView1");
            if (TV.GetLogicalChildren().Any())
            {
                var first = TV.GetLogicalChildren().First(c => c.GetType() == typeof(TreeViewItem));
                TreeViewItem root = (TreeViewItem) first;
                root.IsExpanded = true;
            }
        }

        public void Button1_Click(object sender, RoutedEventArgs e)
        {
            // Console.WriteLine("nothing happened");
            logger.Add("loka", "connetent");
            // manager.TestDownload();
            // manager.TestDownloadInboxes();
            // manager.TestIfPageExists();
            Console.WriteLine("_mountedDirMirror.PrintInfo();");
            _mountedDirMirror.PrintInfo();
            
            Console.WriteLine("_masterKeyring.PrintInfoRecursively();");
            _masterKeyring.PrintInfoRecursively();
        }

        private void Button2_Click(object? sender, RoutedEventArgs e)
        {
            _masterKeyring.PrintInfoRecursively();
        }

        private void ButtonExport_Click(object? sender, RoutedEventArgs e)
        {
            // manager.ExportKeyring();
        }
        
        private void ButtonShareKeyring_Click(object? sender, RoutedEventArgs e)
        {
            // Thread localThread = new(() =>
            //     manager.ShareSelectedKeyring(_viewModel.SelectedShareContacts.ToList()));
            // localThread.Start();
            
            var popup = this.FindControl<Popup>("ShareKeyringPopup");
            popup.IsOpen = false;
        }

        private void ButtonImport_Click(object? sender, RoutedEventArgs e)
        {
            ImportKeyring();
        }

        private async void ImportKeyring()
        {
            var path = await OpenFileDialogAndGetFilePath();

            if (path != null)
            {
                // manager.ImportKeyring(path);
            }
            else
            {
                Console.WriteLine("No path given from FileDialog");
            }
        }

        private async Task<string?> OpenFileDialogAndGetFilePath()
        {
            OpenFileDialog dialog = new();
            dialog.Filters.Add(new FileDialogFilter() {Extensions = {"json"}});

            var output = await dialog.ShowAsync(this);

            // Return first file chosen, if any exist
            if (output != null && output.Length > 0)
            {
                return output[0];
            }

            return null;
        }


        /*
        private void InitCheckBoxHandlers(TreeViewItem root)
        {
            foreach (TreeViewItem TVI in root.GetLogicalChildren().Where(c => c.GetType() == typeof(TreeViewItem)))
            {
                // Console.WriteLine("Child found");
                CheckBox cb = (CheckBox) TVI.GetLogicalChildren()
                    .First(c => c.GetType() == typeof(CheckBox));
                SetCheckBoxCheckedUncheckedEventHandlers(TVI, cb);

                // Check own children
                if (TVI.DataContext.GetType() != typeof(DataFileEntry))
                {
                    InitCheckBoxHandlers(TVI);
                }
            }
        }
        */

        private void CheckBox_OnInitialized(object? sender, EventArgs e)
        {
        }

        private TreeViewItem GetTreeViewItemParent(Control item)
        {
            var parent = item.Parent;

            while (!(parent is TreeViewItem))
            {
                if (parent is TreeView)
                {
                    Console.WriteLine("Error: Parent is treeview");
                    throw new Exception();
                }

                parent = parent.Parent;
            }

            return (TreeViewItem) parent;
        }

        private void ButtonIP_Click(object? sender, RoutedEventArgs e)
        {
            var textBox = this.FindControl<TextBox>("TextBoxIp");
            var ip = textBox.Text;
            // var url = $"http://{ip}/mediawiki/api.php";
            manager.SetDefaultServerLink(ip);
        }
        
        private void SelectedRevisionButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (_viewModel.selectedRevision.revisionID == null) return;

            _viewModel.selectedFile.newestRevisionSelected = false; // IsNewestRevision();
            _viewModel.selectedFileRevision = _viewModel.selectedRevision.revisionID;

            manager.UpdateRequestedRevision(_viewModel.selectedFile.AccessFileReference.targetPageName, _viewModel.selectedFile.AccessFileReference.serverLink, _viewModel.selectedRevision.revisionID);
        }

        private void DefaultRevisionButton_OnClick(object? sender, RoutedEventArgs e)
        {
            // Remove pageName from dictionary of requested revisions
            manager.UpdateRequestedRevision(_viewModel.selectedFile.AccessFileReference.targetPageName, _viewModel.selectedFile.AccessFileReference.serverLink, null);
            _viewModel.selectedFileRevision = "Newest";

            _viewModel.selectedFile.newestRevisionSelected = true;
        }

        private void InputElement_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is TextBlock tb)
            {
                AccessFile accessFile = (tb.DataContext as MDFile)?.symmetricReference.targetAccessFile ?? throw new InvalidOperationException();
                _viewModel.selectedFile = accessFile;

                Thread localThread = new(() =>
                    manager.UpdateAllRevisionsAsync(accessFile.AccessFileReference.targetPageName, accessFile.AccessFileReference.serverLink, _viewModel.revisions));
                localThread.Start();

                // Get requested revision, if any
                _viewModel.selectedFileRevision = manager.GetRequestedRevision(accessFile.AccessFileReference.targetPageName, accessFile.AccessFileReference.serverLink) ?? "Newest";
            }
        }

        private bool IsNewestRevision()
        {
            var newestSelected = true;

            if (_viewModel.selectedRevision.revisionID == null) return newestSelected;

            var selectedRevID = int.Parse(_viewModel.selectedRevision.revisionID);
            foreach (Revision item in _viewModel.revisions)
            {
                if (item.revisionID != null)
                {
                    var itemID = int.Parse(item.revisionID);
                    if (itemID > selectedRevID)
                    {
                        newestSelected = false;
                    }
                }
                else
                {
                    Console.WriteLine("SelectedRevisionButton_OnClick: item.revisionID == null");
                }
            }

            return newestSelected;
        }

        private void HideButtonPopup_Click(object? sender, RoutedEventArgs e)
        {
            var tag = (string) ((Button) sender!).Tag;
            var popup = this.FindControl<Popup>(tag);

            if (tag.Equals("GenerateContactPopup"))
            {
                _viewModel.NicknamePopUp = "";
                _viewModel.ServerLinkPopUp = "";
            }
            
            popup.IsOpen = false;
        }

        private void ShowButtonPopup_Click(object? sender, RoutedEventArgs e)
        {
            var tag = (string) ((Button) sender!).Tag;
            var popup = this.FindControl<Popup>(tag);

            if (tag.Equals("ExportContactsPopup"))
            {
                Thread localThread = new(() =>
                    manager.GetAllContacts(_viewModel.ExportContactsOwn, _viewModel.ExportContactsOther));
                localThread.Start();
            }

            if (tag.Equals("RevokeAccessPopup"))
            {
                // Thread localThread = new(() =>
                //     manager.GetFileContacts(_viewModel.RevokeContacts, _viewModel.selectedFile));
                // localThread.Start();
            }
            
            if (tag.Equals("ShareKeyringPopup"))
            {
                // Thread localThread = new(() =>
                //     manager.GetOtherContacts(_viewModel.ShareContacts));
                // localThread.Start();
            }
            
            if (tag.Equals("AddToKeyringPopup"))
            {
                Thread localThread = new(() =>
                    manager.GetKeyrings(_viewModel.keyrings));
                localThread.Start();
            }
            
            popup.IsOpen = true;
        }

        private void Revoke_Click(object? sender, RoutedEventArgs e)
        {
            var datafile = _viewModel.selectedFile;

            // Thread localThread = new(() =>
            //     manager.RevokeAccess(datafile, _viewModel.SelectedRevokeContacts));
            // localThread.Start();

            var popup = this.FindControl<Popup>("RevokeAccessPopup");
            popup.IsOpen = false;
        }

        private void CacheSettingButton_Click(object? sender, RoutedEventArgs e)
        {
            var button = (Button) sender!;
            var tag = (string) button.Tag;
            var name = button.Name;
            var content = name?.Substring("CacheSettingPopup".Length);

            var popup = this.FindControl<Popup>(tag);
            popup.IsOpen = false;

            // If setting is null, the entry is removed from the exception list
            CachePreferences.CacheSetting? setting;
            if (name != null && name.Equals("CacheSettingPopupDefault"))
            {
                setting = null;
            }
            else
            {
                // Get setting chosen
                setting = (CachePreferences.CacheSetting) Enum.Parse(typeof(CachePreferences.CacheSetting), content);
            }

            manager.SetCacheSettingSingleFile(_viewModel.selectedFile.AccessFileReference.targetPageName, setting);
        }

        private void CacheSettingPopup_OnOpened(object? sender, EventArgs e)
        {
            var popup = (Popup) sender!;

            // Reset color of the other buttons
            var gridChildren = popup.FindLogicalDescendantOfType<Grid>().FindLogicalDescendantOfType<Grid>()
                .GetLogicalChildren();
            foreach (var item in gridChildren)
            {
                if (item.GetType() == typeof(Button) && ((Button) item).Name != null)
                {
                    ((Button) item).Background = Brushes.LightBlue;
                }
            }

            var setting = manager.GetCacheSettingSingleFile(_viewModel.selectedFile.AccessFileReference.targetPageName);

            const string buttonNameCommon = "CacheSettingPopup";
            string buttonName = setting != null ? buttonNameCommon + setting : buttonNameCommon + "Default";

            Button button = popup.FindControl<Button>(buttonName);

            if (button != null)
            {
                button.Background = Brushes.LightSteelBlue;
            }
        }

        private void ScrollViewerItemsRepeaterLog_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = (ScrollViewer) sender!;

            // If entries have not been updated
            if (e.ExtentDelta.Y == 0)
            {
                autoscrollLogger =
                    Math.Abs(scrollViewer.Extent.Height - scrollViewer.Bounds.Height - scrollViewer.Offset.Y) < 5;
            }

            // If autoscroll is on and entries have been updated
            if (autoscrollLogger && e.ExtentDelta.Y != 0)
            {
                scrollViewer.ScrollToEnd();
            }
        }

        private void ButtonGenerateContact_Click(object? sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }
        

        private void ButtonImportContact_Click(object? sender, RoutedEventArgs e)
        {
            ImportContact();
        }

        private async void ImportContact()
        {
            var path = await OpenFileDialogAndGetFilePath();

            if (path != null)
            {
                manager.ImportContact(path);
            }
            else
            {
                Console.WriteLine("No path given from FileDialog");
            }
        }

        private void GenerateContactPopup_Click(object? sender, RoutedEventArgs e)
        {
            var serverLinkTextBox = this.FindControl<TextBox>("ServerLinkTextBox");
            var serverLink = serverLinkTextBox.Text;

            var nicknameTextBox = this.FindControl<TextBox>("NicknameTextBox");
            var nickname = nicknameTextBox.Text;

            if (serverLink != null && nickname != null)
            {
                // Thread localThread = new(() =>
                //     manager.GenerateOwnContact(serverLink, nickname));
                // localThread.Start();
            }

            var popup = this.FindControl<Popup>("GenerateContactPopup");

            _viewModel.NicknamePopUp = "";
            _viewModel.ServerLinkPopUp = "";

            popup.IsOpen = false;
        }
        
        private void ExportContactsPopup_Click(object? sender, RoutedEventArgs e)
        {
            // var exportContacts = _viewModel.SelectedExportContactsOwn;
            var exportContacts = new ObservableCollection<Contact>();
            exportContacts.AddRange(_viewModel.SelectedExportContactsOwn);
            exportContacts.AddRange(_viewModel.SelectedExportContactsOther);
            
            if (exportContacts.Count > 0)
            {
                Thread localThread = new(() =>
                    manager.ExportContacts(exportContacts));
                localThread.Start();
            }
            
            var popup = this.FindControl<Popup>("ExportContactsPopup");
            popup.IsOpen = false;
        }


        private void UpdateInboxes_OnClick(object? sender, RoutedEventArgs e)
        {
            Thread localThread = new(() =>
                manager.ForceUpdateFromAllInboxPages());
            localThread.Start();
        }

        private void ButtonAddToKeyring_Click(object? sender, RoutedEventArgs e)
        {
            Thread localThread = new(() =>
                manager.AddFilesToKeyring(_viewModel.selectedKeyrings.ToList()));
            localThread.Start();
            
            var popup = this.FindControl<Popup>("AddToKeyringPopup");
            popup.IsOpen = false;
        }

        // private void ButtonExportContact_Click(object? sender, RoutedEventArgs e)
        // {
        //     Thread localThread = new(() =>
        //         manager.ExportContact());
        //     localThread.Start();
        // }
    }
}