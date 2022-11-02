using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using UDP_FTP.Models;
using UDP_FTP.Error_Handling;
using static UDP_FTP.Models.Enums;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            // TODO: add the student number of your group members as a string value. 
            // string format example: "Jan Jansen 09123456" 
            // If a group has only one member fill in an empty string for the second student
            string student_1 = "Min En Cheng 1037939";
            string student_2 = "Reajel Cicilia 1018371";


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
            IPEndPoint client = new IPEndPoint(IPAddress.Any, 5010);
            EndPoint clientEP = (EndPoint)client;

            try
            {
                // TODO: Instantiate and initialize your socket
                sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                // TODO: Send hello mesg
                var HelloMSGPackets = JsonSerializer.Serialize(h); 
                msg = Encoding.ASCII.GetBytes(HelloMSGPackets);
                sock.SendTo(msg, msg.Length, SocketFlags.None, ServerEndpoint);
                Console.WriteLine("Send Hello"); // delete

                // TODO: Receive and verify a HelloMSG 
                int recv = sock.ReceiveFrom(buffer, ref clientEP);
                HelloMSG hellodata = JsonSerializer.Deserialize<HelloMSG>(Encoding.ASCII.GetString(buffer, 0, recv));
                if (hellodata.Type == Messages.HELLO_REPLY)
                {
                    Console.WriteLine("Received Hello"); // delete
                }
                // buffer = new byte[1000]    ???
                // msg = new byte[100];
                h.ConID = hellodata.ConID;

                // ConIds setups
                ConSettings connectionsettings = new ConSettings()
                {
                    From = h.From,
                    To = h.To,
                    ConID = h.ConID,
                    Sequence = 0

                };
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
                
                RequestMSG verifyreply = new RequestMSG();
                VerifyReplyFunction(verifyreply,requestdata);

                var verify = ErrorHandler.VerifyRequest(verifyreply, connectionsettings);
                // Console.WriteLine("Checking request"); // delete
                // Console.WriteLine($"{verify} {r.Status}"); // delete
                if (verify != r.Status && verifyreply.FileName == r.FileName){
                    Console.WriteLine("Request Error, stopping client");
                    return;
                }

                // TODO: Check if there are more DataMSG messages to be received 
                // receive the message and verify if there are no errors
                int ackcounter = 0;
                string textTest = "";
                bool addtext = true;
                while (true){
                    recv = sock.ReceiveFrom(buffer, ref clientEP);
                    DataMSG data = JsonSerializer.Deserialize<DataMSG>(Encoding.ASCII.GetString(buffer, 0, recv));
                    ack.Sequence = data.Sequence;
                    verify = ErrorHandler.VerifyAck(ack, connectionsettings);
                    // Console.WriteLine(data.Sequence); // see which seq het is -delete-
                    // Console.WriteLine(verify); // bekijk of error is  -delete-
                // TODO: Send back AckMSG for each received DataMSG 
                    if (ackcounter == data.Sequence){
                        addtext = true;
                    }
                    if (verify == ErrorType.NOERROR){
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
                    if (data.More == false && verify == ErrorType.NOERROR){
                        break;
                    } 
                }
                Console.WriteLine(textTest);
                // TODO: Receive close message
                // receive the message and verify if there are no errors
                var RequestClose = JsonSerializer.Serialize(cls);
                msg = Encoding.ASCII.GetBytes(RequestClose);
                sock.SendTo(msg, msg.Length, SocketFlags.None, ServerEndpoint);

                recv = sock.ReceiveFrom(buffer, ref clientEP);
                CloseMSG closedata = JsonSerializer.Deserialize<CloseMSG>(Encoding.ASCII.GetString(buffer, 0, recv));
                // TODO: confirm close message
                if (closedata.Type == Messages.CLOSE_REQUEST)
                {
                    System.Console.WriteLine("close request is received");
                    Console.WriteLine("Download Complete!");
                    Console.WriteLine("Stopping Client.");
                }
                

                
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
        public static void VerifyReplyFunction(RequestMSG reply, RequestMSG requestdata){
            reply.Type = requestdata.Type;
            reply.From = requestdata.To;
            reply.To = requestdata.From;
            reply.FileName = requestdata.FileName;
            reply.ConID = requestdata.ConID;
            reply.Status = requestdata.Status;
            
        }
    }
}
