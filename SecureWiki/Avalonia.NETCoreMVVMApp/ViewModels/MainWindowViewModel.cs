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
        
        public DataFileEntry selectedFile { get; set; }

        public MediaWikiObjects.PageQuery.AllRevisions revisions
        {
            get { throw new NotImplementedException(); }
            set => throw new NotImplementedException();
        }


        public MainWindowViewModel(RootKeyring rk)
        {
            rootKeyring = rk;
            rootKeyringCollection = new ObservableCollection<RootKeyring>();
            rootKeyringCollection.Add(rootKeyring);
        }
    }
}
