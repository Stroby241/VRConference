using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using UnityEngine;
using Users;
using Utility;

namespace Network.Server
{
    public class Server : MonoBehaviour
    {
        public PublicInt serverTCPPort;
        public PublicInt serverUDPPort;

        private static Dictionary<byte, NetworkHandle.PacketHandler> packetHandlers;

        public TCPServer tcpServer;
        public UDPServer udpServer;
        public ServerHandle serverHandle;
        public ServerSend serverSend;

        public PublicInt networkFeatureState;
        public PublicInt udpFeatureState;
        
        public NetworkSend networkSend;
        public NetworkHandle networkHandle;

        public PublicString playerName;

        private void Awake()
        {
            tcpServer = new TCPServer(this);
            udpServer = new UDPServer(this);
            serverHandle = new ServerHandle(this);
            serverSend = new ServerSend(this);

            packetHandlers = new Dictionary<byte, NetworkHandle.PacketHandler>()
            {
                { (byte)Packets.debugMessage, serverHandle.DebugMessage },
                { (byte)Packets.clientContainerPacket, serverHandle.ClientContainerPacket },
                { (byte)Packets.clientStartUDP, serverHandle.ClientStartUDP },
                { (byte)Packets.clientUDPConnection, serverHandle.ClientUDPConnection },
            };
            
            networkFeatureState.value = (int) FeatureState.offline;
        }
        
        private void Start()
        {
            foreach (KeyValuePair<byte, NetworkHandle.PacketHandler> pair in networkHandle.packetHandlers)
            {
                packetHandlers.Add(pair.Key, pair.Value);
            }
        }

        private void OnApplicationQuit()
        {
            StopServer();
        }

        public void StartServer()
        {
            if (networkFeatureState.value != (int) FeatureState.offline) { return; }
            
            Debug.Log("SERVER: Starting...");
            networkFeatureState.value = (int) FeatureState.starting;
            
            tcpServer.Start();

            Debug.Log("SERVER: Started");
            networkFeatureState.value = (int) FeatureState.online;
        }

        // Main Thread
        public void HandelData(byte[] data)
        {
            Packet packet = new Packet(data);
            packet.PrepareForRead();

            bool async = packet.ReadBool();
            int length = packet.ReadInt32();
            if (length + 5 > data.Length)
            {
                Debug.Log("SERVER: Packet size not correct.");
                return;
            }
            
            byte packetId = packet.ReadByte();
            byte userID = packet.ReadByte();
            
            try
            {
                packetHandlers[packetId](userID, packet);
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
            
            if (length + 5 < data.Length)
            {
                var list = data.ToList().GetRange( length + 5, data.Length - (length + 5));
                HandelData(list.ToArray());
                return;
            }
        }

        public void Send(byte userId, Packet packet, bool useUDP, bool async)
        {
            var user = UserController.instance.users[userId];
            if (user == null || !user.HasFeature("Network"))
            {
                return;
            }
            
            packet.PrepareForSend(async);
            if (!useUDP || udpFeatureState.value != (int) FeatureState.online || !user.HasFeature("UDP"))
            {
                tcpServer.SendData(userId, packet.ToArray(), packet.Length());
            }
            else
            {
                udpServer.SendData(userId, packet.ToArray(), packet.Length());
            }
        }
        
        // MainThread
        public void DisconnectClient(byte userId)
        {
            if (!UserController.instance.users.ContainsKey(userId)) {return;}
            
            tcpServer.DisconnectClient(userId);
            udpServer.DisconnectClient(userId);
            
            Debug.Log("SERVER: Client " + userId + " disconnected.");

            foreach (byte id in UserController.instance.users.Keys)
            {
                if (id != userId)
                {
                    serverSend.ServerUserLeft(id, userId);
                }
            }
                
            UserController.instance.UserLeft(userId);
        }

        public void StopServer()
        {
            if (networkFeatureState.value != (int) FeatureState.online) { return; }
            
            Debug.Log("SERVER: Stopping...");
            networkFeatureState.value = (int) FeatureState.stopping;

            foreach (byte id in UserController.instance.users.Keys)
            {
                DisconnectClient(id);
            }
            
            tcpServer.Stop();

            if (udpFeatureState.value == (int) FeatureState.online)
            {
                udpServer.Stop();
            }
            
            Debug.Log("SERVER: Stopped");
            networkFeatureState.value = (int) FeatureState.offline;
        }
    }
}