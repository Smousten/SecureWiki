using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace SecureWiki.Views
{
    public class CredentialsPopup : Window
    {

        public struct CredentialsResult
        {
            public Result ButtonResult;
            public string Username;
            public string Password;
            public bool SaveUsername;
            public bool SavePassword;
        }

        public enum Result
        {
            Ok,
            Cancel,
        }

        public CredentialsPopup()
        {
            AvaloniaXamlLoader.Load(this);
            Topmost = true;
            Activate();
            Focus();
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        public Task<CredentialsResult> Show(Window parent, string content, string title, string? savedUsername)
        {
            // Set window title
            var popupEnterCredentials = new CredentialsPopup {Title = title};

            // Set content text
            popupEnterCredentials.FindControl<TextBlock>("TextBlock").Text = content;
            var buttonPanel = popupEnterCredentials.FindControl<StackPanel>("Buttons");
            var textBoxUsername = popupEnterCredentials.FindControl<TextBox>("TextBoxUsername");
            var textBoxPassword = popupEnterCredentials.FindControl<TextBox>("TextBoxPassword");
            var checkBoxUsername = popupEnterCredentials.FindControl<CheckBox>("CheckBoxUsername");
            var checkBoxPassword = popupEnterCredentials.FindControl<CheckBox>("CheckBoxPassword");
            var buttonOkay = popupEnterCredentials.FindControl<Button>("ButtonOkay");
            var buttonCancel = popupEnterCredentials.FindControl<Button>("ButtonCancel");

            // If username is provided, show it
            if (savedUsername != null)
            {
                textBoxUsername.Text = savedUsername;
                checkBoxUsername.IsChecked = true;
            }
            
            var res = new CredentialsResult();

            // Set button click events
            buttonOkay.Click += (_, __) => { 
                res.ButtonResult = Result.Ok;
                res.Username = textBoxUsername.Text;
                res.Password = textBoxPassword.Text;
                res.SaveUsername = checkBoxUsername.IsChecked == true;
                res.SavePassword = checkBoxPassword.IsChecked == true;
                popupEnterCredentials.Close();
            };
            buttonCancel.Click += (_, __) => { 
                res.ButtonResult = Result.Cancel;
                popupEnterCredentials.Close();
            };

            // Return result when closing
            var taskCompletionSource = new TaskCompletionSource<CredentialsResult>();
            popupEnterCredentials.Closed += delegate { taskCompletionSource.TrySetResult(res); };
            popupEnterCredentials.ShowDialog(parent);
            Topmost = false;
            return taskCompletionSource.Task;
        }


        private void CheckBoxUsername_OnUnchecked(object? sender, RoutedEventArgs e)
        {
            var checkBoxPassword = this.FindControl<CheckBox>("CheckBoxPassword");
            checkBoxPassword.IsChecked = false;
        }
    }

}