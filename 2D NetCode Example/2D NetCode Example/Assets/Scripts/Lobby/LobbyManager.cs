using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour
{
    [Header("Network Mode Selection")]
    [SerializeField] string _GameplaySceneName = "Gameplay";
    [Space]
    [SerializeField] private TextMeshProUGUI _MessageText;

    [Header("Network Mode Selection")]
    [SerializeField] private CanvasGroup _NetworkSelectionRoot;
    [SerializeField] private Button _StartLocalHostButton;
    [SerializeField] private Button _StartLocalClientButton;
    [Space]
    [SerializeField] private Button _StartRelayHostButton;
    [SerializeField] private Button _StartRelayClientButton;
    [SerializeField] private TMP_InputField _RelayJoinCodeInputField;

    [Header("Character Selection")]
    [SerializeField] private CanvasGroup _CharacterSelectionRoot;
    [SerializeField] private LobbyCharacterHandler _LobbyCharacterHandlerPrefab;
    [SerializeField] private Transform _LobbyCharacterHandlerContainer;
    [SerializeField] private Button _ReadyUpButton;

    private Dictionary<PlayerController, LobbyCharacterHandler> _lobbyCharacterHandlers = new Dictionary<PlayerController, LobbyCharacterHandler>();

    #region Initialization Events

    /// <summary>
    /// Initialize the lobby manager, subscribe to network controller events, 
    /// and cache the instance of the lobby manager in the network controller
    /// Set the initial state of the UI to the network selection root
    /// </summary>
    private void Start()
    {
        // Bind buttons to network mode selection
        _StartLocalHostButton.onClick.AddListener(() => StartNetworkMode(NetworkServer.Mode.LocalHost));
        _StartLocalClientButton.onClick.AddListener(() => StartNetworkMode(NetworkServer.Mode.LocalClient));
        _StartRelayHostButton.onClick.AddListener(() => StartNetworkMode(NetworkServer.Mode.RelayHost));
        _StartRelayClientButton.onClick.AddListener(() => StartNetworkMode(NetworkServer.Mode.RelayClient));

        // Bind relay join code input field to join code
        _RelayJoinCodeInputField.onValueChanged.AddListener(OnRelayJoinCodeChanged);

        // Cache this instance of the lobby manager in the network controller
        NetworkServer.Instance.lobbyManager = this;

        // Bind network controller events
        NetworkServer.Instance.OnNetworkStarted += OnNetworkStarted;
        NetworkServer.Instance.OnNetworkFailed += OnNetworkFailed;

        NetworkServer.Instance.OnPlayerJoined += OnPlayerJoined;
        NetworkServer.Instance.OnPlayerLeft += OnPlayerLeft;

        // Disable start local client button until relay join code is entered
        _StartRelayClientButton.interactable = false;
        _ReadyUpButton.interactable = false;

        // Disable the Character Selection root until we succeed or fail to start the network
        _CharacterSelectionRoot.gameObject.SetActive(false);
        _NetworkSelectionRoot.gameObject.SetActive(true);
    }

    /// <summary>
    /// Unsubscribe from network controller events
    /// </summary>
    private void OnDestroy()
    {
        // Unbind network controller events
        NetworkServer.Instance.OnNetworkStarted -= OnNetworkStarted;
        NetworkServer.Instance.OnNetworkFailed -= OnNetworkFailed;

        NetworkServer.Instance.OnPlayerJoined -= OnPlayerJoined;
        NetworkServer.Instance.OnPlayerLeft -= OnPlayerLeft;

        // Clear the cached instance of the lobby manager in the network controller
        NetworkServer.Instance.lobbyManager = null;
    }

    #endregion

    #region Network Events

    /// <summary>
    /// Start the network mode, this is called by the buttons in the network selection UI
    /// </summary>
    private void StartNetworkMode(NetworkServer.Mode mode)
    {
        NetworkServer.Instance.StartNetworkMode(mode);

        // Disable the network root interactability untit we either succeed or fail to start the network
        _NetworkSelectionRoot.interactable = false;
    }

    /// <summary>
    /// When the relay join code input field is changed, update the join code
    /// and enable/disable the start relay client button if the join code is not empty
    /// </summary>
    private void OnRelayJoinCodeChanged(string value)
    {
        _StartRelayClientButton.interactable = !string.IsNullOrEmpty(value);
        NetworkServer.Instance.JoinCode = value;
    }

    /// <summary>
    /// Called by the network controller when the network actually starts, 
    /// update the UI to the character selection root and disable the network selection root
    /// </summary>
    private void OnNetworkStarted(NetworkServer.Mode mode)
    {
        _NetworkSelectionRoot.gameObject.SetActive(false);
        _CharacterSelectionRoot.gameObject.SetActive(true);

        switch (mode)
        {
            case NetworkServer.Mode.LocalHost:
                _MessageText.text = "Local Host";
                break;
            case NetworkServer.Mode.LocalClient:
                _MessageText.text = "Local Client";
                break;

            case NetworkServer.Mode.RelayHost:
                _MessageText.text = $"Session Join Code: {NetworkServer.Instance.JoinCode}";
                break;
            case NetworkServer.Mode.RelayClient:
                _MessageText.text = $"Session Join Code: {NetworkServer.Instance.JoinCode}";
                break;
        }
    }

    /// <summary>
    /// Called by the network controller when the network fails to start,
    /// re-enable the network selection root and update the message text to the failure reason
    /// </summary>
    private void OnNetworkFailed()
    {
        _NetworkSelectionRoot.interactable = true;
        _MessageText.text = "Failed to start network";
    }

    /// <summary>
    /// Called by the network controller when a player joins the lobby,
    /// instantiate a lobby character handler for the player and add it to the dictionary
    /// </summary>
    private void OnPlayerJoined(PlayerController player)
    {  
        // A lobby character is just a UI element that allows the player to select their color 
        // and gives feedback on the ready up state      
        LobbyCharacterHandler lobbyCharacterHandler = Instantiate(_LobbyCharacterHandlerPrefab, _LobbyCharacterHandlerContainer);
        lobbyCharacterHandler.Initialize(this, player, player.IsOwner);
        _lobbyCharacterHandlers.Add(player, lobbyCharacterHandler);
    }

    /// <summary>
    /// Called by the network controller when a player leaves the lobby,
    /// destroy the lobby character handler for the player and remove it from the dictionary
    /// </summary>
    private void OnPlayerLeft(PlayerController player)
    {        
        if (_lobbyCharacterHandlers.ContainsKey(player))
        {
            Destroy(_lobbyCharacterHandlers[player].gameObject);
            _lobbyCharacterHandlers.Remove(player);
        }
    }

    #endregion

    #region Lobby Ready Up Events

    /// <summary>
    /// When the player clicks the color selection button, allow them to ready up
    /// </summary>
    public void AllowReadyUp()
    {
        _ReadyUpButton.interactable = true;
    }

    /// <summary>
    /// When the player clicks the ready up button, notify the network controller that the owner player is ready
    /// </summary>
    public void PlayerIsReady()
    {
        // Disable the ready up button to prevent multiple clicks
        _ReadyUpButton.interactable = false;

        // Update the text to indicate that the player is waiting for other players to ready up
        TextMeshProUGUI text = _ReadyUpButton.GetComponentInChildren<TextMeshProUGUI>();
        text.text = "Waiting...";

        // Notify the network controller that the owner player is ready
        NetworkServer.Instance.PlayerIsReadyInLobby();
    }

    /// <summary>
    /// Called by the network controller to start the game once all players are ready
    /// </summary>
    public void StartGame()
    {
        // Update the text to indicate that the game is starting
        TextMeshProUGUI text = _ReadyUpButton.GetComponentInChildren<TextMeshProUGUI>();
        text.text = "Starting...";

        // Load the gameplay scene after a short delay, useful for fade
        Invoke(nameof(LoadGameplayScene), 1f);
    }

    /// <summary>
    /// Load the gameplay scene
    /// </summary>
    private void LoadGameplayScene()
    {
        SceneManager.LoadScene(_GameplaySceneName);
    }

    #endregion
}
