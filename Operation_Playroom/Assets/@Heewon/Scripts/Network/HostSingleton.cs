using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using WebSocketSharp;

public class HostSingleton : MonoBehaviour
{
    static HostSingleton instance;

    public static HostSingleton Instance
    {
        get 
        {
            if (instance == null)
            {
                GameObject singletonObject = new GameObject("HostSingleton");
                instance = singletonObject.AddComponent<HostSingleton>();

                DontDestroyOnLoad(singletonObject);
            }
            return instance; 
        }
    }

    const int MaxConnections = 10;
    Allocation allocation;
    public string joinCode;
    public string lobbyId;

    public string LobbyId
    {
        get { return lobbyId; }
    }

    public async Task UpdateLobbyHost(string newHostAuthId)
    {
        try
        {
            UpdateLobbyOptions op = new UpdateLobbyOptions
            {
                HostId = newHostAuthId
            };

            await LobbyService.Instance.UpdateLobbyAsync(lobbyId, op);
        }
        catch (LobbyServiceException ex)
        {
            Debug.LogException(ex);
        }
    }

    public async Task StartHostAsync(bool createNewLobby = true, string joinCode = "", string lobbyId = "")
    {
        if (createNewLobby)
        {
            // 릴레이 접속

            try
            {
                allocation = await RelayService.Instance.CreateAllocationAsync(MaxConnections);
                this.joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                Debug.Log(this.joinCode);
            }
            catch (RelayServiceException e)
            {
                Debug.LogException(e);
                return;
            }

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            RelayServerData relayServerData = allocation.ToRelayServerData("dtls");
            transport.SetRelayServerData(relayServerData);

            // 로비 만들기

            try
            {
                CreateLobbyOptions options = new CreateLobbyOptions();
                options.IsPrivate = false;
                options.Data = new Dictionary<string, DataObject>
            {
                {
                    "JoinCode",
                    new DataObject(DataObject.VisibilityOptions.Member,this.joinCode)
                }
            };

                Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(this.joinCode, MaxConnections, options);
                this.lobbyId = lobby.Id;
            }
            catch (LobbyServiceException e)
            {
                Debug.LogException(e);
                return;
            }
        }
        else
        {
            this.joinCode = joinCode;
            this.lobbyId = lobbyId;
        }
        
        StartCoroutine(HeartBeatLobby(15));

        // 여기까지 로비

        ServerSingleton.Instance.Init();

        UserData userData = new UserData()
        {
            userName = AuthenticationService.Instance.PlayerName ?? "Unknown",
            userAuthId = AuthenticationService.Instance.PlayerId
        };
        string payload = JsonConvert.SerializeObject(userData);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;
        ServerSingleton.Instance.OnClientLeft += HandleClientLeft;

        NetworkManager.Singleton.StartHost();
        if (createNewLobby)
            NetworkManager.Singleton.SceneManager.LoadScene("LobbyScene", 
                UnityEngine.SceneManagement.LoadSceneMode.Single);

    }

    private async void HandleClientLeft(string authId)
    {
        try
        {
            Lobby lobby = await LobbyService.Instance.GetLobbyAsync(lobbyId);
            if (lobby.HostId != AuthenticationService.Instance.PlayerId) 
            {
                Debug.Log("호스트 불일치");
                ShutDown();
                return;
            }

            await LobbyService.Instance.RemovePlayerAsync(lobbyId,authId);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
        }
    }

    IEnumerator HeartBeatLobby(float waitTimeSeconds)
    {
        var delay = new WaitForSecondsRealtime(waitTimeSeconds);

        while (true)
        {
            LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return delay;
        }
    }

    //방폭버튼
    public async void ShutDown(bool deleteLobby = false)    
    {
        StopAllCoroutines();

        if (deleteLobby && !lobbyId.IsNullOrEmpty())
        {
            try
            {
                await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
            }
            catch (LobbyServiceException e)
            {
                Debug.LogException(e);
            }
            lobbyId = null;
        }

        ServerSingleton.Instance.OnClientLeft -= HandleClientLeft;
    }
}
