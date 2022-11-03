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
            string Filname_on_server = "test.txt";

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
            Socket socket;

            IPAddress clientIP = IPAddress.Parse("127.0.0.1");
            IPEndPoint ServerEndpoint = new IPEndPoint(clientIP, 5004);
            IPEndPoint client = new IPEndPoint(clientIP, 5010);
            EndPoint clientEP = (EndPoint)client;

            try
            {
                // TODO: Instantiate and initialize your socket
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                // TODO: Send hello mesg
                HelloMSG hello_msg = new HelloMSG(){Type = Messages.HELLO, From = client_consettings.From, To = client_consettings.To};
                var HelloMSGPackets = JsonSerializer.Serialize(hello_msg); 
                message = Encoding.ASCII.GetBytes(HelloMSGPackets);
                socket.SendTo(message, message.Length, SocketFlags.None, ServerEndpoint);
        

                // TODO: Receive and verify a HelloMSG 
                int receive_from_server = socket.ReceiveFrom(buffer, ref clientEP);
                HelloMSG hello_reply = JsonSerializer.Deserialize<HelloMSG>(Encoding.ASCII.GetString(buffer, 0, receive_from_server));
                
                hello_msg.ConID = hello_reply.ConID;
                // ConnectionID is now set
                client_consettings.ConID = hello_reply.ConID;

                hello_reply.To = hello_msg.To;
                hello_reply.From = hello_msg.From;

                
                var message_status = ErrorHandler.VerifyGreeting(hello_reply, client_consettings);
                if (message_status == ErrorType.BADREQUEST){
                    Console.WriteLine("Error! BADREQUEST (Greeting). The client will be stopped.");
                    return;
                }
                RequestMSG request_msg = new RequestMSG(){From=client_consettings.From, To=client_consettings.To, Type = Messages.REQUEST, FileName = Filname_on_server, Status = ErrorType.NOERROR, ConID=client_consettings.ConID};


                // TODO: Send the RequestMSG message requesting to download a file name
                var serialized_request_msg = JsonSerializer.Serialize(request_msg);
                message = Encoding.ASCII.GetBytes(serialized_request_msg);
                socket.SendTo(message, message.Length, SocketFlags.None, ServerEndpoint);
        

                // TODO: Receive a RequestMSG from remoteEndpoint
                // receive the message and verify if there are no errors
                receive_from_server = socket.ReceiveFrom(buffer, ref clientEP);
                RequestMSG server_accept = JsonSerializer.Deserialize<RequestMSG>(Encoding.ASCII.GetString(buffer, 0, receive_from_server));
                
                server_accept.From = client_consettings.From;
                server_accept.To = client_consettings.To;

                message_status = ErrorHandler.VerifyRequest(server_accept, client_consettings);
                if (message_status != request_msg.Status && server_accept.FileName == request_msg.FileName){
                    Console.WriteLine("Error! Request failed. The client will be stopped.");
                    return;
                }

                // TODO: Check if there are more DataMSG messages to be received 
                // receive the message and verify if there are no errors
                int ack_sequence = 0;
                string download_file = "";
                bool sequence_received = true;
                AckMSG acck_message = new AckMSG(){From = client_consettings.From, Type = Messages.ACK, To = client_consettings.To, ConID = client_consettings.ConID};
                while (true){

                    receive_from_server = socket.ReceiveFrom(buffer, ref clientEP);
                    DataMSG server_data_msg = JsonSerializer.Deserialize<DataMSG>(Encoding.ASCII.GetString(buffer, 0, receive_from_server));
                    acck_message.Sequence = server_data_msg.Sequence;

                // TODO: Send back AckMSG for each received DataMSG 
                    if (ack_sequence == server_data_msg.Sequence){
                        sequence_received = true;
                    }
                    if (message_status == ErrorType.NOERROR){
                        var serialized_ack_msg = JsonSerializer.Serialize(acck_message);
                        message = Encoding.ASCII.GetBytes(serialized_ack_msg);
                        socket.SendTo(message, message.Length, SocketFlags.None, ServerEndpoint);
                    }
                    else {
                        sequence_received = false;
                    } 
                    if (sequence_received){
                        download_file += Encoding.ASCII.GetString(server_data_msg.Data);
                        client_consettings.Sequence++;
                        ack_sequence++;
                    }
                    if (server_data_msg.More == false && message_status == ErrorType.NOERROR && sequence_received){
                        break;
                    } 
                }
                Console.WriteLine(download_file);

                // TODO: Receive close message
                // receive the message and verify if there are no errors
                receive_from_server = socket.ReceiveFrom(buffer, ref clientEP);
                CloseMSG close_msg = JsonSerializer.Deserialize<CloseMSG>(Encoding.ASCII.GetString(buffer, 0, receive_from_server));
                close_msg.From = client_consettings.From;
                close_msg.To = client_consettings.To;
                
                message_status = ErrorHandler.VerifyClose(close_msg, client_consettings);
                if (message_status == ErrorType.BADREQUEST){
                    Console.WriteLine("Closing Error, stopping client");
                    return;
                }

                // TODO: confirm close message
                close_msg.Type = Messages.CLOSE_CONFIRM;
                var close_confirm = JsonSerializer.Serialize(close_msg);
                message = Encoding.ASCII.GetBytes(close_confirm);
                socket.SendTo(message, message.Length, SocketFlags.None, ServerEndpoint);

                Console.WriteLine("Download Complete!");
                Console.WriteLine("Stopping Client.");
                socket.Close(); 
            }
            catch
            {
                Console.WriteLine("\n Socket Error. Terminating");
            }

           
        }
        //, RequestMSG request, 
        public static void SetConnection(HelloMSG hello, CloseMSG close){
            // request setup
            // request.From = hello.From;
            // request.To = hello.To;
            // request.ConID = hello.ConID;
            // data setup
            // data.From = hello.From;
            // data.To = hello.To;
            // data.ConID = hello.ConID;
            // ack setup
            
            // close setup
            

        }
    }
}