using System;
using System.Collections.ObjectModel;
using ReactiveUI;
using SecureWiki.MediaWiki;
using SecureWiki.Model;
using SecureWiki.Utilities;

namespace SecureWiki.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private ObservableCollection<MDFolder> _rootMountedDirFolderCollection;
        public ObservableCollection<MDFolder> rootMountedDirFolderCollection
        {
            get { return _rootMountedDirFolderCollection; }
            set
            {
                _rootMountedDirFolderCollection = value;
                this.RaisePropertyChanged(nameof(rootMountedDirFolderCollection));
            }
        }
        
        private ObservableCollection<MDFolder> _keyringMountedDirFolderCollection;
        public ObservableCollection<MDFolder> keyringMountedDirFolderCollection
        {
            get { return _keyringMountedDirFolderCollection; }
            set
            {
                _keyringMountedDirFolderCollection = value;
                this.RaisePropertyChanged(nameof(keyringMountedDirFolderCollection));
            }
        }
        private ObservableCollection<MasterKeyring> _rootKeyringCollection;

        public ObservableCollection<MasterKeyring> rootKeyringCollection
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

        private MountedDirMirror _mountedDirMirror;
        public MountedDirMirror MountedDirMirror
        {
            get => _mountedDirMirror;
            set => this.RaiseAndSetIfChanged(ref _mountedDirMirror, value);
        }
        public MasterKeyring MasterKeyring;
        private Logger _logger;

        public Logger logger
        {
            get => _logger;
            set => this.RaiseAndSetIfChanged(ref _logger, value);
        }

        public ObservableCollection<LoggerEntry> LoggerEntries;

        private AccessFile _selectedFile;

        public AccessFile selectedFile
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
        
        private string _nicknamePopUp;
        public string NicknamePopUp
        {
            get => _nicknamePopUp;
            set => this.RaiseAndSetIfChanged(ref _nicknamePopUp, value);
        }

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
        
        private ObservableCollection<Contact> _shareContacts = new();

        public ObservableCollection<Contact> ShareContacts
        {
            get => _shareContacts;
            set
            {
                _shareContacts = value;
                this.RaisePropertyChanged(nameof(ShareContacts));
            }
        }

        public ObservableCollection<Contact> SelectedShareContacts { get; } = new();
        
        private ObservableCollection<Keyring> _keyrings = new();
        public ObservableCollection<Keyring> keyrings
        {
            get => _keyrings;
            set
            {
                _keyrings = value;
                this.RaisePropertyChanged(nameof(keyrings));
            }
        }
        public ObservableCollection<Keyring> selectedKeyrings { get; } = new();

        private ObservableCollection<OwnContact> _ownContacts = new();
        public ObservableCollection<OwnContact> ownContacts
        {
            get => _ownContacts;
            set
            {
                _ownContacts = value;
                this.RaisePropertyChanged(nameof(ownContacts));
            }
        }
        private OwnContact _selectedOwnContact;
        public OwnContact selectedOwnContact
        {
            get => _selectedOwnContact;
            set => this.RaiseAndSetIfChanged(ref _selectedOwnContact, value);
        }
        
        public MainWindowViewModel(MasterKeyring rk, Logger logger, MountedDirMirror mountedDirMirror)
        {
            MasterKeyring = rk;
            rootKeyringCollection = new ObservableCollection<MasterKeyring>();
            rootKeyringCollection.Add(MasterKeyring);

            this.logger = logger;
            loggerCollection = new ObservableCollection<Logger>();
            loggerCollection.Add(this.logger);

            this.MountedDirMirror = mountedDirMirror;
            rootMountedDirFolderCollection = new ObservableCollection<MDFolder>();
            rootMountedDirFolderCollection.Add(MountedDirMirror.RootFolder);
            keyringMountedDirFolderCollection = new ObservableCollection<MDFolder>();
            keyringMountedDirFolderCollection.Add(mountedDirMirror.KeyringFolder);

            revisions = new ObservableCollection<Revision>();
        }

        public MainWindowViewModel()
        {
            rootKeyringCollection = new ObservableCollection<MasterKeyring>();
            revisions = new ObservableCollection<Revision>();
        }
    }
}