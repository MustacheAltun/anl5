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


            byte[] buffer = new byte[1000];
            byte[] msg = new byte[100];
            // TODO: Initialise the socket/s as needed from the description of the assignment
            Socket sock = null;

            HelloMSG h = new HelloMSG()
            {
                Type = Messages.HELLO,
                From = "MyClient",//Dns.GetHostName(),
                To = "MyServer"
            };
            RequestMSG r = new RequestMSG()
            {
                Type = Messages.REQUEST,
                FileName = "test.txt",
                Status = ErrorType.NOERROR
            };
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
                var HelloMSGPackets = JsonSerializer.Serialize(h); 
                msg = Encoding.ASCII.GetBytes(HelloMSGPackets);
                sock.SendTo(msg, msg.Length, SocketFlags.None, ServerEndpoint);
                // Console.WriteLine("Send Hello"); // delete

                // TODO: Receive and verify a HelloMSG 
                int recv = sock.ReceiveFrom(buffer, ref clientEP);
                HelloMSG hellodata = JsonSerializer.Deserialize<HelloMSG>(Encoding.ASCII.GetString(buffer, 0, recv));
                

                // Console.WriteLine("Received Hello"); // delete
                h.ConID = hellodata.ConID;

                // ConIds setups
                ConSettings connectionsettings = new ConSettings()
                {
                    From = h.From,
                    To = h.To,
                    ConID = h.ConID,
                    Sequence = 0

                };
                var tmp = hellodata.To;
                hellodata.To = hellodata.From;
                hellodata.From = tmp;
                var verify = ErrorHandler.VerifyGreeting(hellodata, connectionsettings);
                if (verify == ErrorType.BADREQUEST){
                    Console.WriteLine("Greeting Error, stopping client");
                    return;
                }
                //zet From, To, ConID op elk object
                SetConnection(h, r, D, ack, cls);
                // Console.WriteLine(h.To + r.To + D.To + ack.To + cls.To); // delete

                // TODO: Send the RequestMSG message requesting to download a file name
                //Stuur request
                var RequestMSGPackets = JsonSerializer.Serialize(r);
                msg = Encoding.ASCII.GetBytes(RequestMSGPackets);
                sock.SendTo(msg, msg.Length, SocketFlags.None, ServerEndpoint);
                // Console.WriteLine("Send Request"); // delete

                // TODO: Receive a RequestMSG from remoteEndpoint
                // receive the message and verify if there are no errors
                recv = sock.ReceiveFrom(buffer, ref clientEP);
                RequestMSG requestdata = JsonSerializer.Deserialize<RequestMSG>(Encoding.ASCII.GetString(buffer, 0, recv));
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

                verify = ErrorHandler.VerifyRequest(verifyreply, connectionsettings);
                // Console.WriteLine("Checking request"); // delete
                // Console.WriteLine($"{verify} {r.Status}"); // delete
                if (verify != r.Status && verifyreply.FileName == r.FileName){
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

                    recv = sock.ReceiveFrom(buffer, ref clientEP);
                    DataMSG data = JsonSerializer.Deserialize<DataMSG>(Encoding.ASCII.GetString(buffer, 0, recv));
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
                    if (verify == ErrorType.NOERROR){
                        // ack.Sequence = data.Sequence+10;
                        var AckMSGPackets = JsonSerializer.Serialize(ack);
                        msg = Encoding.ASCII.GetBytes(AckMSGPackets);
                        sock.SendTo(msg, msg.Length, SocketFlags.None, ServerEndpoint);
                    }
                    else {
                        addtext = false;
                    } 
                    if (addtext){
                        textTest += Encoding.ASCII.GetString(data.Data);
                        // Console.WriteLine(textTest); // delete
                        connectionsettings.Sequence++;
                        ackcounter++;
                    }
                    if (data.More == false && verify == ErrorType.NOERROR && addtext){
                        break;
                    } 
                }
                Console.WriteLine(textTest);

                // TODO: Receive close message
                // receive the message and verify if there are no errors
                recv = sock.ReceiveFrom(buffer, ref clientEP);
                CloseMSG closedata = JsonSerializer.Deserialize<CloseMSG>(Encoding.ASCII.GetString(buffer, 0, recv));
                tmp = closedata.To;
                closedata.To = closedata.From;
                closedata.From = tmp;
                verify = ErrorHandler.VerifyClose(closedata, connectionsettings);
                if (verify == ErrorType.BADREQUEST){
                    Console.WriteLine("Closing Error, stopping client");
                    return;
                }

                // TODO: confirm close message
                var CloseMSGPackets = JsonSerializer.Serialize(cls);
                msg = Encoding.ASCII.GetBytes(CloseMSGPackets);
                sock.SendTo(msg, msg.Length, SocketFlags.None, ServerEndpoint);

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