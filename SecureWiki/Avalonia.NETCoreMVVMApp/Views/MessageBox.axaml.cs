using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SecureWiki.Views
{
    public class MessageBox : Window
    {
        
        public enum Result
        {
            Ok,
            Cancel,
            Yes,
            No
        }

        public enum Buttons
        {
            Ok,
            OkCancel,
            YesNo,
            YesNoCancel
        }

        public MessageBox()
        {
            AvaloniaXamlLoader.Load(this);
            Topmost = true;
            Activate();
            Focus();
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        public static Task<Result> ShowMessageBox(string message, string title, Buttons buttons)
        {
            // Set window title
            var msgBox = new MessageBox {Title = title};
            
            // Set content text
            msgBox.FindControl<TextBlock>("TextBlock").Text = message;
            var stackPanel = msgBox.FindControl<StackPanel>("StackPanelButtons");

            // set default result
            var res = Result.Ok;

            // Add button to stack panel
            void AddButton(string content, Result result)
            {
                var button = new Button {Content = content};
                // Set click event so that message box closes and returns the answer of the button clicked
                button.Click += (_, __) => { 
                    res = result;
                    msgBox.Close();
                };
                stackPanel.Children.Add(button);
            }

            // Set buttons
            switch (buttons)
            {
                case Buttons.Ok:
                    AddButton("Ok", Result.Ok);
                    break;
                case Buttons.OkCancel:
                    AddButton("Ok", Result.Ok);
                    AddButton("Cancel", Result.Cancel);
                    break;
                case Buttons.YesNo:
                    AddButton("Yes", Result.Yes);
                    AddButton("No", Result.No);
                    break;
                case Buttons.YesNoCancel:
                    AddButton("Yes", Result.Yes);
                    AddButton("No", Result.No);
                    AddButton("Cancel", Result.Cancel);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(buttons), buttons, null);
            }

            // Set output
            var taskCompletionSource = new TaskCompletionSource<Result>();
            msgBox.Closed += delegate { taskCompletionSource.TrySetResult(res); };
            
            msgBox.Show();
            
            return taskCompletionSource.Task;
        }


    }

}