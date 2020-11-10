using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEditor;
using System.IO;
using System.Security.Cryptography;

public class NetworkServer : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public ushort serverPort;
    private NativeList<NetworkConnection> m_Connections;
    string logPath = "Log.txt";

    List<PlayerSpawnMsg> AllSpawnMsg = new List<PlayerSpawnMsg>();

    void WriteToLog(string data)
    {
        if(!File.Exists(logPath))
        {
            File.WriteAllText(logPath, "=========================================\nServer Game Log:\n");
        }
        File.AppendAllText(logPath, "-- "+data+"\n");
    }

    void Start ()
    {
        Debug.Log("Initialized.");
        m_Driver = NetworkDriver.Create();
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = serverPort;
        if (m_Driver.Bind(endpoint) != 0)
            Debug.Log("Failed to bind to port " + serverPort);
        else
            m_Driver.Listen();

        m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
        InvokeRepeating("HandShake", 0.0f, 2.0f);
    }

    void SendToClient(string message, NetworkConnection c){
        var writer = m_Driver.BeginSend(NetworkPipeline.Null, c);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }
    public void OnDestroy()
    {
        m_Driver.Dispose();
        m_Connections.Dispose();
    }

    void OnConnect(NetworkConnection c){
        SendIDToClient(c);
        SendAllSpawnedPlayers(c);
        m_Connections.Add(c);
        Debug.Log("Accepted a connection.");
    }

    void SendIDToClient(NetworkConnection c)
    {
        RequestIDMsg m = new RequestIDMsg();
        m.ID = c.InternalId.ToString();
        SendToClient(JsonUtility.ToJson(m), c);
    }

    void SendAllSpawnedPlayers(NetworkConnection c)
    {
        foreach (PlayerSpawnMsg msg in AllSpawnMsg)
        {
            SendToClient(JsonUtility.ToJson(msg), c);
        }
    }

    void HandShake()
    {
        foreach (NetworkConnection c in m_Connections)
        {
            //// Example to send a handshake message:
            HandshakeMsg m = new HandshakeMsg();
            m.player.id = c.InternalId.ToString();
            SendToClient(JsonUtility.ToJson(m), c);
        }
    }

    void SpawnNewPlayer(PlayerSpawnMsg msg)
    {
        foreach (NetworkConnection c in m_Connections)
        {
            SendToClient(JsonUtility.ToJson(msg), c);
        }
    }

    void UpdatePlayerStats(UpdateStatsMsg msg)
    {
        foreach (NetworkConnection c in m_Connections)
        {
            SendToClient(JsonUtility.ToJson(msg), c);
        }
    }

    void DCPlayer(PlayerDCMsg msg)
    {
        foreach (NetworkConnection c in m_Connections)
        {
            SendToClient(JsonUtility.ToJson(msg), c);
        }
    }

    void OnData(DataStreamReader stream, int i){
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch(header.cmd){
            case Commands.HANDSHAKE:
                HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
                //Debug.Log("Handshake message received!");
            break;

            case Commands.PLAYER_UPDATE:
                PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
                Debug.Log("Player update message received!");
            break;

            case Commands.SERVER_UPDATE:
                ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
                Debug.Log("Server update message received!");
            break;

            case Commands.PLAYER_SPAWN:
                PlayerSpawnMsg psMsg = JsonUtility.FromJson<PlayerSpawnMsg>(recMsg);
                AllSpawnMsg.Add(psMsg);
                SpawnNewPlayer(psMsg);
                Debug.Log(psMsg.ID + " has joined the server!");
                break;

            case Commands.UPDATE_STATS:
                UpdateStatsMsg usMsg = JsonUtility.FromJson<UpdateStatsMsg>(recMsg);
                UpdatePlayerStats(usMsg);
            break;

            case Commands.GAME_START:
                // Log
                WriteToLog("\n\nGame ID: " + System.DateTime.Now.ToString() + UnityEngine.Random.value.ToString());

                GameStartMsg gsMsg = JsonUtility.FromJson<GameStartMsg>(recMsg);

                // Log
                WriteToLog(gsMsg.p1.user_id + " has joined the match. - " + gsMsg.p1.joinTime);
                WriteToLog(gsMsg.p2.user_id + " has joined the match. - " + gsMsg.p1.joinTime);
                WriteToLog(gsMsg.p3.user_id + " has joined the match. - " + gsMsg.p1.joinTime);
                int result = UnityEngine.Random.Range(1, 4);
                Player winner = new Player();

                // Log
                WriteToLog("Game started.");
                WriteToLog("RESULT: ");

                int p1Points = int.Parse(gsMsg.p1.points);
                int p2Points = int.Parse(gsMsg.p2.points);
                int p3Points = int.Parse(gsMsg.p3.points);
                int add;
                switch(result)
                {
                    case 1:
                        add = (int)((p2Points + p3Points) / p1Points) + 20;
                        WriteToLog(gsMsg.p1.user_id + " earned: " + add + " points." + " (" + p1Points + " -> " + (p1Points+add) + ")");
                        p1Points += add;
                        gsMsg.p1.points = p1Points.ToString();
                        winner = gsMsg.p1;

                        WriteToLog(gsMsg.p2.user_id + " lost: " + (int)((p2Points * 0.05) + 20) + " points." + " (" + p2Points + " -> " + (p2Points - (int)((p2Points * 0.05) + 20)) + ")");
                        p2Points -= (int)((p2Points * 0.05) + 20);
                        gsMsg.p2.points = p2Points.ToString();
                        WriteToLog(gsMsg.p3.user_id + " lost: " + (int)((p3Points * 0.05) + 20) + " points." + " (" + p3Points + " -> " + (p3Points - (int)((p3Points * 0.05) + 20)) + ")");
                        p3Points -= (int)((p3Points * 0.05) + 20);
                        gsMsg.p3.points = p3Points.ToString();
                        break;
                    case 2:
                        add = (int)((p1Points + p3Points) / p2Points) + 20;
                        WriteToLog(gsMsg.p2.user_id + " earned: " + add + " points." + " (" + p2Points + " -> " + (p2Points+add) + ")");
                        p2Points += add;
                        gsMsg.p2.points = p2Points.ToString();
                        winner = gsMsg.p2;

                        WriteToLog(gsMsg.p1.user_id + " lost: " + (int)((p1Points * 0.05) + 20) + " points." + " (" + p1Points + " -> " + (p1Points - (int)((p1Points * 0.05) + 20)) + ")");
                        p1Points -= (int)((p1Points * 0.05) + 20);
                        gsMsg.p1.points = p1Points.ToString();
                        WriteToLog(gsMsg.p3.user_id + " lost: " + (int)((p3Points * 0.05) + 20) + " points." + " (" + p3Points + " -> " + (p3Points - (int)((p3Points * 0.05) + 20)) + ")");
                        p3Points -= (int)((p3Points * 0.05) + 20);
                        gsMsg.p3.points = p3Points.ToString();
                        break;
                    case 3:
                        add = (int)((p2Points + p1Points) / p3Points) + 20;
                        WriteToLog(gsMsg.p3.user_id + " earned: " + add + " points." + " (" + p3Points + " -> " + (p3Points+add) + ")");
                        p3Points += add;
                        gsMsg.p3.points = p3Points.ToString();
                        winner = gsMsg.p3;

                        WriteToLog(gsMsg.p2.user_id + " lost: " + (int)((p2Points * 0.05) + 20) + " points." + " (" + p2Points + " -> " + (p2Points - (int)((p2Points * 0.05) + 20)) + ")");
                        p2Points -= (int)((p2Points * 0.05) + 20);
                        gsMsg.p2.points = p2Points.ToString();
                        WriteToLog(gsMsg.p1.user_id + " lost: " + (int)((p1Points * 0.05) + 20) + " points." + " (" + p1Points + " -> " + (p1Points - (int)((p1Points * 0.05) + 20)) + ")");
                        p1Points -= (int)((p1Points * 0.05) + 20);
                        gsMsg.p1.points = p1Points.ToString();
                        break;
                }
                WriteToLog(winner.user_id + " WON!");
                foreach (NetworkConnection c in m_Connections)
                {
                    //// Example to send a handshake message:
                    GameEndMsg m = new GameEndMsg();
                    m.p1 = gsMsg.p1;
                    m.p2 = gsMsg.p2;
                    m.p3 = gsMsg.p3;
                    m.winner = winner;
                    m.LogData = File.ReadAllText(logPath);
                    SendToClient(JsonUtility.ToJson(m), c);
                }

                // Do the match
                break;

            case Commands.PLAYER_DC:
                PlayerDCMsg pdMsg = JsonUtility.FromJson<PlayerDCMsg>(recMsg);
                Debug.Log("Removed Spawn data of: " + pdMsg.PlayerID);
                AllSpawnMsg.Remove(FindPlayerSpawnMsg(pdMsg.PlayerID));
                DCPlayer(pdMsg);
            break;

            default:
                Debug.Log("SERVER ERROR: Unrecognized message received!");
            break;
        }
    }

    PlayerSpawnMsg FindPlayerSpawnMsg(string ID)
    {
        foreach (PlayerSpawnMsg msg in AllSpawnMsg)
        {
            if(msg.ID == ID)
            {
                return msg;
            }
        }
        return null;
    }

    void OnDisconnect(int i){
        Debug.Log("Client disconnected from server");
        m_Connections[i] = default(NetworkConnection);
    }

    void Update ()
    {

        m_Driver.ScheduleUpdate().Complete();

        // CleanUpConnections
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {

                m_Connections.RemoveAtSwapBack(i);
                --i;
            }
        }

        // AcceptNewConnections
        NetworkConnection c = m_Driver.Accept();
        while (c  != default(NetworkConnection))
        {            
            OnConnect(c);

            // Check if there is another new connection
            c = m_Driver.Accept();
        }


        // Read Incoming Messages
        DataStreamReader stream;
        for (int i = 0; i < m_Connections.Length; i++)
        {
            Assert.IsTrue(m_Connections[i].IsCreated);
            
            NetworkEvent.Type cmd;
            cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            while (cmd != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    OnData(stream, i);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    OnDisconnect(i);
                }

                cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            }
        }
    }
}