using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Collections;
using JetBrains.Annotations;
using ReactiveUI;
using SecureWiki.Cryptography;
using SecureWiki.MediaWiki;
using SecureWiki.Model;
using SecureWiki.Utilities;
using SecureWiki.Views;

namespace SecureWiki.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private ObservableCollection<RootKeyring> _rootKeyringCollection;

        public ObservableCollection<RootKeyring> rootKeyringCollection
        {
            get { return _rootKeyringCollection; }
            set
            {
                _rootKeyringCollection = value;
                this.RaisePropertyChanged(nameof(rootKeyringCollection));
                Console.WriteLine("rootKeyringCollection set");
            }
        }

        private ObservableCollection<Logger> _loggerCollection;

        public ObservableCollection<Logger> loggerCollection
        {
            get { return _loggerCollection; }
            set
            {
                _loggerCollection = value;
                this.RaisePropertyChanged(nameof(loggerCollection));
                Console.WriteLine("loggerCollection set");
            }
        }


        public string IP { get; set; } = "http://192.168.1.7/mediawiki/api.php";

        private string _Username;

        public string Username
        {
            get { return _Username; }
            set
            {
                _Username = value;
                this.RaisePropertyChanged("Username");
            }
        }

        public string Password { get; set; }

        public object MailRecipient { get; set; }

        public RootKeyring rootKeyring;
        private Logger _logger;

        public Logger logger
        {
            get => _logger;
            set => this.RaiseAndSetIfChanged(ref _logger, value);
        }

        public ObservableCollection<LoggerEntry> LoggerEntries;

        private DataFileEntry _selectedFile;

        public DataFileEntry selectedFile
        {
            get => _selectedFile;
            set => this.RaiseAndSetIfChanged(ref _selectedFile, value);
        }

        private ObservableCollection<Revision> _revisions = new();

        public ObservableCollection<Revision> revisions
        {
            get => _revisions;
            // set => this.RaiseAndSetIfChanged(ref _revisions, value);
            set
            {
                // Console.WriteLine("setting revisions");
                _revisions = value;
                // Console.WriteLine("revisions set");
                this.RaisePropertyChanged(nameof(revisions));
                // Console.WriteLine("property raised");
            }
        }

        private string _selectedFileRevision;

        public string selectedFileRevision
        {
            get => _selectedFileRevision;
            set => this.RaiseAndSetIfChanged(ref _selectedFileRevision, value);
        }

        private Revision _selectedRevision;

        public Revision selectedRevision
        {
            get => _selectedRevision;
            set => this.RaiseAndSetIfChanged(ref _selectedRevision, value);
        }

        public string ServerLinkPopUp { get; set; }

        public string NicknamePopUp { get; set; }

        private ObservableCollection<Contact> _exportContactsOwn = new();

        public ObservableCollection<Contact> ExportContactsOwn
        {
            get => _exportContactsOwn;
            // set => this.RaiseAndSetIfChanged(ref _revisions, value);
            set
            {
                // Console.WriteLine("setting revisions");
                _exportContactsOwn = value;
                // Console.WriteLine("revisions set");
                this.RaisePropertyChanged(nameof(ExportContactsOwn));
                // Console.WriteLine("property raised");
            }
        }

        public ObservableCollection<Contact> SelectedExportContactsOwn { get; } = new();
        
        private ObservableCollection<Contact> _exportContactsOther = new();

        public ObservableCollection<Contact> ExportContactsOther
        {
            get => _exportContactsOther;
            // set => this.RaiseAndSetIfChanged(ref _revisions, value);
            set
            {
                // Console.WriteLine("setting revisions");
                _exportContactsOther = value;
                // Console.WriteLine("revisions set");
                this.RaisePropertyChanged(nameof(ExportContactsOther));
                // Console.WriteLine("property raised");
            }
        }

        public ObservableCollection<Contact> SelectedExportContactsOther { get; } = new();

        private ObservableCollection<Contact> _revokeContacts = new();

        public ObservableCollection<Contact> RevokeContacts
        {
            get => _revokeContacts;
            // set => this.RaiseAndSetIfChanged(ref _revisions, value);
            set
            {
                // Console.WriteLine("setting revisions");
                _revokeContacts = value;
                // Console.WriteLine("revisions set");
                this.RaisePropertyChanged(nameof(RevokeContacts));
                // Console.WriteLine("property raised");
            }
        }

        public ObservableCollection<Contact> SelectedRevokeContacts { get; } = new();
        
        
        public MainWindowViewModel(RootKeyring rk, Logger logger)
        {
            rootKeyring = rk;
            rootKeyringCollection = new ObservableCollection<RootKeyring>();
            rootKeyringCollection.Add(rootKeyring);

            this.logger = logger;
            loggerCollection = new ObservableCollection<Logger>();
            loggerCollection.Add(this.logger);

            revisions = new ObservableCollection<Revision>();
        }

        public MainWindowViewModel()
        {
            rootKeyringCollection = new ObservableCollection<RootKeyring>();
            revisions = new ObservableCollection<Revision>();
        }
    }
}