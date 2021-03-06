using UnityEngine;
using Users;


namespace Network.Server
{
    public class ServerSend
    {
        private readonly Server server;
        public ServerSend(Server server)
        {
            this.server = server;
        }
        
        public void DebugMessage(string message)
        {
            using Packet packet = new Packet((byte) Packets.debugMessage, 0);
            packet.Write(message);
            SendToAll(packet, false, false);
        }
        
        public void ServerInit(byte userId)
        {
            using Packet packet = new Packet((byte) Packets.serverInit, 0);
            packet.Write(userId);

            packet.Write(UserController.instance.users.Count);
            packet.Write((byte)0);

            foreach (var pair in UserController.instance.users)
            {
                if (pair.Key == userId) {continue;}
                packet.Write(pair.Key);
            }
            
            packet.PrepareForSend(false);
            server.tcpServer.SendData(userId, packet.ToArray(), packet.Length());
        }
            
        public void ServerUserJoined(byte userId, byte joinedUser)
        {
            using Packet packet = new Packet((byte) Packets.serverUserJoined, 0);
            packet.Write(joinedUser);

            server.Send(userId, packet, false, false);
        }
        
        public void ServerUserLeft(byte userId, byte leftUser)
        {
            using Packet packet = new Packet((byte) Packets.serverUserJoined, 0);
            packet.Write(leftUser);

            server.Send(userId, packet, false, false);
        }
        
        public void ServerUDPConnection(byte userId)
        {
            Debug.LogFormat("SERVER: [" +userId+ "] udp test message");
            using Packet packet = new Packet((byte) Packets.serverUDPConnection, 0);
            packet.PrepareForSend(false);
            server.udpServer.SendData(userId, packet.ToArray(), packet.Length());
        }

        public void SendToAll(Packet packet, bool useUDP, bool async)
        {
            server.HandelData(packet.ToArray());
            SendToAllExceptOrigen(packet, useUDP, async);
        }
        
        public void SendToAllExceptOrigen(Packet packet, bool useUDP, bool async)
        {
            foreach (byte id in UserController.instance.users.Keys)
            {
                server.Send(id, packet, useUDP, async);
            }
        }
        
        public void SendToList(Packet packet, byte[] userIDs, bool useUDP, bool async)
        {
            for (int i = 0; i < userIDs.Length; i++)
            {
                server.Send(userIDs[i], packet, useUDP, async);
            }
        }

        public void SendToAllExceptList(Packet packet, byte[] userIDs, bool useUDP, bool async)
        {
            foreach (byte id in UserController.instance.users.Keys)
            {
                bool send = true;
                for (int i = 0; i < userIDs.Length; i++)
                {
                    if (id == userIDs[i])
                    {
                        send = false;
                        break;
                    }
                }

                if (send)
                {
                    server.Send(id, packet, useUDP, async);
                }
            }
        }
    }
}