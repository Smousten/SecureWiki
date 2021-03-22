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
                
                // Reset data for each iteration

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
            var op = inputData.Split(new[] {':'}, 2);
            // Input must contain operation and arguments
            if (op.Length < 2) return;
            Console.WriteLine("Received: {0}", inputData);
            var filePath = op[1].Substring(1);
            char[] arr = filePath.Where(c => (char.IsLetterOrDigit(c) || 
                                              char.IsWhiteSpace(c) || 
                                              c == '.' || c == '/')).ToArray(); 
            
            filePath = new string(arr);
            var filePathSplit = filePath.Split("/");
            var filename = filePathSplit[^1];
            switch (op[0])
            {
                case "write":
                    if (RealFileName(filename))
                    {
                        _manager.UploadNewVersion(filename, filePath);
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
                    var renamePathSplit = op[1].Split("%", 2);
                    var oldPath = renamePathSplit[0].Substring(1);
                    var newPath = renamePathSplit[1].Substring(1);
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
                    break;
                case "read":
                    if (RealFileName(filename))
                    {
                        var decryptedText = _manager.ReadFile(filename) ?? Encoding.ASCII.GetBytes("File error");
                        byte[] byData = decryptedText;
                        byte[] byDataLen = BitConverter.GetBytes(byData.Length);
                        byte[] msgPath = Encoding.ASCII.GetBytes(op[1]);
                        byte[] msgPathLen = BitConverter.GetBytes(msgPath.Length);
                        
                        byte[] rv = new byte[msgPathLen.Length + byDataLen.Length + msgPath.Length + byData.Length];
                        Buffer.BlockCopy(msgPathLen, 0, rv, 0, msgPathLen.Length);
                        Buffer.BlockCopy(byDataLen, 0, rv, msgPathLen.Length, byDataLen.Length);
                        Buffer.BlockCopy(msgPath, 0, rv, msgPathLen.Length + byDataLen.Length, msgPath.Length);
                        Buffer.BlockCopy(byData, 0, rv, msgPathLen.Length + byDataLen.Length + msgPath.Length, byData.Length);
                        _stream?.Write(rv);
                        Console.WriteLine(rv.Length);
                        Console.WriteLine("sending to server socket: {0} {1} {2} {3}", BitConverter.ToInt32(msgPathLen), BitConverter.ToInt32(byDataLen), Encoding.ASCII.GetString(msgPath), Encoding.ASCII.GetString(byData));
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