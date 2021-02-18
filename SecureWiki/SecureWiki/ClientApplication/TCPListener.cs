using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
        private NetworkStream stream;
        
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
                stream = client.GetStream();
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
            var op = inputData.Split(":", 2);
            // Python
            // var path = inputData.Split("/srcTest/", 2);
            // var filename = path[^1];
            // Console.WriteLine(filename);
            // C
            if (op.Length < 2) return;
            var filename = op[1].Substring(1);
            char[] arr = filename.Where(c => (char.IsLetterOrDigit(c) || 
                                         char.IsWhiteSpace(c) || 
                                         c == '.')).ToArray(); 
            
            filename = new string(arr);
            switch (op[0])
            {
                case "release":

                    // if (RealFileName(filename))
                    // {
                    //     wikiHandler.UploadNewVersion(filename);
                    // }
                    // break;
                case "create":
                    if (RealFileName(filename))
                    {
                        keyRing.addNewFile(filename);
                    }
                    break;
                case "write":
                    if (RealFileName(filename))
                    {
                        Console.WriteLine("Uploading  file: " + filename);
                        wikiHandler.UploadNewVersion(filename);
                    }
                    break;
                case "read":
                    if (RealFileName(filename))
                    {
                        Task<string> decryptedTextTask = wikiHandler.ReadFile(filename);
                        string decryptedText = decryptedTextTask.Result;
                        byte[] byData = Encoding.ASCII.GetBytes(decryptedText);
                        byte[] byDataLen = BitConverter.GetBytes(byData.Length);
                        Console.WriteLine(BitConverter.ToInt32(byDataLen));
                        
                        byte[] rv = new byte[byDataLen.Length + byData.Length];
                        Buffer.BlockCopy(byDataLen, 0, rv, 0, byDataLen.Length);
                        Buffer.BlockCopy(byData, 0, rv, byDataLen.Length, byData.Length);
                        // stream.Write(rv);
                        stream.Write(byData);
                        Console.WriteLine(rv.Length);
                        Console.WriteLine("sending to server socket: {0} {1}", BitConverter.ToInt32(byDataLen), Encoding.ASCII.GetString(byData));
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