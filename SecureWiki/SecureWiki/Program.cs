using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SecureWiki
{
    class Program
    {
        public static void Main()
        {
            SetupTcpListener();
        }

        private static void SetupTcpListener()
        {
            TcpListener server = null;
            try
            {
                Int32 port = 11111;
                IPAddress localAddr = IPAddress.Parse("127.0.1.1");
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
                server.Stop();
            }
        }

        private static void ListenLoop(TcpListener server)
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

        private static void Operations(String inputData)
        {
            Console.WriteLine("Received: {0}", inputData);
            var pieces = inputData.Split(new[] {':'}, 2);
            switch (pieces[0])
            {
                case "release":

                    break;
                case "create":

                    break;
                default:
                    break;
            }
        }
    }
}