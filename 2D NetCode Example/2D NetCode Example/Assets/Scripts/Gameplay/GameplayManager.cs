using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class GameplayManager : MonoBehaviour
{
    [SerializeField] private List<PlayerController> _players = new List<PlayerController>();

    [SerializeField] List<Transform> _spawnPoints = new List<Transform>();
    
    //points to win
    [SerializeField] private int _maxScore = 5;

    //setting up points
    private int _player1Score = 0;
    private int _player2Score = 0;

    //return to center after scoring position
    [SerializeField] private Vector3 _centerSpawn = Vector3.zero;

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

        _players = NetworkServer.Instance.ConnectedPlayers.ToList();
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

    /// <summary>
    /// Get the spawn point for the given player id
    /// </summary>
    public Transform GetSpawnPointByPlayerId(int playerId) => _spawnPoints[playerId];

    //TODO: Implement score tracking and reset/return to lobby functionality

    //scoring without return to center
    public void PlayerScored(PlayerController player)
    {
        if (!NetworkServer.Instance.IsServer) return;

        if (player == _players[0]) _player1Score++;
        else if (player == _players[1]) _player2Score++;

        Debug.Log($"Score: P1={_player1Score} P2={_player2Score}");

        // Respawn both players in the center: DID NOT GET TO
        foreach (var p in _players)
        {
            if (p.PlayerAvatar != null)
            {
                p.PlayerAvatar.transform.position = _centerSpawn;
                Rigidbody2D rb = p.PlayerAvatar.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }
        }

        // Check for win
        if (_player1Score >= _maxScore)
            EndGame(1);
        else if (_player2Score >= _maxScore)
            EndGame(2);
    }

    //getting which player won and asking to satrt a new game
    private void EndGame(int winner)
    {
        Debug.Log($"Player {winner} wins!");

        // Reset scores
        _player1Score = 0;
        _player2Score = 0;

        // Reset players to center
        foreach (var p in _players)
        {
            if (p.PlayerAvatar != null)
            {
                p.PlayerAvatar.transform.position = _centerSpawn;
                Rigidbody2D rb = p.PlayerAvatar.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }
        }

        // TODO: Show UI asking "Play Again?" or "Quit"
    }
}
