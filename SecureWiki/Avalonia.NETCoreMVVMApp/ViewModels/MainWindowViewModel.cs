using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SecureWiki.Cryptography;
using SecureWiki.Model;
using SecureWiki.Views;

namespace SecureWiki.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public ObservableCollection<RootKeyring> rootKeyring { get; set; }

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
