using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SecureWiki.Cryptography;
using SecureWiki.Model;

namespace SecureWiki.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public string Greeting => "Welcome to Avalonia!";
        
        public ObservableCollection<KeyringEntry> keyringEntries  { get; set; }
        public ObservableCollection<RootKeyring> rootKeyring { get; set; }
        public string IP { get; set; } = "127.0.0.1";

        public string Username { get; set; }
        public string Password { get; set; }

        public object MailRecipient { get; set; }

        private KeyringEntry rootKeyringEntry;

        public MainWindowViewModel()
        {
            keyringEntries = new();
            rootKeyring = new();
            
            Keyring kr = new();
            kr.InitKeyring();
            rootKeyringEntry = kr.ReadKeyRing();
            
            keyringEntries = rootKeyringEntry.keyrings;

            
            //rootKeyring = new ObservableCollection<RootKeyring>(FillRootKeyring());
            rootKeyring = new ObservableCollection<RootKeyring>(BuildRootKeyring(rootKeyringEntry));
            
        }

        private List<RootKeyring> BuildRootKeyring(KeyringEntry inputKeyringEntry)
        {
            ObservableCollection<RootKeyring> rkr = new();
            RootKeyring rk = new();

            rk.name = "Keyrings:";
            rk.keyrings = inputKeyringEntry.keyrings;
            rk.dataFiles = new ObservableCollection<DataFileEntry>(inputKeyringEntry.dataFiles);
            Console.WriteLine("rk datafile count: " + rk.dataFiles.Count);
            rkr.Add(rk);

            return rkr.ToList();
        }
        
        //
        // private List<RootKeyring> FillRootKeyring()
        // {
        //     
        //     KeyRing kr = new();
        //     kr.InitKeyring();
        //     keyringEntries = kr.ReadKeyRing();
        //     
        //     return new()
        //     {
        //         new RootKeyring()
        //         {
        //             name = "Western",
        //             keyrings = new ObservableCollection<KeyringEntry>()
        //             {
        //                 keyringEntries
        //             }
        //             /*
        //             keyrings =
        //             {
        //                 new KeyringEntry()
        //                 {
        //                     name = "Western Team A1",
        //                     keyrings = new ObservableCollection<KeyringEntry>()
        //                     {
        //                         new()
        //                         {
        //                             name = "Western Team a",
        //                             keyrings = new ObservableCollection<KeyringEntry>()
        //                             {
        //                                 new()
        //                                 {
        //                                     name = "Western Team a",
        //                                     keyrings = new ObservableCollection<KeyringEntry>()
        //                                     {
        //                                         new()
        //                                         {
        //                                             name = "Western Team a"
        //                                         },
        //                                         new()
        //                                         {
        //                                             name = "Western Team b"
        //                                         }
        //                                     }
        //                                 },
        //                                 new()
        //                                 {
        //                                     name = "Western Team b"
        //                                 }
        //                             }
        //                         },
        //                         new()
        //                         {
        //                             name = "Western Team b"
        //                         }
        //                     }
        //                 },
        //                 new KeyringEntry()
        //                 {
        //                     name = "Western Team b"
        //                 },
        //                 new KeyringEntry()
        //                 {
        //                     name = "Western Team c"
        //                 }
        //             }
        //             */
        //         }
        //     };
        // }
        //
        
    }
}

// keyRings = 
// {
//     new KeyringEntry()
//     {
//         name = "Western Team A2",
//         keyRings = 
//         {
//             new KeyringEntry()
//             {
//                 name = "Western Team A3"
//             },
//             new KeyringEntry()
//             {
//                 name = "Western Team b"
//             },
//             new KeyringEntry()
//             {
//                 name = "Western Team c"
//             }
//         }
//     },
//     new KeyringEntry()
//     {
//         name = "Western Team b"
//     },
//     new KeyringEntry()
//     {
//         name = "Western Team c"
//     }
// }