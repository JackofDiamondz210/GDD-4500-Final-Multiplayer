using UnityEngine;
using UnityEngine.UI;

public class LobbyCharacterHandler : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image _PlayerSpriteImage;
    [SerializeField] private Image _ReadyCheckmarkImage;
    [SerializeField] private GameObject _SelectCharacterText;

    [Header("Character Options")]
    [SerializeField] private Sprite[] _CharacterOptions;

    private LobbyManager _lobbyManager;
    private PlayerController _player;
    private bool _isOwner;

    public void Initialize(LobbyManager lobbyManager, PlayerController player, bool isOwner)
    {
        _lobbyManager = lobbyManager;
        _player = player;
        _isOwner = isOwner;

        int index = Mathf.Clamp(_player.CharacterIndex.Value, 0, _CharacterOptions.Length - 1);
        _PlayerSpriteImage.sprite = _CharacterOptions[index];

        _player.CharacterIndex.OnValueChanged += OnPlayerCharacterChanged;
        _player.IsReady.OnValueChanged += OnPlayerReadyChanged;

        _ReadyCheckmarkImage.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        _player.CharacterIndex.OnValueChanged -= OnPlayerCharacterChanged;
        _player.IsReady.OnValueChanged -= OnPlayerReadyChanged;
    }

    #region Character Handling
    private void OnPlayerCharacterChanged(int oldIndex, int newIndex)
    {
        if (newIndex >= 0 && newIndex < _CharacterOptions.Length)
        {
            _PlayerSpriteImage.sprite = _CharacterOptions[newIndex];
        }
    }

    //changing character based on the 2 options using the character index
    public void ChangeCharacter(int characterIndex)
    {
        if (!_isOwner) return;// only change owners sprite

        if (_player == null) return;
        if (characterIndex < 0 || characterIndex >= _CharacterOptions.Length) return;

        // Update networked character index
        _player.CharacterIndex.Value = characterIndex;

        // Update UI immediately
        _PlayerSpriteImage.sprite = _CharacterOptions[characterIndex];

        //Hiding the "Select Character" text
        if (_SelectCharacterText != null)
        {
            _SelectCharacterText.SetActive(false);
        }

        // Optionally, allow the player to ready up once they've picked
        _lobbyManager.AllowReadyUp();
    }
    #endregion

    #region Ready Handling
    private void OnPlayerReadyChanged(bool oldValue, bool newValue)
    {
        _ReadyCheckmarkImage.gameObject.SetActive(newValue);
    }
    #endregion
}
