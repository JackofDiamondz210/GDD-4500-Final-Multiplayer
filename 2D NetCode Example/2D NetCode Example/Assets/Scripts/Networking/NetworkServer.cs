using Unity.Netcode;
using UnityEngine;
using System;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay.Models;
using Unity.Services.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using System.Collections.Generic;
using System.Linq;

public class NetworkServer : SingletonNetworkPersistent<NetworkServer>
{
    public enum Mode
    {
        None,
        LocalHost,
        LocalClient,
        RelayHost,
        RelayClient,
    }

    private Mode _networkMode = Mode.None;
    public Mode NetworkMode => _networkMode;

    private string _joinCode = string.Empty;
    public string JoinCode
    {
        get => _joinCode;
        set => _joinCode = value;
    }

    public Action<Mode> OnNetworkStarted;
    public Action OnNetworkFailed;
    public Action OnNetworkStopped;
    public Action OnNetworkJoined;

    public Action<PlayerController> OnPlayerJoined;
    public Action<PlayerController> OnPlayerLeft;

    [SerializeField] bool DEBUG_MODE = false;
    [SerializeField] private NetworkManager _networkManager;

    // List to keep track of players, stored by client ID (eg. 0, 1, 2, etc.)
    private List<PlayerController> _connectedPlayers = new List<PlayerController>();
    public IReadOnlyList<PlayerController> ConnectedPlayers => _connectedPlayers.AsReadOnly();

    private PlayerController _ownerPlayer;
    public PlayerController OwnerPlayer => _ownerPlayer;

    // Getter and setter for the lobby manager & gameplay manager
    // These get set by the LobbyManager and GameplayManager scripts respectively when they are instantiated
    // These managers are scene scoped, so make sure to check nulls, and set them when the scripts load
    private LobbyManager _lobbyManager;
    public LobbyManager lobbyManager{
        get => _lobbyManager;   
        set => _lobbyManager = value;
    }

    private GameplayManager _gameplayManager;
    public GameplayManager gameplayManager{
        get => _gameplayManager;
        set => _gameplayManager = value;
    }

    #region Initialization Events

    public override void Awake()
    {
        base.Awake();

        _networkManager.OnServerStarted += OnServerStarted;
        _networkManager.OnClientStarted += OnClientStarted;

        _networkManager.OnTransportFailure += OnTransportFailure;

        _networkManager.OnServerStopped += OnServerStopped;
        _networkManager.OnClientStopped += OnClientStopped;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        _networkManager.OnServerStarted -= OnServerStarted;
        _networkManager.OnClientStarted -= OnClientStarted;

        _networkManager.OnTransportFailure -= OnTransportFailure;

        _networkManager.OnServerStopped -= OnServerStopped;
        _networkManager.OnClientStopped -= OnClientStopped;
    }

    #endregion

    #region Network Events

    private void OnServerStarted() => OnNetworkStarted?.Invoke(_networkMode);
    private void OnClientStarted() => OnNetworkStarted?.Invoke(_networkMode);

    private void OnTransportFailure()
    {
        Debug.Log("OnTransportFailure");
        OnNetworkFailed?.Invoke();
    }

    private void OnServerStopped(bool value) => OnNetworkStopped?.Invoke();
    private void OnClientStopped(bool value) => OnNetworkStopped?.Invoke();

    // Helper method to add player (can be called from OnConnectionEvent)
    public void AddPlayer(PlayerController player)
    {
        if (!_connectedPlayers.Contains(player))
        {
            _connectedPlayers.Add(player);
        }

        if (player.IsOwner) _ownerPlayer = player;

        OnPlayerJoined?.Invoke(player);
    }

    // Helper method to remove player
    public void RemovePlayer(PlayerController player)
    {
        if (_connectedPlayers.Contains(player))
        {
            _connectedPlayers.Remove(player);
        }

        OnPlayerLeft?.Invoke(player);
    }

    // Recieves request to start a specified network mode
    public void StartNetworkMode(Mode mode)
    {
        _networkMode = mode;
        switch (mode)
        {
            case Mode.LocalHost:
                _networkManager.StartHost();
                break;
            case Mode.LocalClient:
                _networkManager.StartClient();
                break;
        
            case Mode.RelayHost:
                StartRelayHost();
                break;
            case Mode.RelayClient:
                StartRelayClient();
                break;
        }
    }

    public async void StartRelayHost()
    {
        try
        {
            // 1. Initialize Unity Services (only need once per session, but safe to call here)
            if (UnityServices.State != ServicesInitializationState.Initialized &&
                UnityServices.State != ServicesInitializationState.Initializing)
            {
                await UnityServices.InitializeAsync();
            }

            // 2. Make sure we're signed in
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"Signed in as: {AuthenticationService.Instance.PlayerId}");
            }

            // 3. Create Relay allocation
            int maxConnections = 3; // number of CLIENTS (excluding host)
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

            // 4. (Optional) get join code to share
            _joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"Relay join code: {_joinCode}");

            // 5. Configure UnityTransport with Relay data
            var utp = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;

            utp.SetRelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            // 6. Now it's safe to start host
            NetworkManager.Singleton.StartHost();
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
    }

    public async void StartRelayClient()
    {

        if (string.IsNullOrEmpty(_joinCode))
        {
            Debug.LogError("Join code is empty");
            return;
        }

        // 1. Initialize services if needed
        if (UnityServices.State != ServicesInitializationState.Initialized &&
            UnityServices.State != ServicesInitializationState.Initializing)
        {
            await UnityServices.InitializeAsync();
        }

        // 2. Sign in if not already
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"Signed in as: {AuthenticationService.Instance.PlayerId}");
        }

        // 3. Join relay allocation
        JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(_joinCode);

        // 4. Configure transport
        var utp = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;

        utp.SetRelayServerData(
            joinAlloc.RelayServer.IpV4,
            (ushort)joinAlloc.RelayServer.Port,
            joinAlloc.AllocationIdBytes,
            joinAlloc.Key,
            joinAlloc.ConnectionData,
            joinAlloc.HostConnectionData
        );

        NetworkManager.Singleton.StartClient();
    }

    #endregion

    #region Lobby Events

    /// <summary>
    /// When each client is ready, notify the server to check if all players are ready
    /// </summary>
    public void PlayerIsReadyInLobby()
    {
        // Check if all players are ready
        CheckLobbyReadyUpServerRpc(_ownerPlayer.OwnerClientId);
    }

    /// <summary>
    /// SERVER ONLY: Check if all players are ready in the lobby
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void CheckLobbyReadyUpServerRpc(ulong playerId)
    {
        // Get the player associated with the given playerId, this is the player that is readying up
        // This happens on the server to make sure everything stays in sync
        var player = ConnectedPlayers.FirstOrDefault(p => p.OwnerClientId == playerId);
        
        if(player != null)
        {
            player.IsReady.Value = true;
        }

        if (DEBUG_MODE) Debug.Log("<color=orange>[SERVER] Checking lobby ready up on the server</color>");

        // Check if all players are ready, if there is a player that is not ready, return
        if (!AreAllPlayersReady()) return;

        // Reset the ready state of all players, this allows us to reuse the ready state for the next check
        ResetReadyState();

        // If all players are ready, start the game
        LoadGameplayClientRpc();
    }

    /// <summary>
    /// ALL CLIENTS: Transition to the gameplay scene
    /// </summary>
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    void LoadGameplayClientRpc()
    {
        if (DEBUG_MODE) Debug.Log("<color=cyan>[CLIENT] Transitioning to gameplay scene on the client</color>");

        _lobbyManager.StartGame();
    }

    #endregion

    #region Gameplay Events

    /// <summary>
    /// When the gameplay scene has loaded, notify the server that the scene has loaded
    /// This will be called by each client when the scene has loaded
    /// </summary>
    public void GameplaySceneHasLoaded()
    {
        // Check if all players are ready
        CheckGameplayReadyUpServerRpc(_ownerPlayer.OwnerClientId);
    }

    /// <summary>
    /// SERVER ONLY: Check if all players are ready in the lobby
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void CheckGameplayReadyUpServerRpc(ulong playerId)
    {
        // Get the player associated with the given playerId, this is the player that is readying up
        // This happens on the server to make sure everything stays in sync
        var player = ConnectedPlayers.FirstOrDefault(p => p.OwnerClientId == playerId);
        if(player != null)
        {
            player.IsReady.Value = true;
        }

        if (DEBUG_MODE) Debug.Log("<color=orange>[SERVER] Checking gameplay ready up on the server</color>");

        // Check if all players are ready, if there is a player that is not ready, return
        if (!AreAllPlayersReady()) return;

        // Reset the ready state of all players, this allows us to reuse the ready state for the next check
        ResetReadyState();

        // Once all players are ready, start the game
        // POTENTIAL BUG: This gets called by the last player to load the scene, 
        // so if they crash/disconnect before loading the scene, the game will not start
        StartGameplayClientRpc();
    }

    /// <summary>
    /// ALL CLIENTS: Transition to the gameplay scene
    /// </summary>
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
    void StartGameplayClientRpc()
    {
        if (DEBUG_MODE) Debug.Log("<color=cyan>[CLIENT] Starting gameplay on the client</color>");

        // Get the spawn point for the owner player and spawn the player avatar
        // If we wanted to randomize this, we would need to do that in the server RPC to make sure no duplicates are assigned
        Transform spawnPoint = _gameplayManager.GetSpawnPointByPlayerId((int)_ownerPlayer.OwnerClientId);

        // Spawn the player avatar at the assigned spawn point
        RequestSpawnAvatarRpc(_ownerPlayer.OwnerClientId, spawnPoint.position);
    }

    /// <summary>
    /// SERVER ONLY: Request to spawn the avatar for the given player
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSpawnAvatarRpc(ulong playerId, Vector3 spawnPoint)
    {
        if (DEBUG_MODE) Debug.Log($"<color=orange>[SERVER] Requesting to spawn avatar for player {playerId} at {spawnPoint}</color>");
        
        // Safety: this logic should only ever run on the server
        if (!IsServer)
            return;

        // Instantiate the avatar on the SERVER
        NetworkObject avatarInstance = Instantiate(_ownerPlayer.PlayerAvatar, spawnPoint, Quaternion.identity);

        // Spawn it as a networked object and give ownership to that client
        // This will replicate it to all the clients
        avatarInstance.SpawnWithOwnership(playerId);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Checks if all players are in a ready state
    /// </summary>
    /// <returns></returns>
    private bool AreAllPlayersReady()
    {
        foreach (var connectedPlayer in ConnectedPlayers)
        {
            if (!connectedPlayer.IsReady.Value) return false;
        }
        return true;
    }

    /// <summary>
    /// Resets the ready state of all players, this allows us to reuse the ready state for the next check
    /// </summary>
    private void ResetReadyState()
    {
        foreach (var connectedPlayer in ConnectedPlayers)
        {
            connectedPlayer.IsReady.Value = false;
        }
    }

    /// <summary>
    /// Gets the player controller by the given client id
    /// </summary>
    /// <param name="ownerClientId">The client id of the player to get the controller for</param>
    /// <returns></returns>
    public PlayerController GetPlayerControllerByClientId(ulong ownerClientId)
    {
        return ConnectedPlayers.FirstOrDefault(p => p.OwnerClientId == ownerClientId);
    }

    #endregion
}
