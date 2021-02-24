using System;
using System.ComponentModel;
using System.Net.Sockets;
using System.Threading;
using System.Drawing;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;
using SecureWiki.ClientApplication;
using SecureWiki.Cryptography;
using SecureWiki.MediaWiki;
using SecureWiki.Model;
using Brushes = Avalonia.Media.Brushes;

namespace SecureWiki.Views
{
    public class MainWindow : Window
    {
        
        private WikiHandler wikiHandler;
        private Keyring _keyring;
        private TCPListener tcpListener;
        private Manager manager;
        
        
        
        public MainWindow()
        {
            InitializeComponent();

            /*
            wikiHandler = new WikiHandler("new_mysql_user", "THISpasswordSHOULDbeCHANGED");
            keyRing = new KeyRing();
            tcpListener = new TCPListener(11111, "127.0.1.1", wikiHandler, keyRing);
            Thread instanceCaller = new(tcpListener.RunListener);
            instanceCaller.Start();
            Thread fuseThread = new(Program.RunFuse);
            fuseThread.Start();
            */

            
            
            manager = new(Thread.CurrentThread);
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

            // TreeView TV = this.FindControl<TreeView>("TreeView1");
            // TreeViewItem root = TV.no
            //
            // this.FindControl<TreeView>("TreeView1").ExpandSubTree();

            //InitCheckBoxHandlers((TreeViewItem) TV.GetLogicalChildren().First(c => c.GetType() == typeof(TreeViewItem)));
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            Console.WriteLine();
            Console.WriteLine("Window is closing");
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
                AddToKeyringRecursively(kr, (TreeViewItem) child);
            }

            

        }

        private void Button2_Click(object? sender, RoutedEventArgs e)
        {
            Button1_Click(this, e);

            manager.GetAllRevisions("Www");
            //MediaWikiObjects.PageQuery.AllRevisions allRev = new("Www");
            //allRev.GetAllRevisions();
            
            
        }

        private void Button3_Click(object? sender, RoutedEventArgs e)
        {
            //Button1_Click(this, e);

            //manager.Invoke(manager.printTest("www"));
            //manager.printTest("www");


            string content = manager.GetPageContent("Www");
            
            var textBox1 = this.FindControl<TextBox>("TextBox1");

            textBox1.Text = content;

        }

        private void AddToKeyringRecursively(KeyringEntry parentKeyringEntry, TreeViewItem parentTreeViewItem)
        {
            Console.WriteLine("Entered AddToKeyringRecursively");
            CheckBox parentcb = (CheckBox) parentTreeViewItem.GetLogicalChildren().First(c => c.GetType() == typeof(CheckBox));
            Console.WriteLine("got parentcb");
            bool checkedStatus = parentcb.IsChecked == true;
            Console.WriteLine("parentTreeViewItem.DataContext: " + parentTreeViewItem.DataContext);
            Console.WriteLine("parentTreeViewItem.GetLogicalChildren().Count(): " + parentTreeViewItem.GetLogicalChildren().Count());
            int cnt = 0;
            
            foreach (TreeViewItem child in parentTreeViewItem.GetLogicalChildren()
                .Where(c => c.GetType() == typeof(TreeViewItem)))
            {
                Console.WriteLine("Loop iteration: " + cnt);
                CheckBox cb = (CheckBox) child.GetLogicalChildren()
                    .First(c => c.GetType() == typeof(CheckBox));
                if (cb.IsChecked == true && child.DataContext == typeof(RootKeyring))
                {
                    Console.WriteLine("Going recursively");
                    AddToKeyringRecursively(parentKeyringEntry, child);
                }

                cnt++;
            }

            Console.WriteLine("Passed loop");
        }

        private void CheckBox_CheckedChangedKeyring(object? sender, RoutedEventArgs e)
        {
            Control item = sender as Control;
            
            TreeViewItem TVI = GetTreeViewItemParent(item);
            
            UpdateChildrenTVICheckBoxes(TVI);
            // CheckBox_CheckedChanged(sender, e);
        }

        private void CheckBox_CheckedChanged(object? sender, RoutedEventArgs e)
        {
            Console.WriteLine("CheckBox_CheckedChanged entered");
            CheckBox item = sender as CheckBox;
            if (item != null)
            {
                TreeViewItem TVI = GetTreeViewItemParent(item);
                TreeViewItem TVIParent = GetTreeViewItemParent(TVI); 

                CheckBox cb = (CheckBox) TVIParent.GetLogicalChildren().First(c => c.GetType() == typeof(CheckBox));

                if (cb != null)
                {
                    Console.WriteLine("Found checkbox");
                    UpdateParentTVICheckBox(TVI);

                }
                else
                {
                    Console.WriteLine("CheckBox is null");
                }


            }
        }

        private void UpdateParentTVICheckBox(TreeViewItem TVI)
        {
            bool anyChecked = false;
            bool atleastTwoChecked = false;
            bool anyUnchecked = false;
            bool grandparentChecked = false;
            
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
                    // Console.WriteLine("This one is checked");
                }
                else
                {
                    anyUnchecked = true;
                    // Console.WriteLine("This one is unchecked");
                }
            }
            
            CheckBox parentcb = (CheckBox) TVIParent.GetLogicalChildren().First(c => c.GetType() == typeof(CheckBox));
            
            // Avoid attempting to check TreeView 
            if (TVIParent.DataContext.GetType() == typeof(RootKeyring))
            {
                grandparentChecked = false;
            }
            else
            {
                TreeViewItem TVIGrandParent = GetTreeViewItemParent(TVIParent);
                CheckBox grandparentcb = (CheckBox) TVIGrandParent.GetLogicalChildren().First(c => c.GetType() == typeof(CheckBox));
                grandparentChecked = grandparentcb.IsChecked == true;
            }

            // Update CheckBox of parent
            if (anyChecked && anyUnchecked)
            {
                // Change here to interact with IsThreeState properly
                // parentcb.IsChecked = false;
            }
            else if (anyUnchecked == false || atleastTwoChecked || (grandparentChecked && anyChecked))
            {
                parentcb.IsChecked = true;
            }
            else
            {
                parentcb.IsChecked = false;
            }
            
        }

        private void UpdateChildrenTVICheckBoxes(TreeViewItem TVI)
        {
            Console.WriteLine("Entered UpdateChildrenTVICheckBoxes");
            CheckBox parentcb = (CheckBox) TVI.GetLogicalChildren().First(c => c.GetType() == typeof(CheckBox));
            bool checkedStatus = parentcb.IsChecked == true;

            foreach (var subItem in TVI.GetLogicalChildren().Where(c => c.GetType() == typeof(TreeViewItem)))
            {
                CheckBox cb = (CheckBox) subItem.GetLogicalChildren()
                    .First(c => c.GetType() == typeof(CheckBox));

                
                // Prevent feedback loops
                cb.Checked -= CheckBox_CheckedChanged; 
                cb.Unchecked -= CheckBox_CheckedChanged; 
                
                // Update child
                cb.IsChecked = checkedStatus;
                
                // Restore event handlers
                cb.Checked += CheckBox_CheckedChanged;
                cb.Unchecked += CheckBox_CheckedChanged; 
            }
        }

        private void InitCheckBoxHandlers(TreeViewItem root)
        {
            foreach (TreeViewItem subItem in root.GetLogicalChildren().Where(c => c.GetType() == typeof(TreeViewItem)))
            {
                CheckBox cb = (CheckBox) subItem.GetLogicalChildren()
                    .First(c => c.GetType() == typeof(CheckBox));
                cb.Checked += CheckBox_CheckedChanged;
                InitCheckBoxHandlers(subItem);
            }
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
    }
}