﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using SecureWiki.Cryptography;
using SecureWiki.MediaWiki;

namespace SecureWiki.ClientApplication
{
    class Program
    {
        private static WikiHandler wikiHandler;
        private static KeyRing keyRing;
        private static TCPListener tcpListener;

        public static void Main()
        {
            wikiHandler = new WikiHandler("new_mysql_user", "THISpasswordSHOULDbeCHANGED");
            keyRing = new KeyRing();
            tcpListener = new TCPListener(11111, "127.0.1.1", wikiHandler, keyRing);
        }
    }
}