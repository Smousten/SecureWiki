/*
Code taken from https://stackoverflow.com/questions/55706291/how-to-show-a-message-box-in-avaloniaui-beta
From answer by user 'kekekeks' Apr 16 '19 at 12:05
*/

using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SecureWiki.Views
{
    public class CredentialsPopup : Window
    {

        public struct CredentialsResult
        {
            public PopupButtonResult ButtonResult;
            public string Username;
            public string Password;
            public bool SaveUsername;
            public bool SavePassword;
        }

        public enum PopupButtonResult
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
            // Topmost = false;
        }

        public Task<CredentialsResult> Show(Window parent, string text, string title, string? savedUsername)
        {
            var popupEnterCredentials = new CredentialsPopup()
            {
                Title = title
            };
            popupEnterCredentials.FindControl<TextBlock>("Text").Text = text;
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
                res.ButtonResult = PopupButtonResult.Ok;
                res.Username = textBoxUsername.Text;
                res.Password = textBoxPassword.Text;
                res.SaveUsername = checkBoxUsername.IsChecked == true;
                res.SavePassword = checkBoxPassword.IsChecked == true;
                popupEnterCredentials.Close();
            };
            buttonCancel.Click += (_, __) => { 
                res.ButtonResult = PopupButtonResult.Cancel;
                popupEnterCredentials.Close();
            };

            // Return result when closing
            var tcs = new TaskCompletionSource<CredentialsResult>();
            popupEnterCredentials.Closed += delegate { tcs.TrySetResult(res); };
            popupEnterCredentials.ShowDialog(parent);
            Topmost = false;
            return tcs.Task;
        }


        private void CheckBoxUsername_OnUnchecked(object? sender, RoutedEventArgs e)
        {
            var checkBoxPassword = this.FindControl<CheckBox>("CheckBoxPassword");
            checkBoxPassword.IsChecked = false;
        }
    }

}