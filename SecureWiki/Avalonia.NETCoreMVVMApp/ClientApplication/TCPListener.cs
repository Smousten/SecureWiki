using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
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
        private NetworkStream stream;

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

                stream = client.GetStream();
                
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
            // var srcDir = inputData.Split("/srcTest/", 2);
            // var filepath = srcDir[^1]; 
            // var filepathsplit = filepath.Split("/");
            // var filename = filepathsplit[^1];
            if (op.Length < 2) return;
            var filepath = op[1].Substring(1);
            char[] arr = filepath.Where(c => (char.IsLetterOrDigit(c) || 
                                              char.IsWhiteSpace(c) || 
                                              c == '.' || c == '/')).ToArray(); 
            
            filepath = new string(arr);
            var filepathsplit = filepath.Split("/", 2);
            var filename = filepathsplit[^1];
            switch (op[0])
            {
                case "release":
                    if (RealFileName(filename))
                    {
                        manager.UploadNewVersion(filename);
                    }
                    break;
                case "create":
                    if (RealFileName(filename))
                    {
                        manager.AddNewFile(filepath, filename);
                    }
                    break;
                case "mkdir":
                    Console.WriteLine("filename: " + filename);
                    Console.WriteLine("filepath: " + filepath);
                    manager.AddNewKeyRing(filepath, filename);
                    break;
                case "rename":
                    if (RealFileName(filename))
                    {
                        var renamePathSplit = op[1].Split("%", 2);
                        var oldPath = renamePathSplit[0].Substring(1);
                        var newPath = renamePathSplit[1].Substring(1);
                        Console.WriteLine("Renaming file: " + filename);
                        Console.WriteLine("Old path: " + oldPath);
                        Console.WriteLine("New path: " + newPath);
                        manager.RenameFile(oldPath, newPath);
                    }
                    break;
                case "read":
                    if (RealFileName(filename))
                    {
                        Task<string> decryptedTextTask = manager.ReadFile(filename);
                        string decryptedText = decryptedTextTask.Result;
                        byte[] byData = Encoding.ASCII.GetBytes(decryptedText);
                        // byte[] byDataLen = BitConverter.GetBytes(byData.Length);
                        // Console.WriteLine(BitConverter.ToInt32(byDataLen));
                        //
                        // byte[] rv = new byte[byDataLen.Length + byData.Length];
                        // Buffer.BlockCopy(byDataLen, 0, rv, 0, byDataLen.Length);
                        // Buffer.BlockCopy(byData, 0, rv, byDataLen.Length, byData.Length);
                        // stream.Write(rv);
                        stream.Write(byData);
                        Console.WriteLine("sending to server socket: {0}", Encoding.ASCII.GetString(byData));
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