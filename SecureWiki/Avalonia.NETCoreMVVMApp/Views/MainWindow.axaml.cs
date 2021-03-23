using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;
using DynamicData;
using SecureWiki.ClientApplication;
using SecureWiki.Cryptography;
using SecureWiki.MediaWiki;
using SecureWiki.Model;
using SecureWiki.ViewModels;
using Brushes = Avalonia.Media.Brushes;

namespace SecureWiki.Views
{
    public class MainWindow : Window
    {
        
        private WikiHandler wikiHandler;
        private Keyring _keyring;
        private RootKeyring _rootKeyring = new();
        private readonly object rootKeyringLock = new();
        private TCPListener tcpListener;
        private Manager manager;
        public MainWindowViewModel _viewModel;
        public List<EventHandler<RoutedEventArgs>> CheckBoxEventHandlers = new();
        
        
        
        public MainWindow()
        {
            // Populate global list of CheckBox event handlers
            // CheckBoxEventHandlers.Add(CheckBox_CheckedChangedUpdateParent);
            // CheckBoxEventHandlers.Add(CheckBox_CheckedChangedUpdateChildren);
            
            _viewModel = new(_rootKeyring);
            DataContext = _viewModel;
            InitializeComponent();
            
            manager = new(Thread.CurrentThread, _rootKeyring);
            Thread ManagerThread = new(manager.Run);
            ManagerThread.IsBackground = true;
            ManagerThread.Name = "ManagerThread";
            ManagerThread.Start();

            
            //Console.WriteLine(AppDomain.CurrentDomain.BaseDirectory);
            
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
            
            manager.CleanCache();
            manager.SaveCacheManagerToFile();
            
            
            var currentDir = Directory.GetCurrentDirectory();
            var baseDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
            var mountdirPath = Path.Combine(baseDir, @"fuse/directories/mountdir");
            ProcessStartInfo start = new();
            start.FileName = "/bin/fusermount";
            start.Arguments = $"-u {mountdirPath}";

            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            var process = Process.Start(start);
            process?.WaitForExit();
            process?.Close();
        }

        private void MainWindow_Shown(object sender, EventArgs e)
        {
            Console.WriteLine("MainWindow_Shown");
            
            
            

            // Expand root node in TreeView
            TreeView TV = this.FindControl<TreeView>("TreeView1");
            TreeViewItem root = (TreeViewItem) TV.GetLogicalChildren().First(c => c.GetType() == typeof(TreeViewItem));
            root.IsExpanded = true;
        }

        public void Button1_Click(object sender, RoutedEventArgs e)
        {
            
            TreeView TV = this.FindControl<TreeView>("TreeView1");
            Console.WriteLine("TV.name: " + TV.Name);
            Console.WriteLine("Tv.itemcount: " + TV.ItemCount);

            foreach (var child in TV.GetLogicalChildren())
            {
                Console.WriteLine("child name, type: {0} {1}", child.ToString(), child.GetType() );
                
                foreach (var subChild in child.GetLogicalChildren())
                {
                    Console.WriteLine("subchild name, type: {0} {1}", subChild.ToString(), subChild.GetType() );
                }
                
                // KeyringEntry kr = new();
                // AddToKeyringRecursively(kr, (TreeViewItem) child);
            }         
        }

        private void Button2_Click(object? sender, RoutedEventArgs e)
        {
            _rootKeyring.PrintInfoRecursively();
        }

        private void ButtonExport_Click(object? sender, RoutedEventArgs e)
        {
            manager.ExportKeyring();
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
                manager.ImportKeyring(path);
            }
            else
            {
                Console.WriteLine("No path given from FileDialog");
            }
        }

        private async Task<string?> OpenFileDialogAndGetFilePath()
        {
            OpenFileDialog dialog = new();
            dialog.Filters.Add(new FileDialogFilter() { Extensions =  { "json" } });

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
            manager.SetNewMediaWikiServer(ip);
        }

        private void ButtonLogin_Click(object? sender, RoutedEventArgs e)
        {
            var textBoxUser = this.FindControl<TextBox>("TextBoxUser");
            var username = textBoxUser.Text;
            
            var textBoxPass = this.FindControl<TextBox>("TextBoxPass");
            var password = textBoxPass.Text;

            manager.LoginToMediaWiki(username, password);
        }

        private void ButtonMail(object? sender, RoutedEventArgs e)
        {
            var textBoxMail = this.FindControl<TextBox>("TextBoxMail");
            var recipientEmail = textBoxMail.Text;
            manager.SendEmail(recipientEmail);
        }
        
        private void SelectedRevisionButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (_viewModel.selectedRevision.revisionID != null)
            {
                Console.WriteLine(_viewModel.selectedRevision.revisionID);

                bool newestSelected = true;
                int selectedRevID = Int32.Parse(_viewModel.selectedRevision.revisionID);
                foreach (Revision item in _viewModel.revisions)
                {
                    int itemID = Int32.Parse(item.revisionID);
                    if (itemID > selectedRevID)
                    {
                        newestSelected = false;
                    }
                }
                
                _viewModel.selectedFile.newestRevisionSelected = newestSelected;
                
                if (manager.RequestedRevision.ContainsKey(_viewModel.selectedFile.pageName))
                {
                    manager.RequestedRevision[_viewModel.selectedFile.pageName] = _viewModel.selectedRevision.revisionID;
                }
                else
                {
                    manager.RequestedRevision.Add(_viewModel.selectedFile.pageName, _viewModel.selectedRevision.revisionID);
                }
            }
        }   

        private void InputElement_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Console.WriteLine("InputElement_OnPointerPressed by" + sender);
            
            if (sender is TextBlock tb)
            {
                DataFileEntry dataFile = tb.DataContext as DataFileEntry ?? throw new InvalidOperationException();
                _viewModel.selectedFile = dataFile;
                _viewModel.revisions = manager.GetAllRevisions(dataFile.pageName).revisionList;
                // var allRevisions = manager.GetAllRevisions(dataFile.pagename);
                // _viewModel.revisions = new ObservableCollection<Revision>(allRevisions.revisionList);
                Console.WriteLine(dataFile.filename);
            }
            
        }

        private void Hide_Click(object? sender, RoutedEventArgs e)
        {
            var popup = this.FindControl<Popup>("RevokeAccessPopup");
            popup.IsOpen = false;
            _viewModel.IsAccessRevocationPopupOpen = false;
        }

        private void Show_Click(object? sender, RoutedEventArgs e)
        {
            var popup = this.FindControl<Popup>("RevokeAccessPopup");
            popup.IsOpen = true;
            _viewModel.IsAccessRevocationPopupOpen = true;
        }

        private void Revoke_Click(object? sender, RoutedEventArgs e)
        {
            var datafile = _viewModel.selectedFile;
            manager.RevokeAccess(datafile);
            
            var popup = this.FindControl<Popup>("RevokeAccessPopup");
            popup.IsOpen = false;
            _viewModel.IsAccessRevocationPopupOpen = false;
        }
    }
}