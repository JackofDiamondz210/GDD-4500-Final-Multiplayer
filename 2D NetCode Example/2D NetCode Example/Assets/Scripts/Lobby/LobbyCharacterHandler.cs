using UnityEngine;
using UnityEngine.UI;

public class LobbyCharacterHandler : MonoBehaviour
{
    [SerializeField] GameObject _SelectColorText;
    [SerializeField] Image _PlayerColorImage;
    [SerializeField] CanvasGroup _ColorSelectionRoot;
    [SerializeField] Image _ReadyCheckmarkImage;

    private LobbyManager _lobbyManager;
    private PlayerController _player;

    public void Initialize(LobbyManager lobbyManager, PlayerController player, bool isOwner)
    {
        _lobbyManager = lobbyManager;
        _player = player;

        _PlayerColorImage.color = player.PlayerColor.Value;

        _player.PlayerColor.OnValueChanged += OnPlayerColorChanged;
        _player.IsReady.OnValueChanged += OnPlayerReadyChanged;

        // If the player is the owner, make the color selection visible
        _ColorSelectionRoot.gameObject.SetActive(isOwner);
        _SelectColorText.SetActive(isOwner);

        _ReadyCheckmarkImage.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        _player.PlayerColor.OnValueChanged -= OnPlayerColorChanged;
        _player.IsReady.OnValueChanged -= OnPlayerReadyChanged;
    }

    private void OnPlayerColorChanged(Color32 oldColor, Color32 newColor)
    {
        _PlayerColorImage.color = newColor;
    }

    public void ChangeColor(Image image)
    {
        // Hide the select color text and show the player color image
        _SelectColorText.SetActive(false);
        _PlayerColorImage.gameObject.SetActive(true);

        // Set the player's color to the selected color, this is the network variable that will be synced to the server
        _player.PlayerColor.Value = image.color;

        // Once we've selected a color, allow the player to ready up
        _lobbyManager.AllowReadyUp();
    }

    private void OnPlayerReadyChanged(bool oldValue, bool newValue)
    {
        _ReadyCheckmarkImage.gameObject.SetActive(newValue);
    }
}
