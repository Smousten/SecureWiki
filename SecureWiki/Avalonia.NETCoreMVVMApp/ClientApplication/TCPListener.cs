using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SecureWiki.ClientApplication
{
    public class TCPListener
    {
        private readonly Int32 _port;
        private readonly IPAddress _localAddr;
        private readonly Manager _manager;
        private NetworkStream? _stream;

        public TCPListener(int port, string localAddr, Manager manager)
        {
            this._port = port;
            this._localAddr = IPAddress.Parse(localAddr);
            this._manager = manager;
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
            Byte[] bytes = new Byte[256];
            string? data = null;
            int input;

            while (true)
            {
                Console.Write("Waiting for TCP connection at port:" + _port);

                TcpClient client = server.AcceptTcpClient();
                Console.WriteLine("Connected at port:" + _port);

                _stream = client.GetStream();
                
                // Reset data for each iteration
                data = null;

                while ((input = _stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    // Convert input bytes to ASCII before use
                    data = Encoding.ASCII.GetString(bytes, 0, input);
                    Operations(data);
                }

                client.Close();
            }
        }

        private async void Operations(String inputData)
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
            var filePath = op[1].Substring(1);
            char[] arr = filePath.Where(c => (char.IsLetterOrDigit(c) || 
                                              char.IsWhiteSpace(c) || 
                                              c == '.' || c == '/')).ToArray(); 
            
            filePath = new string(arr);
            var filePathSplit = filePath.Split("/");
            var filename = filePathSplit[^1];
            switch (op[0])
            {
                case "release":
                    if (RealFileName(filename))
                    {
                        await _manager.UploadNewVersion(filename);
                    }
                    break;
                case "create":
                    if (RealFileName(filename))
                    {
                        _manager.AddNewFile(filePath, filename);
                    }
                    break;
                case "mkdir":
                    _manager.AddNewKeyRing(filePath, filename);
                    break;
                case "rename":
                    if (RealFileName(filename))
                    {
                        var renamePathSplit = op[1].Split("%", 2);
                        var oldPath = renamePathSplit[0].Substring(1);
                        var newPath = renamePathSplit[1].Substring(1);
                        _manager.RenameFile(oldPath, newPath);
                    }
                    break;
                case "read":
                    if (RealFileName(filename))
                    {
                        string decryptedText = _manager.ReadFile(filename);
                        byte[] byData = Encoding.ASCII.GetBytes(decryptedText);
                        _stream?.Write(byData);
                    }
                    break;
                // TODO: Does not currently work because fuse does not remove the file but instead calls rename to move the file to a .Trash directory
                //
                // case "rmfile":
                //     if (RealFileName(filename))
                //     {
                //         _manager.RemoveFile(filePath, filename, "file");
                //     }
                //     break;
                // case "rmdir":
                //     if (RealFileName(filename))
                //     {
                //         _manager.RemoveFile(filePath, filename, "keyring");
                //     }
                //
                //     break;
            }
        }
        
        private bool RealFileName(string filepath)
        {
            return !(filepath.Contains(".goutputstream") || filepath.Contains(".Trash"));
        }
    }
}