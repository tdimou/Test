using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Google.FlatBuffers;
using hyperhdrnet;

namespace HyperTizen
{
    public static class Networking
    {
        public static TcpClient client;
        public static NetworkStream stream;

        public static void SendRegister()
        {
            client = new TcpClient(Globals.Instance.ServerIp, Globals.Instance.ServerPort);
            stream = Networking.client.GetStream();
            byte[] registrationMessage = Networking.CreateRegistrationMessage(true);
            var header = new byte[4];
            header[0] = (byte)((registrationMessage.Length >> 24) & 0xFF);
            header[1] = (byte)((registrationMessage.Length >> 16) & 0xFF);
            header[2] = (byte)((registrationMessage.Length >> 8) & 0xFF);
            header[3] = (byte)((registrationMessage.Length) & 0xFF);
            stream.Write(header, 0, header.Length);
            stream.Write(registrationMessage, 0, registrationMessage.Length);
            ReadRegisterReply();
            Debug.WriteLine("Sent Registration");
        }

        public static void SendImage(byte[] yData, byte[] uvData, int width, int height)
        {
            if (!client.Connected)
                return;
            byte[] message = CreateFlatBufferMessage(yData, uvData, width, height);
            SendMessageAndReceiveReply(message);
        }

        static byte[] CreateFlatBufferMessage(byte[] yData, byte[] uvData, int width, int height)
        {
            if (!client.Connected)
                return null;
            var builder = new FlatBufferBuilder(yData.Length + uvData.Length + 100);

            var yVector = NV12Image.CreateDataYVector(builder, yData);
            var uvVector = NV12Image.CreateDataUvVector(builder, uvData);

            NV12Image.StartNV12Image(builder);
            NV12Image.AddDataY(builder, yVector);
            NV12Image.AddDataUv(builder, uvVector);
            NV12Image.AddWidth(builder, width);
            NV12Image.AddHeight(builder, height);
            NV12Image.AddStrideY(builder, width);  //TODO: Check if this is correct
            NV12Image.AddStrideUv(builder, width);
            var nv12Image = NV12Image.EndNV12Image(builder);

            Image.StartImage(builder);
            Image.AddDataType(builder, ImageType.NV12Image);
            Image.AddData(builder, nv12Image.Value);
            Image.AddDuration(builder, -1);
            var imageOffset = Image.EndImage(builder);

            Request.StartRequest(builder);
            Request.AddCommandType(builder, Command.Image);
            Request.AddCommand(builder, imageOffset.Value);
            var requestOffset = Request.EndRequest(builder);

            builder.Finish(requestOffset.Value);
            return builder.SizedByteArray();
        }

        static Reply ParseReply(byte[] receivedData)
        {
            var byteBuffer = new ByteBuffer(receivedData,4); //shift for header
            return Reply.GetRootAsReply(byteBuffer);
        }

        public static byte[] CreateRegistrationMessage(bool subscribe)
        {
            if (!client.Connected)
                return null;

            var builder = new FlatBufferBuilder(256);

            var originOffset = builder.CreateString("HyperTizen");

            Register.StartRegister(builder);
            Register.AddPriority(builder, 123);
            Register.AddOrigin(builder, originOffset);
            var registerOffset = Register.EndRegister(builder);

            Request.StartRequest(builder);
            Request.AddCommandType(builder, Command.Register);
            Request.AddCommand(builder, registerOffset.Value);
            var requestOffset = Request.EndRequest(builder);

            builder.Finish(requestOffset.Value);
            return builder.SizedByteArray();
        }

        public static void ReadRegisterReply()
        {
            if (!client.Connected)
                return;

            byte[] buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead > 0)
            {
                byte[] replyData = new byte[bytesRead];
                Array.Copy(buffer, replyData, bytesRead);

                Reply reply = ParseReply(replyData);
                Debug.WriteLine($"Registered: {reply.Registered}");
            }
        }

        static void SendMessageAndReceiveReply(byte[] message)
        {
            try
            {
                if (!client.Connected)
                    return;   
                {
                    
                    var header = new byte[4];
                    header[0] = (byte)((message.Length >> 24) & 0xFF);
                    header[1] = (byte)((message.Length >> 16) & 0xFF);
                    header[2] = (byte)((message.Length >> 8) & 0xFF);
                    header[3] = (byte)((message.Length) & 0xFF);
                    stream.Write(header, 0, header.Length);
                    
                    Debug.WriteLine(message.Length);
                    stream.Write(message, 0, message.Length);
                    Debug.WriteLine("Data sent. Waiting for Answer");

                    byte[] buffer = new byte[1024];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {

                        byte[] replyData = new byte[bytesRead];
                        Array.Copy(buffer, replyData, bytesRead);
                        string test = BitConverter.ToString(replyData);


                        Reply reply = ParseReply(replyData);

                        Debug.WriteLine($"Error: {reply.Error}");
                        Debug.WriteLine($"Video: {reply.Video}");
                        Debug.WriteLine($"Registered: {reply.Registered}");
                    }
                    else
                    {
                        Debug.WriteLine("No Answer from Server.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error Sending/Receiving: " + ex.Message);
            }
        }

    }
}
