using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using UDP_FTP.Models;
using UDP_FTP.Error_Handling;
using static UDP_FTP.Models.Enums;
using System.Linq;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            // TODO: add the student number of your group members as a string value. 
            // string format example: "Jan Jansen 09123456" 
            // If a group has only one member fill in an empty string for the second student
            string student_1 = "Mustafa Altun 1034323";
            string student_2 = "Marc-Shaquille Martina 1001658";

            string Server = "MyServer";
            string Client = "MyClient";

                ConSettings client_consettings = new ConSettings()
                {
                    From = Client,
                    To = Server,
                    Sequence = 0
                    //Missing SessionID, but this will be determined once a reply is received from the server.
                };

            byte[] buffer = new byte[1000];
            byte[] message = new byte[100];
            // TODO: Initialise the socket/s as needed from the description of the assignment
            Socket sock = null;

            DataMSG D = new DataMSG()
            {
                Type = Messages.DATA
            };
            AckMSG ack = new AckMSG()
            {
                Type = Messages.ACK
            };
            CloseMSG cls = new CloseMSG()
            {
                Type = Messages.CLOSE_CONFIRM
            };

            IPAddress clientIP = IPAddress.Parse("127.0.0.1");
            IPEndPoint ServerEndpoint = new IPEndPoint(clientIP, 5004);
            IPEndPoint client = new IPEndPoint(clientIP, 5010);
            EndPoint clientEP = (EndPoint)client;

            try
            {
                // TODO: Instantiate and initialize your socket
                sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                // sock.ReceiveTimeout = 5000;
                // TODO: Send hello mesg
                HelloMSG hello_msg = new HelloMSG(){Type = Messages.HELLO, From = client_consettings.From, To = client_consettings.To};
                var HelloMSGPackets = JsonSerializer.Serialize(hello_msg); 
                message = Encoding.ASCII.GetBytes(HelloMSGPackets);
                sock.SendTo(message, message.Length, SocketFlags.None, ServerEndpoint);
        

                // TODO: Receive and verify a HelloMSG 
                int receive_from_server = sock.ReceiveFrom(buffer, ref clientEP);
                HelloMSG hello_reply = JsonSerializer.Deserialize<HelloMSG>(Encoding.ASCII.GetString(buffer, 0, receive_from_server));
                
                hello_msg.ConID = hello_reply.ConID;
                // ConnectionID is now set
                client_consettings.ConID = hello_reply.ConID;

                string tmp;
                // var tmp = hello_reply.To;
                // hello_reply.To = hello_reply.From;
                // hello_reply.From = tmp;
                hello_reply.To = hello_msg.To;
                hello_reply.From = hello_msg.From;

                
                var verify_message = ErrorHandler.VerifyGreeting(hello_reply, client_consettings);
                if (verify_message == ErrorType.BADREQUEST){
                    Console.WriteLine("Error! BADREQUEST (Greeting). The client will be stopped.");
                    return;
                }
                RequestMSG r = new RequestMSG(){ Type = Messages.REQUEST, FileName = "test.txt", Status = ErrorType.NOERROR};
                //zet From, To, ConID op elk object

                // SetConnection(hello_msg, r, D, ack, cls);
                
                // Console.WriteLine(h.To + r.To + D.To + ack.To + cls.To); // delete

                // TODO: Send the RequestMSG message requesting to download a file name
                //Stuur request
                var RequestMSGPackets = JsonSerializer.Serialize(r);
                message = Encoding.ASCII.GetBytes(RequestMSGPackets);
                sock.SendTo(message, message.Length, SocketFlags.None, ServerEndpoint);
                // Console.WriteLine("Send Request"); // delete

                // TODO: Receive a RequestMSG from remoteEndpoint
                // receive the message and verify if there are no errors
                receive_from_server = sock.ReceiveFrom(buffer, ref clientEP);
                RequestMSG requestdata = JsonSerializer.Deserialize<RequestMSG>(Encoding.ASCII.GetString(buffer, 0, receive_from_server));
                // Console.WriteLine("Received Request"); // delete
                // buffer = new byte[1000];    reset or nah?
                // msg = new byte[100];
                // Console.WriteLine($"{requestdata.From} {requestdata.To} {requestdata.ConID} {requestdata.Status}"); // delete
                // Console.WriteLine($"{connectionsettings.From} {connectionsettings.To} {connectionsettings.ConID}"); // delete
                // Console.WriteLine(requestdata.Type != Messages.REPLY); // delete
                
                RequestMSG verifyreply = new RequestMSG(){
                    Type = requestdata.Type,
                    From = requestdata.To,
                    To = requestdata.From,
                    FileName = requestdata.FileName,
                    ConID = requestdata.ConID,
                    Status = requestdata.Status
                };

                verify_message = ErrorHandler.VerifyRequest(verifyreply, client_consettings);
                // Console.WriteLine("Checking request"); // delete
                // Console.WriteLine($"{verify} {r.Status}"); // delete
                if (verify_message != r.Status && verifyreply.FileName == r.FileName){
                    Console.WriteLine("Request Error, stopping client");
                    return;
                }

                // TODO: Check if there are more DataMSG messages to be received 
                // receive the message and verify if there are no errors
                //rndlist -delete-
                // var mylist = new List<int>(){
                //     0, 0, 0, 1, 0, 1, 0, 0
                // };
                int ackcounter = 0;
                string textTest = "";
                bool addtext = true;
                while (true){
                    //rndizer -delete-
                    // Random rng = new Random();
                    // var rnglist = mylist.OrderBy(item => rng.Next());

                    receive_from_server = sock.ReceiveFrom(buffer, ref clientEP);
                    DataMSG data = JsonSerializer.Deserialize<DataMSG>(Encoding.ASCII.GetString(buffer, 0, receive_from_server));
                    // Console.WriteLine($"conid:{data.ConID} size:{data.Size} more:{data.More} seq:{data.Sequence}");
                    // foreach(var item in data.Data){
                    // Console.WriteLine(item);
                    // Console.WriteLine(Encoding.ASCII.GetString(data.Data));
                    // }
                    ack.Sequence = data.Sequence;
                    // Console.WriteLine(data.Sequence); // see which seq het is -delete-
                    // Console.WriteLine(verify); // bekijk of error is  -delete-

                // TODO: Send back AckMSG for each received DataMSG 
                    if (ackcounter == data.Sequence){
                        addtext = true;
                    }
                    //(rnglist.ElementAt(1) == 0 && verify == ErrorType.NOERROR){
                    if (verify_message == ErrorType.NOERROR){
                        // ack.Sequence = data.Sequence+10;
                        var AckMSGPackets = JsonSerializer.Serialize(ack);
                        message = Encoding.ASCII.GetBytes(AckMSGPackets);
                        sock.SendTo(message, message.Length, SocketFlags.None, ServerEndpoint);
                    }
                    else {
                        addtext = false;
                    } 
                    if (addtext){
                        textTest += Encoding.ASCII.GetString(data.Data);
                        // Console.WriteLine(textTest); // delete
                        client_consettings.Sequence++;
                        ackcounter++;
                    }
                    if (data.More == false && verify_message == ErrorType.NOERROR && addtext){
                        break;
                    } 
                }
                Console.WriteLine(textTest);

                // TODO: Receive close message
                // receive the message and verify if there are no errors
                receive_from_server = sock.ReceiveFrom(buffer, ref clientEP);
                CloseMSG closedata = JsonSerializer.Deserialize<CloseMSG>(Encoding.ASCII.GetString(buffer, 0, receive_from_server));
                tmp = closedata.To;
                closedata.To = closedata.From;
                closedata.From = tmp;
                verify_message = ErrorHandler.VerifyClose(closedata, client_consettings);
                if (verify_message == ErrorType.BADREQUEST){
                    Console.WriteLine("Closing Error, stopping client");
                    return;
                }

                // TODO: confirm close message
                var CloseMSGPackets = JsonSerializer.Serialize(cls);
                message = Encoding.ASCII.GetBytes(CloseMSGPackets);
                sock.SendTo(message, message.Length, SocketFlags.None, ServerEndpoint);

                Console.WriteLine("Download Complete!");
                Console.WriteLine("Stopping Client.");
                sock.Close(); 
            }
            catch
            {
                Console.WriteLine("\n Socket Error. Terminating");
            }

           
        }
        public static void SetConnection(HelloMSG hello, RequestMSG request, DataMSG data, AckMSG ack, CloseMSG close){
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

        }
    }
}