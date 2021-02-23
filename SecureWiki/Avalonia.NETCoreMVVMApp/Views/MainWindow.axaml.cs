using System;
using System.Net.Sockets;
using System.Threading;
using System.Drawing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SecureWiki.ClientApplication;
using SecureWiki.Cryptography;
using SecureWiki.MediaWiki;
using SecureWiki.Model;

namespace SecureWiki.Views
{
    public class MainWindow : Window
    {
        
        private WikiHandler wikiHandler;
        private KeyRing keyRing;
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
            var textBox1 = this.FindControl<TextBox>("TextBox1");

            textBox1.Text = sender.ToString();
            
            MediaWikiObjects.PageQuery.AllRevisions allRev = manager.GetAllRevisions("Www");

            string startID = allRev.revisionList[0].revisionID;
            string endID = allRev.revisionList[1].revisionID;

            Console.WriteLine("startID: " + startID);
            
            //manager.UndoRevisionsByID("Www",startID, "9");
            manager.DeleteRevisionsByID("Www", startID + "|" + endID);

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

        private void CheckBox_CheckedChanged(object? sender, RoutedEventArgs e)
        {
            Console.WriteLine("CheckBox_CheckedChanged entered");
            CheckBox item = sender as CheckBox;
            if (item != null)
            {
                Console.WriteLine("item wasnt null");
                
                Console.WriteLine("parentname: " + item.Parent.Name);
                
                
                Console.WriteLine("parent: " + item.Parent.ToString());

                TreeViewItem TVIParent = GetTreeViewItemParent(item); 
                Console.WriteLine("TVIParent.na: " + TVIParent.Name);
                Console.WriteLine("TVIParent: " + TVIParent.ToString());

            }
        }

        private TreeViewItem GetTreeViewItemParent(ContentControl item)
        {
            var parent = item.Parent;

            while (!(parent is TreeViewItem))
            {
                if (parent is TreeView)
                {
                    throw new Exception();
                }
                parent = parent.Parent;
            }

            return (TreeViewItem) parent;
        }
    }
}