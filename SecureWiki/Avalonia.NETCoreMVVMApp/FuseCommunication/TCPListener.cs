using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SecureWiki.FuseCommunication
{
    public class TCPListener : IFuseInteraction
    {
        private readonly Int32 _port;
        private readonly IPAddress _localAddr;
        private readonly Manager _manager;
        private NetworkStream? _stream;
        private Dictionary<string, List<string>> _queue = new();
        private bool lastOperationWasRead = false;
        private string previousFilename = "";
        private string previousFilepath = "";
        private byte[] decryptedText;

        public TCPListener(int port, string localAddr, Manager manager)
        {
            _port = port;
            _localAddr = IPAddress.Parse(localAddr);
            _manager = manager;
        }

        public void RunListener()
        {
            SetupTcpListener();
        }

        private void SetupTcpListener()
        {
            TcpListener? server = null;
            try
            {
                server = new TcpListener(_localAddr, _port);
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
            var bytes = new byte[256];

            while (true)
            {
                Console.WriteLine("Waiting for TCP connection at port:" + _port);

                TcpClient client = server.AcceptTcpClient();
                Console.WriteLine("Connected at port:" + _port);

                _stream = client.GetStream();
                int input;
                while ((input = _stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    // Convert input bytes to ASCII before use
                    var data = Encoding.ASCII.GetString(bytes, 0, input);
                    Operations(data);
                }

                client.Close();
            }
        }

        private void Operations(String inputData)
        {
            // Input must contain operation and arguments
            var op = inputData.Split(new[] {':'}, 2);
            if (op.Length < 2) return;

            Console.WriteLine("Received: {0}", inputData);
            
            // Extract and parse filepath
            var filepath = op[1].Substring(1);
            char[] arr = filepath.Where(c => (char.IsLetterOrDigit(c) ||
                                              char.IsWhiteSpace(c) ||
                                              c == '.' || c == '/' || c == '_')).ToArray();
            filepath = new string(arr);
            
            // Get filename
            var filepathSplit = filepath.Split("/");
            var filename = filepathSplit[^1];
            
            switch (op[0])
            {
                case "create":
                    Create(filename, filepath);
                    lastOperationWasRead = false;
                    break;
                case "read":
                    Read(filename, op[1]);
                    break;
                case "write":
                    Write(filename, filepath);
                    lastOperationWasRead = false;
                    break;
                case "rename":
                    var renamePathSplit = op[1].Split("%", 2);
                    Rename(filename, renamePathSplit);
                    lastOperationWasRead = false;
                    break;
                case "mkdir":
                    Mkdir(filename, filepath);
                    lastOperationWasRead = false;
                    break;
            }
        }

        public void Create(string filename, string filepath)
        {
            if (RealFileName(filename))
            {
                _manager.AddNewFile(filename, filepath);
            }
        }

        public void Read(string filename, string filepath)
        {
            if (RealFileName(filename))
            {
                
                
                // Method 1) write to file in rootdir
                // var decryptedText = _manager.Download(filename) ?? Encoding.ASCII.GetBytes("File error");
                // byte[] byData = decryptedText;
                // byte[] msgPath = Encoding.ASCII.GetBytes(filepath);
                //
                // filepath = filepath.Trim('\0');
                // var relativeFilePath = "fuse/directories/rootdir" + filepath;
                // var currentDir = Directory.GetCurrentDirectory();
                // var projectDir = Path.GetFullPath(Path.Combine(currentDir, @"../../../../.."));
                // var srcDir = Path.Combine(projectDir, relativeFilePath);
                //
                // // Do not write bytes to file if last write time was within 10 seconds
                // if (File.Exists(srcDir))
                // {
                //     var lastWriteTime = File.GetLastWriteTime(srcDir);
                //     var currentTime = DateTime.Now;
                //     var span = currentTime - lastWriteTime;
                //     if (span.Seconds < 10)
                //     {
                //         _stream?.Write(msgPath);
                //         return;
                //     }
                // }
                //
                // Console.WriteLine("writing to file " + srcDir);
                // File.WriteAllBytes(srcDir, byData);
                // _stream?.Write(msgPath);
                
                // Method 2) use socket to send bytes
                
                // If the previous operation was not exactly the same, otherwise reuse decrypted text
                if (!(lastOperationWasRead && filename.Equals(previousFilename) && filepath.Equals(previousFilepath)))
                {
                    decryptedText = _manager.Download(filename) ?? Encoding.ASCII.GetBytes("File error");
                    lastOperationWasRead = true;
                    previousFilename = filename;
                    previousFilepath = filepath;
                }
                
                byte[] byData = decryptedText;
                byte[] byDataLen = BitConverter.GetBytes(byData.Length);
                byte[] msgPath = Encoding.ASCII.GetBytes(filepath);
                byte[] msgPathLen = BitConverter.GetBytes(msgPath.Length);

                byte[] rv = new byte[msgPathLen.Length + byDataLen.Length + msgPath.Length + byData.Length];
                Buffer.BlockCopy(msgPathLen, 0, rv, 0, msgPathLen.Length);
                Buffer.BlockCopy(byDataLen, 0, rv, msgPathLen.Length, byDataLen.Length);
                Buffer.BlockCopy(msgPath, 0, rv, msgPathLen.Length + byDataLen.Length, msgPath.Length);
                Buffer.BlockCopy(byData, 0, rv, msgPathLen.Length + byDataLen.Length + msgPath.Length, byData.Length);
                _stream?.Write(rv);
                lastOperationWasRead = true;
            }
        }

        public void Write(string filename, string filepath)
        {
            if (RealFileName(filename))
            {
                _manager.UploadNewVersion(filename, filepath);
            }
        }

        public void Rename(string filename, string[] filepaths)
        {
            var oldPath = filepaths[0].Substring(1);
            var newPath = filepaths[1].Substring(1);
            newPath = newPath.Trim('\0');

            // Remove if file is moved to trash folder
            if (newPath.Contains(".Trash"))
            {
                var oldFilePathSplit = oldPath.Split("/");
                var oldFilename = oldFilePathSplit[^1];
                _manager.RemoveFile(oldPath, oldFilename);
            }
            // Else if the file is renamed from goutputstream then upload new version
            else if (oldPath.Contains(".goutputstream"))
            {
                var newFilePathSplit = newPath.Split("/");
                var newFilename = newFilePathSplit[^1];
                _manager.UploadNewVersion(newFilename, newPath);
            }
            // Else if the new filename is not goutputstream or .trash then rename
            else if (RealFileName(filename))
            {
                _manager.RenameFile(oldPath, newPath);
            }
        }

        public void Mkdir(string filename, string filepath)
        {
            _manager.AddNewKeyRing(filename, filepath);
        }
        
        private bool RealFileName(string filepath)
        {
            return !(filepath.Contains(".goutputstream") || filepath.Contains(".Trash"));
        }
    }
}