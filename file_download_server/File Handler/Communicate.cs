using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using UDP_FTP.Error_Handling;
using UDP_FTP.Models;
using static UDP_FTP.Models.Enums;

namespace UDP_FTP.File_Handler
{
    class Communicate
    {
        private const string Server = "MyServer";
        private string Client;
        private int SessionID;
        private Socket socket;
        private IPEndPoint remoteEndpoint;
        private IPEndPoint sender;
        private EndPoint remoteEP;
        private ErrorType Status;
        private byte[] buffer;
        byte[] msg;
        private string file;
        ConSettings C;

        // public void RunServer() {
        // byte[] buffer = new byte[1000];
        // byte[] msg = Encoding.ASCII.GetBytes("From server: Your message delivered\n");
        // string data = null;
        // Socket sock;
        // int MsgCounter = 0;
        // IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
        // IPEndPoint localEndpoint = new IPEndPoint(ipAddress, 5004);
        // IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        // EndPoint remoteEP = (EndPoint) sender;
        // try {
        //     sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        //     sock.Bind(localEndpoint);
        //     while (MsgCounter < 10) {
        //     Console.WriteLine("\n Waiting for the next client message..");
        //     int b = sock.ReceiveFrom(buffer, ref remoteEP);
        //     data = Encoding.ASCII.GetString(buffer, 0, b);
        //     Console.WriteLine("A message received from " + remoteEP.ToString() + " " + data);
        //     sock.SendTo(msg, msg.Length, SocketFlags.None, remoteEP);
        //     MsgCounter++;
        //     }
        //     sock.Close();
        // } catch {
        //     Console.WriteLine("\n Socket Error. Terminating");
        // }
        // }
        public Communicate()
        {
            // TODO: Initializes another instance of the IPEndPoint for the remote host
            this.remoteEndpoint = new IPEndPoint(IPAddress.Any, 5010);

            // TODO: Specify the buffer size
            this.buffer = new byte[1000];

            // TODO: Get a random SessionID
            Random rnd = new Random();

            this.SessionID = rnd.Next(1882102360);

            // TODO: Create local IPEndpoints and a Socket to listen 
            //       Keep using port numbers and protocols mention in the assignment description
            //       Associate a socket to the IPEndpoints to start the communication
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            this.sender = new IPEndPoint(ipAddress, 5004);
            this.remoteEP = (EndPoint)sender;
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(sender);

        }

        public ErrorType StartDownload()
        {
            // TODO: Instantiate and initialize different messages needed for the communication
            // required messages are: HelloMSG, RequestMSG, DataMSG, AckMSG, CloseMSG
            // Set attribute values for each class accordingly 
            HelloMSG GreetBack = new HelloMSG()
            {
                Type = Messages.HELLO_REPLY,
                From = "MyServer",
                To = "MyClient",
                ConID = this.SessionID
            };
            RequestMSG req = new RequestMSG()
            {
                From = "MyServer",
                To = "MyClient",
                Type = Messages.REPLY,
                Status = ErrorType.NOERROR,
                ConID = this.SessionID
            };
            DataMSG data = new DataMSG()
            {
                From = "MyServer",
                To = "MyClient",
                Type = Messages.DATA,
                ConID = this.SessionID
            };
            AckMSG ack = new AckMSG()
            {                
                From = "MyServer",
                To = "MyClient",
                Type = Messages.ACK,
                ConID = this.SessionID
            };
            CloseMSG cls = new CloseMSG()
            {
                From = "MyServer",
                To = "MyClient",
                Type = Messages.CLOSE_REQUEST,
                ConID = this.SessionID
            };

            this.C = new ConSettings()
                {
                    From = GreetBack.From,
                    To = GreetBack.To,
                    ConID = GreetBack.ConID,
                    Sequence = 0

                };

            // TODO: Start the communication by receiving a HelloMSG message
            // Receive and deserialize HelloMSG message 
            // Verify if there are no errors
            // Type must match one of the ConSettings' types and receiver address must be the server address

            int recv = socket.ReceiveFrom(buffer, ref remoteEP);
            HelloMSG hellodata = JsonSerializer.Deserialize<HelloMSG>(Encoding.ASCII.GetString(buffer, 0, recv));
            hellodata.From = GreetBack.From;
            hellodata.To = GreetBack.To;
            hellodata.Type = GreetBack.Type;
            hellodata.ConID = GreetBack.ConID;
            Console.WriteLine("receive Hello"); // delete

            // TODO: If no error is found then HelloMSG will be sent back
            var HelloMSGPackets = JsonSerializer.Serialize(hellodata); 
            msg = Encoding.ASCII.GetBytes(HelloMSGPackets);
            socket.SendTo(msg, msg.Length, SocketFlags.None, this.remoteEP);
            Console.WriteLine("Send greeting back"); // delete

            // TODO: Receive the next message
            // Expected message is a download RequestMSG message containing the file name
            // Receive the message and verify if there are no errors
            recv = socket.ReceiveFrom(buffer, ref remoteEP);
            RequestMSG DownloadRequestMSG = JsonSerializer.Deserialize<RequestMSG>(Encoding.ASCII.GetString(buffer, 0, recv));
            DownloadRequestMSG.From = req.From;
            DownloadRequestMSG.To = req.To;
            
            if (ErrorHandler.VerifyRequest(DownloadRequestMSG, C) == ErrorType.BADREQUEST)
            {
                Console.WriteLine("Download request error from the client side, stopping server"); // delete
                return ErrorType.BADREQUEST;
            }
            // TODO: Send a RequestMSG of type REPLY message to remoteEndpoint verifying the status
            DownloadRequestMSG.Type = req.Type;
            var DownloadReplyMSG = JsonSerializer.Serialize(DownloadRequestMSG); 
            msg = Encoding.ASCII.GetBytes(DownloadReplyMSG);
            socket.SendTo(msg, msg.Length, SocketFlags.None, this.remoteEP);
            Console.WriteLine("Send download reply back"); // delete


            // TODO:  Start sending file data by setting first the socket ReceiveTimeout value
            this.socket.ReceiveTimeout = 1000;


            // TODO: Open and read the text-file first
            // Make sure to locate a path on windows and macos platforms
            // string lines = System.IO.File.ReadAllText(@".\"+DownloadRequestMSG.FileName);
            byte[] lines = System.IO.File.ReadAllBytes(Path.GetFullPath(@"./"+DownloadRequestMSG.FileName));
            System.Console.WriteLine(lines.Length);
            // TODO: Sliding window with go-back-n implementation
            // Calculate the length of data to be sent
            // Send file-content as DataMSG message as long as there are still values to be sent
            // Consider the WINDOW_SIZE and SEGMENT_SIZE when sending a message  
            // Make sure to address the case if remaining bytes are less than WINDOW_SIZE
            int StringIndex = 0;
            while (true)
            {
                int j =0;
                // randomlist = [true,false,false,false,false]
                for (int i = 0; i < (int)Enums.Params.WINDOW_SIZE; i++)
                {

                    try
                    {
                        data.Sequence = j;
                        
                        var DataMSGPacketSend = JsonSerializer.Serialize(data); 
                        msg = Encoding.ASCII.GetBytes(DataMSGPacketSend);
                        socket.SendTo(msg, msg.Length, SocketFlags.None, this.remoteEP);
                        // randomlist[0] = true;
                        recv = socket.ReceiveFrom(buffer, ref remoteEP);
                        AckMSG DataMSGPacketReceive = JsonSerializer.Deserialize<AckMSG>(Encoding.ASCII.GetString(buffer, 0, recv));
                        if (DataMSGPacketReceive.Sequence == data.Sequence)
                        {
                            j++;
                        }
                    }
                    catch (SocketException test)
                    {
                        
                        
                    }

                }
            }
            // Suggestion: while there are still bytes left to send,
            // first you send a full window of data
            // second you wait for the acks
            // then you start again.



            // TODO: Receive and verify the acknowledgements (AckMSG) of sent messages
            // Your client implementation should send an AckMSG message for each received DataMSG message   



            // TODO: Print each confirmed sequence in the console
            // receive the message and verify if there are no errors


            // TODO: Send a CloseMSG message to the client for the current session
            // Send close connection request

            // TODO: Receive and verify a CloseMSG message confirmation for the current session
            // Get close connection confirmation
            // Receive the message and verify if there are no errors


            // Console.WriteLine("Group members: {0} | {1}", student_1, student_2);
            return ErrorType.NOERROR;
        }
    }
}
