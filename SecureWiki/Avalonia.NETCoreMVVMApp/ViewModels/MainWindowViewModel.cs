using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using ReactiveUI;
using SecureWiki.Cryptography;
using SecureWiki.MediaWiki;
using SecureWiki.Model;
using SecureWiki.Views;

namespace SecureWiki.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private ObservableCollection<RootKeyring> _rootKeyringCollection;
        public ObservableCollection<RootKeyring> rootKeyringCollection 
        {
            get
            {
              return _rootKeyringCollection;  
            }
            set
            {
                _rootKeyringCollection = value;
                this.RaisePropertyChanged(nameof(rootKeyringCollection));
                Console.WriteLine("rootKeyringCollection set");
            }
                
        }
        public string IP { get; set; } = "127.0.0.1";

        private string _Username;
        public string Username
        {
            get
            {
                return _Username;
            }
            set
            {
                _Username = value;
                this.RaisePropertyChanged("Username");
            }
        }
        public string Password { get; set; }

        public object MailRecipient { get; set; }

        public RootKeyring rootKeyring;
        
        private DataFileEntry _selectedFile;
        public DataFileEntry selectedFile
        {
            get => _selectedFile;
            set => this.RaiseAndSetIfChanged(ref _selectedFile, value);
        }

        private List<Revision> _revisions;
        public List<Revision>? revisions
        {
            get => _revisions.Count > 0 ? _revisions : null;
            set => this.RaiseAndSetIfChanged(ref _revisions, value);
        }

        private Revision _selectedRevision;
        public Revision selectedRevision
        {
            get => _selectedRevision;
            set => this.RaiseAndSetIfChanged(ref _selectedRevision, value);
        }

        public MainWindowViewModel(RootKeyring rk)
        {
            rootKeyring = rk;
            rootKeyringCollection = new ObservableCollection<RootKeyring>();
            rootKeyringCollection.Add(rootKeyring);
        }
    }
}
