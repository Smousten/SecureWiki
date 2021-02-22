using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.CodeAnalysis;
using SecureWiki.Cryptography;
using SecureWiki.MediaWiki;

namespace SecureWiki.ClientApplication
{
    public class TCPListener
    {
        private Int32 port;
        private IPAddress localAddr;
        private Manager manager;

        public TCPListener(int port, string localAddr, Manager manager)
        {
            this.port = port;
            this.localAddr = IPAddress.Parse(localAddr);
            this.manager = manager;
        }

        public void RunListener()
        {
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
                Console.WriteLine("SetupTcpListener:- SocketException: {0}", e);
            }
            finally
            {
                server?.Stop();
            }
        }

        private void ListenLoop(TcpListener server)
        {
            Byte[] bytes = new Byte[256];
            String data = null;
            int input;

            while (true)
            {
                Console.Write("Waiting for TCP connection at port:" + port);

                TcpClient client = server.AcceptTcpClient();
                Console.WriteLine("Connected at port:" + port);

                NetworkStream stream = client.GetStream();
                
                // Reset data for each iteration
                data = null;

                while ((input = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    // Convert input bytes to ASCII before use
                    data = System.Text.Encoding.ASCII.GetString(bytes, 0, input);
                    Operations(data);
                }

                client.Close();
            }
        }

        private void Operations(String inputData)
        {
            Console.WriteLine("Received: {0}", inputData);
            var op = inputData.Split(new[] {':'}, 2);
            // var path = inputData.Split("/srcTest/", 2);
            // var filename = path[^1];
            var srcDir = inputData.Split("/srcTest/", 2);
            var filepath = srcDir[^1]; 
            var filepathsplit = filepath.Split("/");
            var filename = filepathsplit[^1];
            switch (op[0])
            {
                case "release":
                    if (RealFileName(filepath))
                    {
                        // wikiHandler.UploadNewVersion(filename);
                    }
                    break;
                case "create":
                    if (RealFileName(filepath))
                    {
                        manager.AddNewFile(filepath, filename);
                    }
                    break;
                case "mkdir":
                    if (RealFileName(filepath))
                    {
                        manager.AddNewKeyRing(filepath, filename);
                    }
                    break;
                case "rename":
                    if (RealFileName(filepath))
                    {
                        // manager.RenameFile(filepath, oldname, newname);
                    }

                    break;
            }
        }
        
        private bool RealFileName(string filepath)
        {
            return !(filepath.Contains(".goutputstream") || filepath.Contains(".Trash"));
        }
    }
}