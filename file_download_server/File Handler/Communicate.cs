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
        private IPEndPoint ipEndpointSender;
        private EndPoint remoteEP;
        private ErrorType Status;
        private byte[] buffer;
        byte[] message_close_Request;
        private string file;
        ConSettings sever_Consettings;

        public Communicate()
        {   
            this.Client = "MyClient";
            // TODO: Initializes another instance of the IPEndPoint for the remote host
            this.remoteEndpoint = new IPEndPoint(IPAddress.Any, 5010);
            // TODO: Specify the buffer size
            this.buffer = new byte[1000];
            // TODO: Get a random SessionID
            Random randomSessionID = new Random();
            this.SessionID = randomSessionID.Next(1453517479);
            // TODO: Create local IPEndpoints and a Socket to listen 
            //       Keep using port numbers and protocols mention in the assignment description
            //       Associate a socket to the IPEndpoints to start the communication
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            this.ipEndpointSender = new IPEndPoint(ipAddress, 5004);
            this.remoteEP = (EndPoint)ipEndpointSender;
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(ipEndpointSender);
        }
        public ErrorType StartDownload()
        {
            // TODO: Instantiate and initialize different messages needed for the communication
            // required messages are: HelloMSG, RequestMSG, DataMSG, AckMSG, CloseMSG
            // Set attribute values for each class accordingly 
            
            //Instance of connection setting
            this.sever_Consettings = new ConSettings(){ From = Server, To = this.Client, ConID = this.SessionID, Sequence = 0};

            // TODO: Start the communication by receiving a HelloMSG message
            // Receive and deserialize HelloMSG message 
            // Verify if there are no errors
            // Type must match one of the ConSettings' types and receiver address must be the server address

            //Receiving Hello message from remote endpoint (client)
            int receive_from_client = socket.ReceiveFrom(buffer, ref remoteEP);
            //Deserializing the received string into a Hello message object.
            HelloMSG hellodata = JsonSerializer.Deserialize<HelloMSG>(Encoding.ASCII.GetString(buffer, 0, receive_from_client));

            //Reusing the object received to write a reply to the client.
            hellodata.From = sever_Consettings.From;
            hellodata.To = sever_Consettings.To;
            hellodata.Type = Messages.HELLO_REPLY;
            hellodata.ConID = sever_Consettings.ConID;
            
            // TODO: If no error is found then HelloMSG will be sent back

            //Serializing the Hello message object in a string for it to be sent.
            var HelloMSGPackets = JsonSerializer.Serialize(hellodata); 
            message_close_Request = Encoding.ASCII.GetBytes(HelloMSGPackets);
            socket.SendTo(message_close_Request, message_close_Request.Length, SocketFlags.None, this.remoteEP);

            // TODO: Receive the next message
            // Expected message is a download RequestMSG message containing the file name
            // Receive the message and verify if there are no errors

            //Receive request meesage from client
            receive_from_client = socket.ReceiveFrom(buffer, ref remoteEP);
            RequestMSG client_download_request = JsonSerializer.Deserialize<RequestMSG>(Encoding.ASCII.GetString(buffer, 0, receive_from_client));
            client_download_request.From = sever_Consettings.From;
            client_download_request.To = sever_Consettings.To;

            if (ErrorHandler.VerifyRequest(client_download_request, sever_Consettings) == ErrorType.BADREQUEST)
            {
                Console.WriteLine("Error! Download request error (client side). The server will be stopped.");
                return ErrorType.BADREQUEST;
            }

            // TODO: Send a RequestMSG of type REPLY message to remoteEndpoint verifying the status
            client_download_request.Type = Messages.REPLY;

            //Send download request reply back to the client.
            var server_download_reply = JsonSerializer.Serialize(client_download_request); 
            message_close_Request = Encoding.ASCII.GetBytes(server_download_reply);
            socket.SendTo(message_close_Request, message_close_Request.Length, SocketFlags.None, this.remoteEP);

    
            // TODO:  Start sending file data by setting first the socket ReceiveTimeout value

            //Determining the socket timeout for receiving an acknowledgement from client.
            this.socket.ReceiveTimeout = 1000;

            // TODO: Open and read the text-file first
            // Make sure to locate a path on windows and macos platforms
            // string lines = System.IO.File.ReadAllText(@".\"+DownloadRequestMSG.FileName);

            //Reading the whole file and putting it into a string value.
            string completeFileData = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(client_download_request.FileName)); 
            System.Console.WriteLine(System.IO.Path.GetFullPath(client_download_request.FileName));

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

            var segmented_File_Data_List = new List<string>();
            string data_segment_string = "";
            //This forloop iterates through the complete file data to divide the data into the given segment size.
            //The list of data segments loads the segmented messages to be send in a datamessage object.
            for(int i = 0; i < completeFileData.Length; i++){
                data_segment_string += completeFileData[i];
                if (data_segment_string.Length == (int)Enums.Params.SEGMENT_SIZE || i == completeFileData.Length-1)
                {
                    segmented_File_Data_List.Add(data_segment_string);
                    data_segment_string = "";
                }
            }


            int counter_WindowSize = 0;
            int counter_Sequence = 0;
            int counter_SequenceDataloss = 0;
            DataMSG data = new DataMSG(){ From = sever_Consettings.From, To = sever_Consettings.To, Type = Messages.DATA, ConID = sever_Consettings.ConID};
    
            while(true)
            {
                bool noDatalost = true;
                int windowSize = (int)Enums.Params.WINDOW_SIZE;

                for (int i = 0; i < windowSize && counter_Sequence <= segmented_File_Data_List.Count-1; i++)
                {
                    //The data will be sent out the client (Try) and an acknowledgement will be expected.
                    //If no ack is received for the sequence within the givin socket timeout the exception will be catched and handled.
                    //The forloop will continue executing until the windowsize is reached or there is is no more datafile segments that need to be sent.
                    try
                    {
                        //Preparing the datagram with the data size, sequence, data value and indicating whether this is the last frame or not.
                        data.Size = segmented_File_Data_List[counter_Sequence].Length;
                        data.Sequence = counter_Sequence;
                        data.Data = Encoding.ASCII.GetBytes(segmented_File_Data_List[counter_Sequence]);
                        data.More = true;

                        //If data segment size is smaller than the given segment size, this indicates that this is the last datagram sequence that must be sent.
                        if (data.Size < (int)Enums.Params.SEGMENT_SIZE){
                            data.More = false;
                        }
                        
                        //The data is serialized and sent out to the client endpoint.
                        var DataMSGPackets = JsonSerializer.Serialize(data);
                        message_close_Request = Encoding.ASCII.GetBytes(DataMSGPackets);
                        socket.SendTo(message_close_Request, message_close_Request.Length, SocketFlags.None, remoteEP);

                        // TODO: Receive and verify the acknowledgements (AckMSG) of sent messages
                        // Your client implementation should send an AckMSG message for each received DataMSG message  

                        receive_from_client = socket.ReceiveFrom(buffer, ref remoteEP);
                        AckMSG receive_Ack = JsonSerializer.Deserialize<AckMSG>(Encoding.ASCII.GetString(buffer, 0, receive_from_client));
                    
                        // TODO: Print each confirmed sequence in the console
                        // receive the message and verify if there are no errors
                        receive_Ack.From = sever_Consettings.From;
                        receive_Ack.To = sever_Consettings.To;
                        sever_Consettings.Sequence = counter_Sequence;

                        if (ErrorHandler.VerifyAck(receive_Ack, sever_Consettings) == ErrorType.BADREQUEST)
                        {
                            System.Console.WriteLine("Error! BADREQUEST (Acknowlegdement), the server is retrying.");
                            noDatalost = false;
                        }
                        System.Console.WriteLine("Ack Sequence "+ counter_Sequence);
                        
                        //If the last datagram has been sent and there were no ack issues the loop wil be broken.
                        if (data.More == false && !noDatalost)
                        {
                            break;
                        }
                    }
                    catch (SocketException socketExceptionError)
                    {
                        noDatalost = false;
                    }
                    counter_Sequence++;

                    if (noDatalost)
                    {
                        counter_SequenceDataloss++;
                    }
                }
                if (data.More == false && noDatalost)
                {
                    break;
                }
                else if (noDatalost)
                {
                    counter_WindowSize++;
                }
                else if (!noDatalost)
                {
                    counter_Sequence = counter_SequenceDataloss;
                }
            }


            // TODO: Send a CloseMSG message to the client for the current session
            // Send close connection request

            //Once the data has been sent succesfully the server will initiate closing message and send this to the client.
            CloseMSG sever_close_Request = new CloseMSG(){From = sever_Consettings.From, To = sever_Consettings.To, Type = Messages.CLOSE_REQUEST, ConID = sever_Consettings.ConID};
            var RequestClose = JsonSerializer.Serialize(sever_close_Request); 
            message_close_Request = Encoding.ASCII.GetBytes(RequestClose);
            socket.SendTo(message_close_Request, message_close_Request.Length, SocketFlags.None, this.remoteEP);

            // TODO: Receive and verify a CloseMSG message confirmation for the current session
            // Get close connection confirmation
            // Receive the message and verify if there are no errors

            //The server waits for the client to confirm the closing of the socket.
            receive_from_client = socket.ReceiveFrom(buffer, ref remoteEP);
            CloseMSG receive_close_confirm = JsonSerializer.Deserialize<CloseMSG>(Encoding.ASCII.GetString(buffer, 0, receive_from_client));
            receive_close_confirm.From = sever_Consettings.From;
            receive_close_confirm.To = sever_Consettings.To;
            

            if (ErrorHandler.VerifyClose(receive_close_confirm, sever_Consettings) == ErrorType.BADREQUEST){
                    Console.WriteLine("Error! BADREQUEST (Closing), the server will be stopped.");
                    return ErrorType.BADREQUEST;
            }
            return ErrorType.NOERROR;
        }
    }
}