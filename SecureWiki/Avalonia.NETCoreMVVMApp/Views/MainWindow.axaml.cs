using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Avalonia.NETCoreMVVMApp.Views
{
    public class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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

        }

        private void Button2_Click(object? sender, RoutedEventArgs e)
        {
            Button1_Click(this, e);
        }

        private void Button3_Click(object? sender, RoutedEventArgs e)
        {
            Button1_Click(this, e);
        }
    }
}