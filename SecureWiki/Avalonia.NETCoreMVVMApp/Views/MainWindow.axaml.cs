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
            
            var currentDir = Directory.GetCurrentDirectory();
            var baseDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
            var mountdirPath = Path.Combine(baseDir, @"fuse/example/mountdir");
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
            /*
            //Getting Controls references
            var nameControl = this.FindControl<TextBox>("NameTextBox");
            var messageControl = this.FindControl<TextBlock>("MessageLabel");

            //Setting the value
            messageControl.Text = $"Hello {nameControl.Text} !!!";
            */
            // var textBox1 = this.FindControl<TextBox>("TextBox1");
            //
            // textBox1.Text = sender.ToString();
            //
            // MediaWikiObjects.PageQuery.AllRevisions allRev = manager.GetAllRevisions("Www");
            //
            // string startID = allRev.revisionList[0].revisionID;
            // string endID = allRev.revisionList[1].revisionID;
            //
            // Console.WriteLine("startID: " + startID);
            //
            // //manager.UndoRevisionsByID("Www",startID, "9");
            // manager.DeleteRevisionsByID("Www", startID + "|" + endID);
            
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
                
                KeyringEntry kr = new();
                // AddToKeyringRecursively(kr, (TreeViewItem) child);
            }         
        }

        private void Button2_Click(object? sender, RoutedEventArgs e)
        {
            // Button1_Click(this, e);

            // manager.GetAllRevisions("Www");
            //MediaWikiObjects.PageQuery.AllRevisions allRev = new("Www");
            //allRev.GetAllRevisions();    
            
            _rootKeyring.PrintInfoRecursively();
            
        }

        private void Button3_Click(object? sender, RoutedEventArgs e)
        {
            //
            // string content = manager.GetPageContent("Www");
            //
            // var textBox1 = this.FindControl<TextBox>("TextBox1");
            //
            // textBox1.Text = content;
            
            manager.ExportKeyring();
            
        }

        private void Button4_Click(object? sender, RoutedEventArgs e)
        {
            string importPath = OpenFileDialogAndGetJsonPath().ToString();
            var textBox = this.FindControl<TextBox>("TextBox1");
            textBox.Text = importPath;
        }

        private async Task<string> OpenFileDialogAndGetJsonPath()
        {
            Console.WriteLine("OpenFileDialogAndGetJsonPath entered");
            OpenFileDialog dialog = new();
            dialog.Filters.Add(new FileDialogFilter() { Extensions =  { "json" } });
            Console.WriteLine("Dialog created");
            var output = await dialog.ShowAsync(this);
            Console.WriteLine("Dialog shown");
            if (output == null)
            {
                Console.WriteLine("Output is null");
                var res = await dialog.ShowAsync(this);
            }
            
            Console.WriteLine("Returning OpenFileDialog output='{0}'", output[0]);
            var textBox = this.FindControl<TextBox>("TextBox1");
            textBox.Text = output[0];

            manager.ImportKeyring(output[0]);

            return output[0];
        }

        // TODO: finish this
        // private void AddToKeyringRecursively(KeyringEntry parentKeyringEntry, TreeViewItem parentTreeViewItem)
        // {
        //     Console.WriteLine("Entered AddToKeyringRecursively");
        //     CheckBox parentcb = (CheckBox) parentTreeViewItem.GetLogicalChildren().First(c => c.GetType() == typeof(CheckBox));
        //     Console.WriteLine("got parentcb");
        //     bool checkedStatus = parentcb.IsChecked == true;
        //     Console.WriteLine("parentTreeViewItem.DataContext: " + parentTreeViewItem.DataContext);
        //     Console.WriteLine("parentTreeViewItem.GetLogicalChildren().Count(): " + parentTreeViewItem.GetLogicalChildren().Count());
        //     int cnt = 0;
        //     
        //     foreach (TreeViewItem child in parentTreeViewItem.GetLogicalChildren()
        //         .Where(c => c.GetType() == typeof(TreeViewItem)))
        //     {
        //         Console.WriteLine("Loop iteration: " + cnt);
        //         CheckBox cb = (CheckBox) child.GetLogicalChildren()
        //             .First(c => c.GetType() == typeof(CheckBox));
        //         if (cb.IsChecked == true && child.DataContext == typeof(RootKeyring))
        //         {
        //             Console.WriteLine("Going recursively");
        //             AddToKeyringRecursively(parentKeyringEntry, child);
        //         }
        //
        //         cnt++;
        //     }
        //
        //     Console.WriteLine("Passed loop");
        // }

        // private void CheckBox_CheckedChangedRootKeyring(object? sender, RoutedEventArgs e)
        // {
        //     Control item = sender as Control;
        //     
        //     TreeViewItem TVI = GetTreeViewItemParent(item);
        //     
        //     UpdateChildrenTVICheckBoxes(TVI);
        // }
        
        // private void CheckBox_CheckedChangedKeyring(object? sender, RoutedEventArgs e)
        // {
        //     Control item = sender as Control;
        //     
        //     TreeViewItem TVI = GetTreeViewItemParent(item);
        //     
        //     UpdateChildrenTVICheckBoxes(TVI);
        //     CheckBox_CheckedChanged(sender, e);
        // }

        // private void CheckBox_CheckedChangedDataFile(object? sender, RoutedEventArgs e)
        // {
        //     Control item = sender as Control;
        //     
        //     TreeViewItem TVI = GetTreeViewItemParent(item);
        //     
        //     UpdateTVIAncestors(TVI);
        // }

        /*
        private void CheckBox_CheckedChangedUpdateParent(object? sender, RoutedEventArgs e)
        {
            // Console.WriteLine("CheckBox_CheckedChangedUpdateParent entered");
            CheckBox cb = sender as CheckBox;

            if (cb == null)
            {
                Console.WriteLine("Error, sender is not a checkbox");
                throw new Exception();
            }
            
            TreeViewItem TVI = GetTreeViewItemParent(cb);
            UpdateTVIAncestors(TVI);
        }
        
        private void CheckBox_CheckedChangedUpdateChildren(object? sender, RoutedEventArgs e)
        {
            // Console.WriteLine("CheckBox_CheckedChangedUpdateChildren entered");
            CheckBox cb = sender as CheckBox;

            if (cb == null)
            {
                Console.WriteLine("Error, sender is not a checkbox");
                throw new Exception();
            }
            
            TreeViewItem TVI = GetTreeViewItemParent(cb);
            UpdateTVIChildren(TVI);
        }
        
        private void UpdateTVIChildren(TreeViewItem TVI)
        {
            UpdateChildrenTVICheckBoxes(TVI);
        }

        private void UpdateTVIAncestors(TreeViewItem TVI)
        {
            bool anyChecked = false;
            bool atleastTwoChecked = false;
            bool anyUnchecked = false;
            bool ancestorChecked = false;
            
            TreeViewItem TVIParent = GetTreeViewItemParent(TVI);
    
            foreach (var subitem in TVIParent.GetLogicalChildren().Where(c => c.GetType() == typeof(TreeViewItem)))
            {
                CheckBox cb = (CheckBox) subitem.GetLogicalChildren().First(c => c.GetType() == typeof(CheckBox));
                if (cb.IsChecked == true)
                {
                    if (anyChecked)
                    {
                        atleastTwoChecked = true;
                    }
                    anyChecked = true;
                }
                else
                {
                    anyUnchecked = true;
                    
                }
            }
            
            CheckBox parentcb = (CheckBox) TVIParent.GetLogicalChildren().First(c => c.GetType() == typeof(CheckBox));

            TreeViewItem ancestor = TVIParent;
            List<CheckBox> cbList = new();
            
            while (!(ancestor.DataContext?.GetType() == typeof(RootKeyring)) && ancestorChecked == false)
            {
                CheckBox ancestorcb = (CheckBox) ancestor.GetLogicalChildren().First(c => c.GetType() == typeof(CheckBox));
                cbList.Add(ancestorcb);

                if (ancestorcb.IsChecked == true)
                {
                    ancestorChecked = true;

                    foreach (CheckBox cb in cbList)
                    {
                        // Ancestors can only be DataContext type of Keyring (or RootKeyring)
                        UpdateCheckBoxWithoutTriggeringEventHandler(cb, true, CheckBox_CheckedChangedUpdateChildren);
                    }
                    
                    break;
                }
                
                ancestor = GetTreeViewItemParent(ancestor);
            }

            // // Update CheckBox of parent
            // if (anyChecked && anyUnchecked)
            // {
            //     // Change here to interact with IsThreeState properly
            //     // parentcb.IsChecked = false;
            // }
            if (anyUnchecked == false || atleastTwoChecked || (ancestorChecked && anyChecked))
            {
                UpdateCheckBoxWithoutTriggeringEventHandler(parentcb, true, CheckBox_CheckedChangedUpdateChildren);
            }
            else if (anyChecked == false)
            {
                UpdateCheckBoxWithoutTriggeringEventHandler(parentcb, false, CheckBox_CheckedChangedUpdateChildren);
            }
        }

        private void UpdateChildrenTVICheckBoxes(TreeViewItem TVI)
        {
            CheckBox cb = (CheckBox) TVI.GetLogicalChildren().First(c => c.GetType() == typeof(CheckBox));
            bool checkedStatus = cb.IsChecked == true;

            foreach (var TVIChild in TVI.GetLogicalChildren().Where(c => c.GetType() == typeof(TreeViewItem)))
            {
                CheckBox childcb = (CheckBox) TVIChild.GetLogicalChildren()
                    .First(c => c.GetType() == typeof(CheckBox));
                
                // Only update downwards and prevent feedback loops
                UpdateCheckBoxWithoutTriggeringEventHandler(childcb, checkedStatus, CheckBox_CheckedChangedUpdateParent);
            }
        }

        // TODO: Accept multiple event handlers?
        private void UpdateCheckBoxWithoutTriggeringEventHandler(CheckBox cb, bool? checkedStatus, EventHandler<RoutedEventArgs> eh)
        {
            // Prevent feedback loops by temporarily removing the specified event handler 
            cb.Checked -= eh;
            cb.Unchecked -= eh;
            
            // Update child
            cb.IsChecked = checkedStatus;
                
            // Restore event handler
            cb.Checked += eh;
            cb.Unchecked += eh;
        }

        private void RemoveAllCheckedUncheckedEventHandlers(CheckBox cb)
        {
            // Remove any existing handlers
            // Attempting to remove handlers not subscribed to shouldn't do any harm

            foreach (EventHandler<RoutedEventArgs> eh in CheckBoxEventHandlers)
            {
                cb.Checked -= eh;
                cb.Unchecked -= eh;
            }
        }

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

        private void SetCheckBoxCheckedUncheckedEventHandlers(TreeViewItem parent, CheckBox cb)
        {
            RemoveAllCheckedUncheckedEventHandlers(cb);
                
            if (parent.DataContext.GetType() == typeof(KeyringEntry))
            {
                // Console.WriteLine("KeyringEntry found");
                cb.Checked += CheckBox_CheckedChangedUpdateChildren;
                cb.Unchecked += CheckBox_CheckedChangedUpdateChildren;
                cb.Checked += CheckBox_CheckedChangedUpdateParent;
                cb.Unchecked += CheckBox_CheckedChangedUpdateParent;
            }
            else if (parent.DataContext.GetType() == typeof(DataFileEntry))
            {
                // Console.WriteLine("DataFileEntry found");
                cb.Checked += CheckBox_CheckedChangedUpdateParent;
                cb.Unchecked += CheckBox_CheckedChangedUpdateParent;
            }
            else if (parent.DataContext.GetType() == typeof(RootKeyring))
            {
                // Console.WriteLine("RootKeyring found");
                cb.Checked += CheckBox_CheckedChangedUpdateChildren;
                cb.Unchecked += CheckBox_CheckedChangedUpdateChildren;
            }
        }
        */
        
        private void CheckBox_OnInitialized(object? sender, EventArgs e)
        {
            // CheckBox cb = sender as CheckBox;
            //
            // TreeViewItem TVI = GetTreeViewItemParent(cb);
            //
            // SetCheckBoxCheckedUncheckedEventHandlers(TVI, cb);
            //
            // // Let Keyrings and DataFiles inherit IsChecked value from parent (root)Keyring
            // if (TVI.DataContext.GetType() != typeof(RootKeyring))
            // {
            //     TreeViewItem TVIParent = GetTreeViewItemParent(TVI);
            //     CheckBox parentcb = (CheckBox) TVIParent.GetLogicalChildren()
            //         .First(c => c.GetType() == typeof(CheckBox));
            //     cb.IsChecked = parentcb.IsChecked;
            // }
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
            manager.SetMediaWikiServer(ip);
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


        private void CheckBox_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                DataFileEntry dataFile = cb.DataContext as DataFileEntry ?? throw new InvalidOperationException();
                _viewModel.selectedFile = dataFile;
                _viewModel.revisions = manager.GetAllRevisions(dataFile.pagename).revisionList;
                // var allRevisions = manager.GetAllRevisions(dataFile.pagename);
                // _viewModel.revisions = new ObservableCollection<Revision>(allRevisions.revisionList);
                Console.WriteLine(dataFile.filename);
            }
            // manager.GetAllRevisions("Www");
        }

        private void CheckBox_OnDoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                DataFileEntry dataFile = cb.DataContext as DataFileEntry ?? throw new InvalidOperationException();
                _viewModel.selectedFile = dataFile;
                _viewModel.revisions = manager.GetAllRevisions(dataFile.pagename).revisionList;
                // var allRevisions = manager.GetAllRevisions(dataFile.pagename);
                // _viewModel.revisions = new ObservableCollection<Revision>(allRevisions.revisionList);
                Console.WriteLine(dataFile.filename);
            }
            // manager.GetAllRevisions("Www");
        }

        private void InputElement_OnDoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                DataFileEntry dataFile = cb.DataContext as DataFileEntry ?? throw new InvalidOperationException();
                _viewModel.selectedFile = dataFile;
                _viewModel.revisions = manager.GetAllRevisions(dataFile.pagename).revisionList;
                // var allRevisions = manager.GetAllRevisions(dataFile.pagename);
                // _viewModel.revisions = new ObservableCollection<Revision>(allRevisions.revisionList);
                Console.WriteLine(dataFile.filename);
            }
            // manager.GetAllRevisions("Www");
        }

        private void CheckBox_OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.R) return;
            if (sender is CheckBox cb)
            {
                DataFileEntry dataFile = cb.DataContext as DataFileEntry ?? throw new InvalidOperationException();
                _viewModel.selectedFile = dataFile;
                _viewModel.revisions = manager.GetAllRevisions(dataFile.pagename).revisionList;
                // var allRevisions = manager.GetAllRevisions(dataFile.pagename);
                // _viewModel.revisions = new ObservableCollection<Revision>(allRevisions.revisionList);
                Console.WriteLine(dataFile.filename);
            }
            // manager.GetAllRevisions("Www");
        }

        private void SelectedRevisionButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (_viewModel.selectedRevision.revisionID != null)
            {
                Console.WriteLine(_viewModel.selectedRevision.revisionID);
                manager.RequestedRevision.Add(_viewModel.selectedFile, _viewModel.selectedRevision.revisionID);
            }
        }
    }
}