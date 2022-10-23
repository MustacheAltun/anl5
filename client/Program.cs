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
            string student_1 = "Mustafa Altun 1034323";
            string student_2 = "bob 121211";


            byte[] buffer = new byte[1000];
            // byte[] msg = new byte[100];
            Socket sock;
            // TODO: Initialise the socket/s as needed from the description of the assignment

            HelloMSG h = new HelloMSG();
            RequestMSG r = new RequestMSG();
            DataMSG D = new DataMSG();
            AckMSG ack = new AckMSG();
            CloseMSG cls = new CloseMSG();


            
            h.To = "MyServer";
            h.From = student_1;
            h.Type = Messages.HELLO;
            var test = JsonSerializer.Serialize(h);

            byte[] msg = Encoding.ASCII.GetBytes(test);

            // Socket sock;
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            IPEndPoint ServerEndpoint = new IPEndPoint(ipAddress, 5004);
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            EndPoint remoteEP = (EndPoint) sender;
            try {
                sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sock.SendTo(msg, msg.Length, SocketFlags.None, ServerEndpoint);
                int b = sock.ReceiveFrom(buffer, ref remoteEP);
                HelloMSG data = JsonSerializer.Deserialize<HelloMSG>(Encoding.ASCII.GetString(buffer, 0, b));
                Console.WriteLine("Server said" + data.ConID);
                var ConnectionId = data.ConID;
                sock.Close();
            } catch {
                Console.WriteLine("\n Socket Error. Terminating");
            }
            // try
            // {
            //     // TODO: Instantiate and initialize your socket 


            //     // TODO: Send hello mesg

            //     // TODO: Receive and verify a HelloMSG 


            //     // TODO: Send the RequestMSG message requesting to download a file name

            //     // TODO: Receive a RequestMSG from remoteEndpoint
            //     // receive the message and verify if there are no errors


            //     // TODO: Check if there are more DataMSG messages to be received 
            //     // receive the message and verify if there are no errors

            //     // TODO: Send back AckMSG for each received DataMSG 


            //     // TODO: Receive close message
            //     // receive the message and verify if there are no errors

            //     // TODO: confirm close message

            // }
            // catch
            // {
            //     Console.WriteLine("\n Socket Error. Terminating");
            // }

            // Console.WriteLine("Download Complete!");
           
        }
    }
}
