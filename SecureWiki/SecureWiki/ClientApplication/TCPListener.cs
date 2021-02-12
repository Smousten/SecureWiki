using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using SecureWiki.MediaWiki;

namespace SecureWiki.ClientApplication
{
    public class TCPListener
    {
        private Int32 port;
        private IPAddress localAddr;
        private WikiHandler wikiHandler;
        
        public TCPListener(Int32 port, string localAddr, WikiHandler wikiHandler)
        {
            this.port = port;
            this.localAddr = IPAddress.Parse(localAddr);
            this.wikiHandler = wikiHandler;
            
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
            var path = inputData.Split(new[] {'/'}, 2);
            
            switch (op[0])
            {
                case "release":

                    if (RealFileName(path))
                    {
                        wikiHandler.UploadNewVersion(op[1]);
                    }
                    break;
            }
        }
        
        private bool RealFileName(IReadOnlyList<string> path)
        {
            return !path[1].StartsWith(".goutputstream") && !path[1].StartsWith(".Trash");
        }
    }
}