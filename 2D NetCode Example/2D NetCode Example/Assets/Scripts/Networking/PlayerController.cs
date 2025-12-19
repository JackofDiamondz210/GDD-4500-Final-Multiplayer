using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{

    public Vector3 SpawnPoint;

    // General purpose network variable to track if the player is ready, used in both the lobby and gameplay
    // This is managed by the server to make sure everything stays in sync when checking
    public NetworkVariable<bool> IsReady = new NetworkVariable<bool>
        (
            false, 
            NetworkVariableReadPermission.Everyone, 
            NetworkVariableWritePermission.Server
        );

    // Network variable to track the player's choice
    public NetworkVariable<int> CharacterIndex = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );


    [SerializeField] private NetworkObject _PlayerAvatarPrefab;
    public NetworkObject PlayerAvatar => _PlayerAvatarPrefab;

    private PlayerAvatar _playerAvatar;


    #region Network Events

    /// <summary>
    /// Called when the player network object is spawned, add the player to the list of connected players
    /// </summary>
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Add the player to the list of connected players
        NetworkServer.Instance.AddPlayer(this);

        this.name = $"Player {OwnerClientId}";

        DontDestroyOnLoad(this);
    }

    /// <summary>
    /// Called when the player network object is despawned, remove the player from the list of connected players
    /// </summary>
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        // Remove the player from the list of connected players
        NetworkServer.Instance.RemovePlayer(this);
    }
    
    /// <summary>
    /// Registers the player avatar with the player controller
    /// </summary>
    /// <param name="playerAvatar">The player avatar to register</param>
    public void RegisterPlayerAvatar(PlayerAvatar playerAvatar)
    {
        _playerAvatar = playerAvatar;
    }

    public void Respawn()
    {
        if (_playerAvatar != null)
        {
            RespawnServerRpc();
        }
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void RespawnServerRpc()
    {
        if (_playerAvatar == null) return;

        //starting the respawn coroutine on the avatar after getting hit
        _playerAvatar.StartCoroutine(_playerAvatar.RespawnCoroutine(SpawnPoint));
    }

    #endregion


}
