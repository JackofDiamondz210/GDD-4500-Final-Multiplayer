using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
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

    public NetworkVariable<int> Health = new NetworkVariable<int>
        (
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
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

        Health.OnValueChanged += OnHealthChanged;

        DontDestroyOnLoad(this);
    }

    /// <summary>
    /// Called when the player network object is despawned, remove the player from the list of connected players
    /// </summary>
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        Health.OnValueChanged -= OnHealthChanged;

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

    /// <summary>
    /// Called when the health value changes
    /// </summary>
    /// <param name="previousValue">The previous value of the health</param>
    /// <param name="newValue">The new value of the health</param>
    private void OnHealthChanged(int previousValue, int newValue)
    {
        if (newValue <= 0)
        {
            // TODO: Implement respawn logic
            Health.Value = 100;
        }
    }

    #endregion


}
