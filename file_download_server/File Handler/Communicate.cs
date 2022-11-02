using System;
using System.Collections.Generic;
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
        private EndPoint remoteEP;
        private ErrorType Status;
        private byte[] buffer;
        byte[] msg;
        private string file;
        ConSettings C;


        public Communicate()
        {
            // TODO: Initializes another instance of the IPEndPoint for the remote host
            IPAddress serverIP = IPAddress.Parse("127.0.0.1");
            this.remoteEndpoint = new IPEndPoint(serverIP, 5010);

            // TODO: Specify the buffer size
            this.buffer = new byte[1000];
            this.msg = new byte[100];

            // TODO: Get a random SessionID
            var allids = new List<int>();
            while(true){
                bool notallid = true;
                Random conid = new Random();
                var tmp = conid.Next(999999934);
                foreach(var currentid in allids){
                    if(SessionID == currentid){
                        notallid = false;
                        break;
                    }
                }
                if (notallid == true){
                    allids.Add(tmp);
                    this.SessionID = tmp;
                    break;
                }

            }

            // TODO: Create local IPEndpoints and a Socket to listen 
            //       Keep using port numbers and protocols mention in the assignment description
            //       Associate a socket to the IPEndpoints to start the communication
            IPEndPoint server = new IPEndPoint(serverIP, 5004);
            this.remoteEP = (EndPoint)server;
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        }

        public ErrorType StartDownload()
        {
            // TODO: Instantiate and initialize different messages needed for the communication
            // required messages are: HelloMSG, RequestMSG, DataMSG, AckMSG, CloseMSG
            // Set attribute values for each class accordingly 
            HelloMSG GreetBack = new HelloMSG(){
                Type = Messages.HELLO_REPLY,
                From = Server,
                ConID = this.SessionID
            };
            RequestMSG req = new RequestMSG(){
                Type = Messages.REPLY,
                Status = ErrorType.NOERROR
            };
            DataMSG data = new DataMSG(){
                Type = Messages.DATA
            };
            AckMSG ack = new AckMSG(){
                Type = Messages.ACK
            };
            CloseMSG cls = new CloseMSG(){
                Type = Messages.CLOSE_REQUEST
            };
            C = new ConSettings{
            To = GreetBack.From
            };


            // TODO: Start the communication by receiving a HelloMSG message
            // Receive and deserialize HelloMSG message 
            // Verify if there are no errors
            // Type must match one of the ConSettings' types and receiver address must be the server address
            socket.Bind(remoteEP);
            int recv = socket.ReceiveFrom(buffer, ref remoteEP);
            HelloMSG hellodata = JsonSerializer.Deserialize<HelloMSG>(Encoding.ASCII.GetString(buffer, 0, recv));
            var verify = ErrorHandler.VerifyGreeting(hellodata, C);
            if (verify == ErrorType.BADREQUEST){
                Console.WriteLine("Greeting Error, closing socket");
                socket.Close();
                return ErrorType.CONNECTION_ERROR;
            }
            GreetBack.To = hellodata.From;
            // Console.WriteLine("Received Hello from client" + remoteEP);

            // TODO: If no error is found then HelloMSG will be sent back
            SetConnection(GreetBack, req, data, ack, cls, C);
            var HelloMSGPackets = JsonSerializer.Serialize(GreetBack); 
            msg = Encoding.ASCII.GetBytes(HelloMSGPackets);
            socket.SendTo(msg, msg.Length, SocketFlags.None, remoteEP);
            // Console.WriteLine("Sending Hello to client");

            // TODO: Receive the next message
            // Expected message is a download RequestMSG message containing the file name
            // Receive the message and verify if there are no errors
            recv = socket.ReceiveFrom(buffer, ref remoteEP);
            RequestMSG requestdata = JsonSerializer.Deserialize<RequestMSG>(Encoding.ASCII.GetString(buffer, 0, recv));
            
            verify = ErrorHandler.VerifyRequest(requestdata, C);
            Console.WriteLine($"{requestdata.From} {requestdata.To}");
            Console.WriteLine($"{C.From} {C.To}");
            if (verify == ErrorType.BADREQUEST){
                Console.WriteLine("Request Error, stopping client");
                return ErrorType.BADREQUEST;
            }
            req.FileName = requestdata.FileName;

            // TODO: Send a RequestMSG of type REPLY message to remoteEndpoint verifying the status
            var ReplyMSGPackets = JsonSerializer.Serialize(req);
            msg = Encoding.ASCII.GetBytes(ReplyMSGPackets);
            socket.SendTo(msg, msg.Length, SocketFlags.None, remoteEP);

            // TODO:  Start sending file data by setting first the socket ReceiveTimeout value
            socket.ReceiveTimeout = 5000;

            // TODO: Open and read the text-file first
            // Make sure to locate a path on windows and macos platforms
            
            string lines = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(requestdata.FileName));

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
                for (int i = 0; i < (int)Enums.Params.WINDOW_SIZE && windowsizecounter != dataList.Count; i++)
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

                        recv = socket.ReceiveFrom(buffer, ref remoteEP);
                        DataMSG ackReceive = JsonSerializer.Deserialize<DataMSG>(Encoding.ASCII.GetString(buffer, 0, recv));

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


            // TODO: Receive and verify the acknowledgements (AckMSG) of sent messages
            // Your client implementation should send an AckMSG message for each received DataMSG message   



            // TODO: Print each confirmed sequence in the console
            // receive the message and verify if there are no errors


            // TODO: Send a CloseMSG message to the client for the current session
            // Send close connection request

            // TODO: Receive and verify a CloseMSG message confirmation for the current session
            // Get close connection confirmation
            // Receive the message and verify if there are no errors


            //Console.WriteLine("Group members: {0} | {1}", student_1, student_2);
            return ErrorType.NOERROR;
        }
        public static void SetConnection(HelloMSG hello, RequestMSG request, DataMSG data, AckMSG ack, CloseMSG close, ConSettings C){
            // request setup
            request.From = hello.From;
            request.To = hello.To;
            request.ConID = hello.ConID;
            // data setup
            data.From = hello.From;
            data.To = hello.To;
            data.ConID = hello.ConID;
            // ack setup
            ack.From = hello.From;
            ack.To = hello.To;
            ack.ConID = hello.ConID;
            // close setup
            close.From = hello.From;
            close.To = hello.To;
            close.ConID = hello.ConID;
            // consetting setup
            C.To = hello.From;
            C.From = hello.To;
            C.ConID = hello.ConID;
        }
    }
}
