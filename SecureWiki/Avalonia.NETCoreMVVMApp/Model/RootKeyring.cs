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
            IsChecked = false;
        }
    }
}
