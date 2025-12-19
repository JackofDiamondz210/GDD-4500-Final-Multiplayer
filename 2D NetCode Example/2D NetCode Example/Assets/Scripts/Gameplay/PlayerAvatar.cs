using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerAvatar : NetworkBehaviour
{
    [SerializeField] private float _Speed = 25f;
    [SerializeField] private NetworkObject _BulletPrefab;
    [Space]
    [SerializeField] private SpriteRenderer _SpriteRenderer;

    private PlayerController _playerController;
    public PlayerController PlayerController => _playerController;

    private Camera _camera;
    private Rigidbody2D _rigidbody;

    private InputAction _moveAction;
    private InputAction _shootAction;

    private Vector3 _spawnPoint;

    private Vector2 _moveDirection;

    #region Initialization Events

    private void Awake()
    {
        _camera = Camera.main;

        _moveAction = InputSystem.actions.FindAction("Player/Move");
        _shootAction = InputSystem.actions.FindAction("Player/Shoot");

        _rigidbody = GetComponent<Rigidbody2D>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Get the player controller associated with this client ID
        _playerController = NetworkServer.Instance.GetPlayerControllerByClientId(OwnerClientId);

        // Initialize the player avatar with the player controller
        if (_playerController != null) Initialize(_playerController);

        // If the player controller is not found, something has gone horribly wrong, disable the player avatar
        else 
        {
            Debug.LogError($"PlayerAvatar {OwnerClientId} could not find player controller");
            gameObject.SetActive(false);
            return;
        }
    }

    public void Initialize(PlayerController playerController)
    {
        _playerController.RegisterPlayerAvatar(this);

        gameObject.SetActive(true);

        _spawnPoint = transform.position;
        _SpriteRenderer.color = _playerController.PlayerColor.Value;

        this.name = $"Player Avatar {OwnerClientId}";
    }

    #endregion

    #region Player Input Events

    void Update()
    {
        // Only read the input if the player is the owner
        if (IsOwner) ReadLocalInput();
    }

    private void ReadLocalInput()
    {
        _moveDirection = _moveAction.ReadValue<Vector2>();

        // If the shoot action is triggered, request to shoot on the server
        if (_shootAction.triggered) 
        {
            Vector2 targetPosition = _camera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2 direction = (targetPosition - (Vector2)transform.position).normalized;
            Vector3 startPosition = this.transform.position + (Vector3)direction * 1f;

            RequestShootServerRpc(startPosition, direction);
        }
    }

    private void FixedUpdate()
    {
        _rigidbody.AddForce(_moveDirection * _Speed * Time.fixedDeltaTime * 10f, ForceMode2D.Force);
    }

    /// <summary>
    /// SERVER ONLY: Request to shoot a bullet, this must be on the server since we want to make sure
    ///  the bullet is spawned on all the clients, not just the owner
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void RequestShootServerRpc(Vector3 startPosition, Vector3 direction)
    {
        if (!IsServer) return;

        // Spawn projectile on the SERVER
        NetworkObject projectileInstance = Instantiate(
            _BulletPrefab,
            startPosition,
            Quaternion.identity
        );

        // Get the bullet mover component and initialize it
        BulletMover bulletMover = projectileInstance.GetComponent<BulletMover>();
        bulletMover.Initialize(direction, OwnerClientId);

        // Server-owned projectile; everyone will see it
        projectileInstance.Spawn();
    }

    #endregion
}
