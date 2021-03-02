using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ReactiveUI;

namespace SecureWiki.Model
{
    public class RootKeyring : KeyringEntry
    {
        public RootKeyring()
        {
            Name = "Root";
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        [NotifyPropertyChangedInvocator]
         public override void OnPropertyChanged([CallerMemberName] string propertyName = null)
         {
             PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
             Console.WriteLine("OnPropertyChanged in RootKeyring, property: " + propertyName);
         }
        
    }
}


// namespace SecureWiki.Model
// {
//     [JsonObject(MemberSerialization.OptIn)]
//     // TODO: Make class derived from KeyringEntry?
//     public class RootKeyring : IReactiveObject
//     {
//         private bool? _checked = false;
//         public bool? Checked
//         {
//             get
//             {
//                 return (_checked ?? false);
//             }
//             set
//             {
//                 _checked = value;
//                 OnPropertyChanged(nameof(Checked));
//             }
//         }
//         
//         private string name = "Root";
//         [JsonProperty]
//         public string Name
//         {
//             get { return name;}
//             set
//             {
//                 name = value; 
//                 OnPropertyChanged(nameof(Name));
//                 OnPropertyChanged(nameof(name));
//                 this.RaiseAndSetIfChanged(ref name, value);
//             }
//         }
//         [JsonProperty]
//         public ObservableCollection<KeyringEntry> keyrings { get; set; } 
//             = new();
//         [JsonProperty]
//         public ObservableCollection<DataFileEntry> dataFiles { get; set; } 
//             = new();
//         
//         public ObservableCollection<object> combinedList
//         {
//             get
//             {
//                 var output = new ObservableCollection<object>();
//
//                 foreach (KeyringEntry entry in keyrings)
//                 {
//                     output.Add(entry);
//                 }
//                 foreach (DataFileEntry entry in dataFiles)
//                 {
//                     output.Add(entry);
//                 }
//                 
//                 return output;
//             }   
//         }
//
//         public void AddKeyring(KeyringEntry keyringEntry)
//         {
//             keyrings.Add(keyringEntry);
//             RaisePropertyChanged(nameof(keyrings));
//             RaisePropertyChanged(nameof(combinedList));
//         }
//         
//         public void AddDataFile(DataFileEntry dataFile)
//         {
//             dataFiles.Add(dataFile);
//             RaisePropertyChanged(nameof(dataFiles));
//             RaisePropertyChanged(nameof(combinedList));
//         }
//
//         public event PropertyChangingEventHandler? PropertyChanging;
//         public void RaisePropertyChanging(PropertyChangingEventArgs args)
//         {
//             throw new System.NotImplementedException();
//         }
//
//         public void RaisePropertyChanged(PropertyChangedEventArgs args)
//         {
//             throw new NotImplementedException();
//         }
//
//         public void RaisePropertyChanged(string propertyName)
//         {
//             PropertyChangedEventHandler handler = PropertyChanged;
//             if (handler != null)
//             {
//                 handler(this, new PropertyChangedEventArgs(propertyName));
//             }
//         }
//
//         public event PropertyChangedEventHandler? PropertyChanged;
//
//         [NotifyPropertyChangedInvocator]
//         protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
//         {
//             PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
//             Console.WriteLine("OnPropertyChanged in RootKeyring, property: " + propertyName);
//         }
//     }
// }