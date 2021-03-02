using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SecureWiki.Cryptography;
using SecureWiki.MediaWiki;
using SecureWiki.Model;
using SecureWiki.Views;

namespace SecureWiki.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public ObservableCollection<RootKeyring> rootKeyring { get; set; }
        public string IP { get; set; } = "127.0.0.1";

        public string Username { get; set; }
        public string Password { get; set; }

        public object MailRecipient { get; set; }

        public DataFileEntry selectedFile { get; set; }

        public MediaWikiObjects.PageQuery.AllRevisions revisions
        {
            get { throw new NotImplementedException(); }
            set => throw new NotImplementedException();
        }

        private KeyringEntry rootKeyringEntry;

        public MainWindowViewModel()
        {
            rootKeyring = new ObservableCollection<RootKeyring>(BuildRootKeyring());
        }

        private List<RootKeyring> BuildRootKeyring()
        {
            ObservableCollection<RootKeyring> rkr = new();
            RootKeyring rk = new();
            Keyring kr = new();

            kr.InitKeyring();
            KeyringEntry rootKeyringEntry = kr.ReadKeyRing();
            
            rk.name = "Keyrings:";
            rk.keyrings = rootKeyringEntry.keyrings;
            rk.dataFiles = new ObservableCollection<DataFileEntry>(rootKeyringEntry.dataFiles);
            Console.WriteLine("BuildRootKeyring:- rk datafile count: " + rk.dataFiles.Count);
            rkr.Add(rk);

            return rkr.ToList();
        }
    }
}
