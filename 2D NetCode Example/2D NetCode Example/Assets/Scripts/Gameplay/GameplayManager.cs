using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class GameplayManager : MonoBehaviour
{
    [SerializeField] private List<PlayerController> _players = new List<PlayerController>();

    [SerializeField] List<Transform> _spawnPoints = new List<Transform>();

    [SerializeField] private NetworkObject _player1GoalPrefab;
    [SerializeField] private NetworkObject _player2GoalPrefab;

    private NetworkObject _player1GoalInstance;
    private NetworkObject _player2GoalInstance;

    /// <summary>
    /// Initialize the gameplay manager, cache the instance of the gameplay manager in the network controller
    /// and flag that this client has loaded the gameplay scene
    /// </summary>
    void Start()
    {
        // Cache this instance of the gameplay manager in the network controller
        NetworkServer.Instance.gameplayManager = this;

        // Flag that this client has loaded the gameplay scene
        NetworkServer.Instance.GameplaySceneHasLoaded();

        AssignSpawnPoints();
    }

    //tried to make it so that they respawn at their spawn point but decided to just make it 0,0
    public void AssignSpawnPoints()
    {
        if (!NetworkServer.Instance.IsServer) return;

        Vector3 commonSpawn = Vector3.zero; //getting both players to move to 0,0 if hit

        foreach (var player in NetworkServer.Instance.ConnectedPlayers)
        {
            player.SpawnPoint = commonSpawn;

            // Move avatar
            if (player.PlayerAvatar != null)
            {
                player.PlayerAvatar.transform.position = commonSpawn;

                Rigidbody2D rb = player.PlayerAvatar.GetComponent<Rigidbody2D>();
                if (rb != null)
                    rb.linearVelocity = Vector2.zero;
            }
        }
    }

    //spawning goal posts
    public void SpawnGoals()
    {
        if (!NetworkServer.Instance.IsServer) return;

        _player1GoalInstance = Instantiate(_player1GoalPrefab, new Vector3(-33.5f, 0, 0), Quaternion.identity);
        _player1GoalInstance.Spawn();

        _player2GoalInstance = Instantiate(_player2GoalPrefab, new Vector3(33.5f, 0, 0), Quaternion.identity);
        _player2GoalInstance.Spawn();
    }


    /// <summary>
    /// Get the spawn point for the given player id
    /// </summary>
    public Transform GetSpawnPointByPlayerId(int playerId) => _spawnPoints[playerId];

    //TODO: Implement score tracking and reset/return to lobby functionality
}
