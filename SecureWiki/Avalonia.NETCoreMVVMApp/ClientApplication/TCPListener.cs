using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using SecureWiki.Cryptography;
using SecureWiki.MediaWiki;

namespace SecureWiki.ClientApplication
{
    public class TCPListener
    {
        private Int32 port;
        private IPAddress localAddr;
        private WikiHandler wikiHandler;
        private KeyRing keyRing;
        
        public TCPListener(int port, string localAddr, WikiHandler wikiHandler, KeyRing keyRing)
        {
            this.port = port;
            this.localAddr = IPAddress.Parse(localAddr);
            this.wikiHandler = wikiHandler;
            this.keyRing = keyRing;
        }

        public void RunListener()
        {
            keyRing.initKeyring();
            SetupTcpListener();
        }

        private void SetupTcpListener()
        {
            TcpListener server = null;
            try
            {
                server = new TcpListener(localAddr, port);
                server.Start();

                ListenLoop(server);
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                // Stop listening for new clients.
                server?.Stop();
            }
        }

        private void ListenLoop(TcpListener server)
        {
            Byte[] bytes = new Byte[256];
            String data = null;

            while (true)
            {
                Console.Write("Waiting for a connection... ");

                // Perform a blocking call to accept requests.
                // You could also use server.AcceptSocket() here.
                TcpClient client = server.AcceptTcpClient();
                Console.WriteLine("Connected!");

                data = null;
                NetworkStream stream = client.GetStream();
                int i;

                while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    // Translate data bytes to a ASCII string.
                    data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                    Operations(data);
                }

                client.Close();
            }
        }

        private void Operations(String inputData)
        {
            Console.WriteLine("Received: {0}", inputData);
            var op = inputData.Split(new[] {':'}, 2);
            var path = inputData.Split("/srcTest/", 2);
            var filename = path[^1];
            
            switch (op[0])
            {
                case "release":

                    if (RealFileName(filename))
                    {
                        wikiHandler.UploadNewVersion(filename);
                    }
                    break;
                case "create":
                    if (RealFileName(filename))
                    {
                        keyRing.addNewFile(filename);
                    }
                    break;
            }
        }
        
        private bool RealFileName(string filename)
        {
            return !(filename.StartsWith(".goutputstream") || filename.StartsWith(".Trash"));
        }
    }
}