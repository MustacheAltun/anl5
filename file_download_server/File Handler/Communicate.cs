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
            // HelloMSG GreetBack = new HelloMSG()
            // {
            //     Type = Messages.HELLO_REPLY,
            //     From = "MyServer",
            //     To = "MyClient",
            //     ConID = this.SessionID
            // };
            // RequestMSG req = new RequestMSG()
            // {
            //     From = "MyServer",
            //     To = "MyClient",
            //     Type = Messages.REPLY,
            //     Status = ErrorType.NOERROR,
            //     ConID = this.SessionID
            // };
            DataMSG data = new DataMSG()
            {
                From = "MyServer",
                To = "MyClient",
                Type = Messages.DATA,
                ConID = this.SessionID
            };
            // AckMSG ack = new AckMSG()
            // {                
            //     From = "MyServer",
            //     To = "MyClient",
            //     Type = Messages.ACK,
            //     ConID = this.SessionID
            // };
            CloseMSG cls = new CloseMSG()
            {
                From = "MyServer",
                To = "MyClient",
                Type = Messages.CLOSE_REQUEST,
                ConID = this.SessionID
            };
            this.C = new ConSettings()
                {
                    From = "MyServer",
                    To = "MyClient",
                    ConID = this.SessionID,
                    Sequence = 0
                };
            // TODO: Start the communication by receiving a HelloMSG message
            // Receive and deserialize HelloMSG message 
            // Verify if there are no errors
            // Type must match one of the ConSettings' types and receiver address must be the server address
            int recv = socket.ReceiveFrom(buffer, ref remoteEP);
            HelloMSG hellodata = JsonSerializer.Deserialize<HelloMSG>(Encoding.ASCII.GetString(buffer, 0, recv));
            hellodata.From = C.From;
            hellodata.To = C.To;
            hellodata.Type = Messages.HELLO_REPLY;
            hellodata.ConID = C.ConID;
            
            // Console.WriteLine("receive Hello"); // delete
            // TODO: If no error is found then HelloMSG will be sent back
            var HelloMSGPackets = JsonSerializer.Serialize(hellodata); 
            msg = Encoding.ASCII.GetBytes(HelloMSGPackets);
            socket.SendTo(msg, msg.Length, SocketFlags.None, this.remoteEP);

            // Console.WriteLine("Send greeting back"); // delete
            // TODO: Receive the next message
            // Expected message is a download RequestMSG message containing the file name
            // Receive the message and verify if there are no errors
            recv = socket.ReceiveFrom(buffer, ref remoteEP);
            RequestMSG DownloadRequestMSG = JsonSerializer.Deserialize<RequestMSG>(Encoding.ASCII.GetString(buffer, 0, recv));
            DownloadRequestMSG.From = C.From;
            DownloadRequestMSG.To = C.To;
            
            if (ErrorHandler.VerifyRequest(DownloadRequestMSG, C) == ErrorType.BADREQUEST)
            {
                Console.WriteLine("Download request error from the client side, stopping server"); // delete
                return ErrorType.BADREQUEST;
            }

            // TODO: Send a RequestMSG of type REPLY message to remoteEndpoint verifying the status
            DownloadRequestMSG.Type = Messages.REPLY;
            var DownloadReplyMSG = JsonSerializer.Serialize(DownloadRequestMSG); 
            msg = Encoding.ASCII.GetBytes(DownloadReplyMSG);
            socket.SendTo(msg, msg.Length, SocketFlags.None, this.remoteEP);

            // Console.WriteLine("Send download reply back"); // delete
            // TODO:  Start sending file data by setting first the socket ReceiveTimeout value
            this.socket.ReceiveTimeout = 1000;

            // TODO: Open and read the text-file first
            // Make sure to locate a path on windows and macos platforms
            // string lines = System.IO.File.ReadAllText(@".\"+DownloadRequestMSG.FileName);
            string lines = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(DownloadRequestMSG.FileName));
            System.Console.WriteLine(System.IO.Path.GetFullPath(DownloadRequestMSG.FileName));

            // TODO: Sliding window with go-back-n implementation
            // Calculate the length of data to be sent
            // Send file-content as DataMSG message as long as there are still values to be sent
            // Consider the WINDOW_SIZE and SEGMENT_SIZE when sending a message  
            // Make sure to address the case if remaining bytes are less than WINDOW_SIZE
            //
            // Suggestion: while there are still bytes left to send,
            // first you send a full window of data
            // second you wait for the acks
            // then you start again.
            var dataList = new List<string>();
            string dataSizeString = "";
            for(int i = 0; i < lines.Length; i++){
                dataSizeString += lines[i];
                if (dataSizeString.Length == (int)Enums.Params.SEGMENT_SIZE || i == lines.Length-1)
                {
                    dataList.Add(dataSizeString);
                    dataSizeString = "";
                }
            }
            //var myList = new List<>


            // int indexofdatabytes = 0;
            int windowsizecounter = 0;
            int sequenceCounter = 0;
            int sequenceOfLostData = 0;
            
            while(true)
            {
                bool nodatalost = true;
                int forloopcounter = (int)Enums.Params.WINDOW_SIZE;
                // && sequenceCounter <= dataList.Count-1
                for (int i = 0; i < forloopcounter && sequenceCounter <= dataList.Count-1; i++)
                {
                    try
                    {
                        data.Size = dataList[sequenceCounter].Length;
                        data.Sequence = sequenceCounter;
                        data.Data = Encoding.ASCII.GetBytes(dataList[sequenceCounter]);
                        data.More = true;
                        if (data.Size < (int)Enums.Params.SEGMENT_SIZE){
                            data.More = false;
                        }
                        
                        var DataMSGPackets = JsonSerializer.Serialize(data);
                        msg = Encoding.ASCII.GetBytes(DataMSGPackets);
                        socket.SendTo(msg, msg.Length, SocketFlags.None, remoteEP);

                        // TODO: Receive and verify the acknowledgements (AckMSG) of sent messages
                        // Your client implementation should send an AckMSG message for each received DataMSG message  
                        recv = socket.ReceiveFrom(buffer, ref remoteEP);
                        AckMSG ackReceive = JsonSerializer.Deserialize<AckMSG>(Encoding.ASCII.GetString(buffer, 0, recv));
                        // System.Console.WriteLine(DataMSGPackets);
                        // TODO: Print each confirmed sequence in the console
                        // receive the message and verify if there are no errors
                        ackReceive.From = C.From;
                        ackReceive.To = C.To;
                        C.Sequence = sequenceCounter;
                        if (ErrorHandler.VerifyAck(ackReceive, C) == ErrorType.BADREQUEST)
                        {
                            System.Console.WriteLine("ack badrequest, stopping the server");
                            nodatalost = false;
                        }
                        System.Console.WriteLine("Ack Sequence "+ sequenceCounter);
                        // && !nodatalost
                        if (data.More == false && !nodatalost)
                        {
                            break;
                        }
                    }
                    catch (SocketException test)
                    {
                        nodatalost = false;

                    }
                    // indexofdatabytes++;
                    sequenceCounter++;
                    if (nodatalost)
                    {
                        sequenceOfLostData++;
                    }

                }
                if (data.More == false && nodatalost)
                {
                    break;
                }
                else if (nodatalost)
                {

                    windowsizecounter++;
                }
                else if (!nodatalost)
                {
                    sequenceCounter = sequenceOfLostData;
                }
            }



            // TODO: Send a CloseMSG message to the client for the current session
            // Send close connection request
            var RequestClose = JsonSerializer.Serialize(cls); 
            msg = Encoding.ASCII.GetBytes(RequestClose);
            socket.SendTo(msg, msg.Length, SocketFlags.None, this.remoteEP);

            // TODO: Receive and verify a CloseMSG message confirmation for the current session
            // Get close connection confirmation
            // Receive the message and verify if there are no errors
            recv = socket.ReceiveFrom(buffer, ref remoteEP);
            CloseMSG ReceiveClose = JsonSerializer.Deserialize<CloseMSG>(Encoding.ASCII.GetString(buffer, 0, recv));
            ReceiveClose.From = C.From;
            ReceiveClose.To = C.To;
            if (ErrorHandler.VerifyClose(ReceiveClose, C) == ErrorType.BADREQUEST){
                    Console.WriteLine("Closing Error, stopping client");
                    return ErrorType.BADREQUEST;
            }

            // Console.WriteLine("Group members: {0} | {1}", student_1, student_2);
            return ErrorType.NOERROR;
        }
    }
}