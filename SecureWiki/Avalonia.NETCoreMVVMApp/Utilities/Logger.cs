using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using JetBrains.Annotations;
using ReactiveUI;

namespace SecureWiki.Utilities
{
    public class LoggerEntry : IReactiveObject
    {
        public enum LogPriority
        {
            Fatal,
            Error,
            Warning,
            High,
            Normal,
            Low
        }

        public LogPriority Priority;
        
        private string _timestamp = "";
        public string timestamp
        {
            get => _timestamp + " - ";
            set
            {
                _timestamp = value;
                RaisePropertyChanged(nameof(timestamp));
                // OnPropertyChanged(nameof(Content));
            }
        }
        private string? _location;
        public string? location
        {
            get => _location;
            set
            {
                _location = value + ": ";
                RaisePropertyChanged(nameof(location));
                // OnPropertyChanged(nameof(Content));
            }
        }
        private string _content = "";
        public string content
        {
            get => BuildContentString();
            set
            {
                _content = value;
                RaisePropertyChanged(nameof(content));
                // OnPropertyChanged(nameof(Content));
            }
        }

        public LoggerEntry(string timestamp, string? location, string content, LogPriority priority)
        {
            this.timestamp = timestamp;
            this.location = location;
            this.content = content;
            this.Priority = priority;
        }

        private string BuildContentString()
        {
            string contentString = "";

            switch (Priority)
            {
                case LogPriority.Fatal:
                    contentString += "FATAL ERROR: ";
                    break;
                case LogPriority.Error:
                    contentString += "ERROR: ";
                    break;
                case LogPriority.Warning:
                    contentString += "Warning: ";
                    break;
                case LogPriority.High:
                    break;
                case LogPriority.Normal:
                    break;
                case LogPriority.Low:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            const string separator = " - ";
            
            contentString += _content;
            contentString += separator;
            contentString += location;
            
            return contentString;
        }
        

        public event PropertyChangedEventHandler? PropertyChanged;
        public event PropertyChangingEventHandler? PropertyChanging;
        public void RaisePropertyChanging(PropertyChangingEventArgs args)
        {
            throw new NotImplementedException();
        }

        public void RaisePropertyChanged(PropertyChangedEventArgs args)
        {
            throw new NotImplementedException();
        }

        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler? handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        [NotifyPropertyChangedInvocator]
        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null!)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    public class Logger : IReactiveObject
    {
        public LoggerEntry Headers;

        public ObservableCollection<LoggerEntry> Entries { get; set; } = new();

        public Logger()
        {
            Headers = new LoggerEntry("Timestamp", "Location", "Message", LoggerEntry.LogPriority.Low);
            // Add("at creation", "yes yes");
            // var longString = "this is a very long text and it should be wider than the textblock and maybe even the " +
            //                  "container of then textblock but how do we know that? We don't but let's find out... now? " +
            //                  "Yes, that should do it.";
            // Add("some file", longString, LoggerEntry.LogPriority.Fatal);
            // Add("some file", longString, LoggerEntry.LogPriority.Error);
            // Add("some file", longString, LoggerEntry.LogPriority.Warning);
            // Add("some file", longString, LoggerEntry.LogPriority.High);
            // Add("some file", longString, LoggerEntry.LogPriority.Normal);
            // Add("some file", longString, LoggerEntry.LogPriority.Low);
        }

        public void Add(string content, string? location = null, 
            LoggerEntry.LogPriority priority = LoggerEntry.LogPriority.Normal)
        {
            var timeNow = DateTime.Now;
            
            // Add entry through UI thread
            Dispatcher.UIThread.Post(
                () =>
                {
                    Entries.Add(new LoggerEntry(timeNow.ToShortTimeString(), location, content, priority));

                    if (Entries.Count > 200)
                    {
                        Entries.RemoveAt(0);
                    }

                    RaisePropertyChanged(nameof(Entries));
                    OnPropertyChanged(nameof(Entries));
                });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event PropertyChangingEventHandler? PropertyChanging;
        public void RaisePropertyChanging(PropertyChangingEventArgs args)
        {
            throw new NotImplementedException();
        }

        public void RaisePropertyChanged(PropertyChangedEventArgs args)
        {
            throw new NotImplementedException();
        }
        
        public void RaisePropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler? handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        [NotifyPropertyChangedInvocator]
        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null!)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}