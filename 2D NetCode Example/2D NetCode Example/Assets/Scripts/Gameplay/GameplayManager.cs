using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameplayManager : MonoBehaviour
{
    [SerializeField] private List<PlayerController> _players = new List<PlayerController>();

    [SerializeField] List<Transform> _spawnPoints = new List<Transform>();

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
    }

    /// <summary>
    /// Get the spawn point for the given player id
    /// </summary>
    public Transform GetSpawnPointByPlayerId(int playerId) => _spawnPoints[playerId];

    //TODO: Implement score tracking and reset/return to lobby functionality
}
