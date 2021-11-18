﻿using System;
using System.Collections.Generic;
using System.IO;
using Engine.User;
using Network.Both;
using Unity.Mathematics;
using UnityEngine;
using Utility;

namespace Network
{
    public class NetworkHandle
    {
        private readonly NetworkController network;

        public NetworkHandle(NetworkController network)
        {
            this.network = network;
            
            packetHandlers = new Dictionary<byte, PacketHandler>()
            {
                { (byte)Packets.featureSettings, FeatureSettings },
                { (byte)Packets.userVoiceId, UserVoiceID },
                { (byte)Packets.userPos, UserPos },
                
                { (byte)Packets.userGetListOfLocalFiles, GetListOfLocalFiles },
                { (byte)Packets.userListOfLocalFiles, ListOfLocalFiles },
                { (byte)Packets.userGetFile, GetFile },
                { (byte)Packets.userFile, FileRecived },
            };
        }
        
        public delegate void PacketHandler(byte userID, Packet packet);
        public Dictionary<byte, PacketHandler> packetHandlers;
        
        public void FeatureSettings(byte userID, Packet packet)
        {
            String log = "NETWORK: Received Feature Settings from "+ userID +":\n";
            
            User user = UserController.instance.users[userID];

            bool needToReply = packet.ReadBool();
            
            int lenght = packet.ReadInt32();
            for (int i = 0; i < lenght; i++)
            {
                String name = packet.ReadString();
                bool value = packet.ReadBool();
                
                log += name + " " + value + "\n";
                user.features[name] = value;
            }
            
            if (needToReply)
            {
                network.networkSend.FeatureSettings(userID, false);
            }
            
            if (network.networkFeatureState.value != (int) FeatureState.online)
            {
                bool allLoaded = true;
                foreach (User otherUser in UserController.instance.users.Values)
                {
                    if (!otherUser.features.ContainsKey("Network"))
                    {
                        allLoaded = false;
                    }
                }

                if (allLoaded)
                {
                    Debug.Log("Network: online");
                    network.networkFeatureState.value = (int) FeatureState.online;
                }
            }
        }
        
        public void UserVoiceID(byte userID, Packet packet)
        {
            byte vID = packet.ReadByte();
            bool reply = packet.ReadBool();

            User user = UserController.instance.users[userID];
            if (user == null)
            {
                Debug.Log("NETWORK: User not existing");
                return;
            }
            
            user.voiceId = vID;
            
            if (reply)
            {
                network.networkSend.UserVoiceID(false);
            }
            
            Debug.LogFormat("NETWORK: User: {0} VoiceID: {1}", userID, vID);
        }

        public void UserPos(byte userID, Packet packet)
        {
            float3 pos = packet.ReadFloat3();
            UserController.instance.users[userID].transform.position = pos;
        }

        public void GetListOfLocalFiles(byte userID, Packet packet)
        {
            List<string> fileNames = new List<string>();
            var fileEntries = FileShare.FileShare.instance.fileEntries;

            foreach (FileShare.FileShare.FileEntry fileEntry in fileEntries)
            {
                if (fileEntry.localPath != "")
                {
                    fileNames.Add(fileEntry.fileName);
                }
            }
            
            network.networkSend.ListOfLocalFiles(userID, fileNames.ToArray());
        }
        
        public void ListOfLocalFiles(byte userID, Packet packet)
        {
            int length = packet.ReadInt32();

            for (int i = 0; i < length; i++)
            {
                string name = packet.ReadString();
                FileShare.FileShare.instance.AddFileEntry(userID, name);
            }
        }
        
        public void GetFile(byte userID, Packet packet)
        {
            string filename = packet.ReadString();

            FileShare.FileShare.FileEntry fileEntry = FileShare.FileShare.instance.fileEntries.Find(
                file => file.fileName == filename);
            
            if (File.Exists(fileEntry.localPath))
            {
                byte[] data = File.ReadAllBytes(fileEntry.localPath);
                network.networkSend.File(filename, data, userID);
            }
            
            Threader.RunOnMainThread(() =>
            {
                Debug.LogFormat("File Not Found {filename}");
            });
        }
        
        public void FileRecived(byte userID, Packet packet)
        {
            string filename = packet.ReadString();
            int length = packet.ReadInt32();
            byte[] data = packet.ReadBytes(length);

            string path = "D:\\" + filename;
            FileStream oFileStream = null;
            oFileStream = new FileStream(path, FileMode.Create);
            oFileStream.Write(data, 0, data.Length);
            oFileStream.Close();
            
            FileShare.FileShare.instance.AddFileEntry(userID, filename, path);
        }
    }
}