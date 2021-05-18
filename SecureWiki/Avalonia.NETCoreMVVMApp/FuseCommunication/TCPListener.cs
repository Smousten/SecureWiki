using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using SecureWiki.Utilities;

namespace SecureWiki.FuseCommunication
{
    public class TCPListener : IFuseInteraction
    {
        private enum Operation
        {
            None,
            Read,
            Write,
            Create,
            Rename,
            MakeDir
        }
        
        
        private readonly Int32 _port;
        private readonly IPAddress _localAddr;
        private readonly Manager _manager;
        private NetworkStream? _stream;
        private Dictionary<string, List<string>> _queue = new();
        private Operation lastOperation = Operation.None;
        private string previousFilename = "";
        private string previousFilepath = "";
        private byte[]? plaintext;

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

        // Create new TcpListener with the given address and port, then start listening
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
                Console.WriteLine("SetupTcpListener:- SocketException: {0}", e.Message);
            }
            finally
            {
                server?.Stop();
            }
        }

        // Wait for client to connect, then continue to receive input from client
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
                _stream.Close();
                break;
            }
        }

        // Perform operation requested by fuse
        private void Operations(string inputData)
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
                    lastOperation = Operation.Create;
                    break;
                case "read":
                    Read(filename, filepath);
                    lastOperation = Operation.Read;
                    break;
                case "write":
                    Write(filename, filepath);
                    lastOperation = Operation.Write;
                    break;
                case "rename":
                    var renamePathSplit = op[1].Split("%", 2);
                    Rename(filename, renamePathSplit);
                    lastOperation = Operation.Rename;
                    break;
                case "mkdir":
                    Mkdir(filename, filepath);
                    lastOperation = Operation.MakeDir;
                    break;
            }
        }

        // Received create operation from FUSE
        // Should add new file to keyring json file
        public void Create(string filename, string filepath)
        {
            if (RealFileName(filename))
            {
                _manager.AddNewFile(filepath);
            }
        }

        // Received read operation from FUSE
        // Should return byte[] stored on server or in cache
        public void Read(string filename, string filepath)
        {
            // if file is goutputstream or trash
            if (!RealFileName(filename)) return;
            
            // If the previous operation was exactly the same, reuse decrypted text
            if (!(lastOperation == Operation.Read 
                  && filename.Equals(previousFilename) 
                  && filepath.Equals(previousFilepath)) 
                || plaintext == null)
            {
                plaintext = _manager.GetContent(filepath) ?? Encoding.ASCII.GetBytes("Empty file.");
                previousFilename = filename;
                previousFilepath = filepath;
            }
                
            // Get message data
            byte[] content = plaintext;
            byte[] contentLen = BitConverter.GetBytes(content.Length);
            byte[] msgPath = Encoding.ASCII.GetBytes(filepath);
            byte[] msgPathLen = BitConverter.GetBytes(msgPath.Length);

            // Efficiently copy message data to new array and write to socket
            var dataList = new List<byte[]> {msgPathLen, contentLen, msgPath, content};
            var rv = ByteArrayCombiner.Combine(dataList);
            _stream?.Write(rv);
                
            lastOperation = Operation.Read;
        }
        
        // Received write operation from FUSE
        // Should upload new version to server
        public void Write(string filename, string filepath)
        {
            var filepathSplit = filepath.Split('/');
            
            // Check if the file should be added to new server keyring
            if (filepathSplit[0].Equals("Keyrings"))
            {
                _manager.AddFileToKeyring(filename, filepath);
            }
            else if (RealFileName(filename) && 
                !(lastOperation == Operation.Write && filename.Equals(previousFilename) && filepath.Equals(previousFilepath)))
            {
                previousFilename = filename;
                previousFilepath = filepath;
                _manager.UploadNewVersion(filename, filepath);
                lastOperation = Operation.Write;
            }
        }

        // Received rename operation from FUSE
        // Should update keyring json file to reflect rename
        // Alternatively, used to delete (new path contains .Trash) and upload (old path contains .goutputstream)
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
                // _manager.RemoveFile(oldPath, oldFilename);
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

        // Received mkdir operation from FUSE
        // If the folder is placed in 'Keyrings' then upload new keyring object on server
        // else make local folder
        public void Mkdir(string filename, string filepath)
        {
            var filePathSplit = filepath.Split("/");
            if (filePathSplit[0].Equals("Keyrings"))
            {
                _manager.AddNewKeyRing(filename);
            }
            _manager.AddNewFolder(filename, filepath);
        }
        
        // Check if file is not goutputstream or trash
        private bool RealFileName(string filepath)
        {
            return !(filepath.Contains(".goutputstream") || filepath.Contains(".Trash"));
        }
        
        // Reset queue
        public void ResetQueue()
        {
            lastOperation = Operation.None;
        }
    }
}