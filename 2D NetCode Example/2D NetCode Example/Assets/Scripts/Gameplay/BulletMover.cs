using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class BulletMover : NetworkBehaviour
{
    [SerializeField] private int _Damage = 10;
    [SerializeField] private float _Speed = 10f;
    [SerializeField] private float _LifeTime = 5f;
    [Space]
    [SerializeField] private ParticleSystem _ImpactParticles;
    private Rigidbody2D _rigidbody;
    private Collider2D _collider;
    private Vector2 _targetDirection;
    private NetworkObject _ownerPlayerAvatar;

    #region Initialization Events

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();

        _collider.enabled = false;
    }

    /// <summary>
    /// Initializes the bullet mover, sets the target direction and the owner player avatar
    /// </summary>
    /// <param name="targetDirection">The direction in which the bullet will move</param>
    /// <param name="ownerID">The client ID of the owner player avatar</param>
    public void Initialize(Vector2 targetDirection, ulong ownerID)
    {
        _targetDirection = targetDirection;
        _ownerPlayerAvatar = NetworkServer.Instance.GetPlayerControllerByClientId(ownerID).PlayerAvatar;

        _collider.enabled = true;

        Invoke(nameof(RequestDespawnServerRpc), _LifeTime);
    }

    #endregion

    #region Update

    /// <summary>
    /// Called every fixed update, moves the bullet in the direction of the target direction
    /// </summary>
    private void FixedUpdate()
    {
        _rigidbody.linearVelocity = _targetDirection * _Speed;
    }

    #endregion

    #region Collision Events

    /// <summary>
    /// Called when the bullet collides with another object (other than the owner)
    /// </summary>
    /// <param name="collision">The collider that the bullet collided with</param>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_ownerPlayerAvatar == null) return;
        
        if (collision.gameObject == _ownerPlayerAvatar.gameObject) return; // don't damage the owner

        PlayerAvatar playerAvatar = collision.gameObject.GetComponent<PlayerAvatar>();
        if (playerAvatar != null)
        {
            playerAvatar.PlayerController.Health.Value -= _Damage;
        }

        // Despawn the bullet
        RequestDespawnServerRpc();

        // Disable the collider to prevent the bullet from causing damage again before it is despawned
        _collider.enabled = false;
    }

    #endregion

    #region Despawn & Particles

    /// <summary>
    /// SERVER ONLY: Despawn the bullet, this must be on the server since it needs
    /// to manage the despawn of the bullet on all the clients
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestDespawnServerRpc()
    {
        if (!IsServer) return;

        SpawnImpactParticlesClientRpc(this.transform.position);

        NetworkObject.Despawn();
    }

    /// <summary>
    /// CLIENT ONLY: Spawn the impact particles at the given position
    /// This must be on the everyone since we want to see the impact particles on all the clients
    /// </summary>
    /// <param name="impactPosition">The position at which the impact particles will be spawned</param>
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
    private void SpawnImpactParticlesClientRpc(Vector3 impactPosition)
    {
        ParticleSystem impactParticles = Instantiate(_ImpactParticles, impactPosition, Quaternion.identity);
        impactParticles.Play();
        Destroy(impactParticles.gameObject, impactParticles.main.duration);
    }

    #endregion
}
