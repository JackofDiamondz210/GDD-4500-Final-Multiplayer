using System;
using System.Collections;
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

    [SerializeField] private Sprite[] _characterSprites;

    private PlayerController _playerController;
    public PlayerController PlayerController => _playerController;

    private Camera _camera;
    private Rigidbody2D _rigidbody;

    private InputAction _moveAction;
    private InputAction _shootAction;

    private Vector3 _spawnPoint;
    private bool _canMove = true;

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
        UpdateSprite();

        _playerController.CharacterIndex.OnValueChanged += OnCharacterIndexChanged;

        this.name = $"Player Avatar {OwnerClientId}";
    }

    private void OnCharacterIndexChanged(int oldIndex, int newIndex)
    {
        UpdateSprite();
    }

    private void UpdateSprite()
    {
        int index = Mathf.Clamp(_playerController.CharacterIndex.Value, 0, _characterSprites.Length - 1);
        _SpriteRenderer.sprite = _characterSprites[index];

    }

    #endregion

    #region Player Input Events

    void Update()
    {
        // Only read the input if the player is the owner
        if (IsOwner) ReadLocalInput();
    }

    //reading inputs for player actions
    private void ReadLocalInput()
    {
        if (!_canMove) return;

        _moveDirection = _moveAction.ReadValue<Vector2>();

        // If the shoot action is triggered, request to shoot on the server
        if (_shootAction.triggered) 
        {
            Vector2 targetPosition = _camera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2 direction = (targetPosition - (Vector2)transform.position).normalized;
            Vector3 startPosition = this.transform.position + (Vector3)direction * 2f;

            RequestShootServerRpc(startPosition, direction);
        }
    }

    private void FixedUpdate()
    {
        _rigidbody.AddForce(_moveDirection * _Speed * Time.fixedDeltaTime * 10f, ForceMode2D.Force);
    }

    //resetting player when he gets hit by bullet and respawns
    public void ResetMovementState()
    {
        _moveDirection = Vector2.zero; // Stop input
        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector2.zero; //stop all motion
        }
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

    [ClientRpc]
    public void RespawnClientRpc(Vector3 spawnPosition)
    {
        StartCoroutine(RespawnCoroutine(spawnPosition));
    }


    //making it so you can't move for a little bit when returning to spawn
    public IEnumerator RespawnCoroutine(Vector3 spawnPosition)
    {
        transform.position = spawnPosition;
        ResetMovementState();

        _canMove = false; // disable input temporarily
        yield return null; // wait one frame
        _canMove = true; // re-enable input
    }

    #endregion
}
