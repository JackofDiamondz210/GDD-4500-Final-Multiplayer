using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;
using Unity.Netcode;

public class TargetGroupCam : MonoBehaviour
{
    [SerializeField] private CinemachineTargetGroup targetGroup;

    void Start()
    {
        // Add existing players
        foreach (var player in NetworkServer.Instance.ConnectedPlayers)
        {
            if (player.PlayerAvatar != null)
                targetGroup.AddMember(player.PlayerAvatar.transform, 1f, 0.5f);
        }

        // Subscribe to future joins/leaves
        NetworkServer.Instance.OnPlayerJoined += OnPlayerJoined;
        NetworkServer.Instance.OnPlayerLeft += OnPlayerLeft;
    }

    private void OnPlayerJoined(PlayerController player)
    {
        if (player.PlayerAvatar != null)
        {
            targetGroup.AddMember(player.PlayerAvatar.transform, 1f, 0.5f);
        }
    }

    private void OnPlayerLeft(PlayerController player)
    {
        if (player.PlayerAvatar != null)
        {
            targetGroup.RemoveMember(player.PlayerAvatar.transform);
        }
    }
}
