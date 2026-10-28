using System;
using MistForest.Core.Protocol;
using NativeWebSocket;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MistForest.Client.App.RoomCreateTest
{
    public class RoomCreateTestPanel : MonoBehaviour
    {
        [SerializeField]
        private ClientRoot _clientRoot = null!;

        [SerializeField]
        private TMP_InputField _modeInput = null!;

        [SerializeField]
        private TMP_InputField _playerCount = null!;

        [SerializeField]
        private TMP_InputField _teamCount = null!;

        [SerializeField]
        private Button _createRoomButton = null!;

        [SerializeField]
        private Button _exitButton = null!;

        [SerializeField]
        private TMP_Text _statusText = null!;

        private ClientSession _session => _clientRoot.Session;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            _session.RoomCreated += (response) => OnRoomCreated(ref response);
            _session.Error += OnError;
            _session.Disconnected += OnDisconnected;
        }

        // Update is called once per frame
        void Update()
        {
            _createRoomButton.interactable =
                _session.IsConnected && string.IsNullOrEmpty(_session.RoomId);
            _exitButton.interactable = _session.IsConnected;

            if (_session.IsConnected && _statusText.text == "Connecting...")
            {
                _statusText.text = "Connected, waiting for room creation...";
            }
        }

        public async void CreateRoomClicked()
        {
            if (_session.IsConnected is false)
            {
                _statusText.text = "Not connected to server";
                return;
            }

            if (int.TryParse(_playerCount.text, out int playerCount) is false || playerCount < 2)
            {
                _statusText.text = "Invalid player count";
                return;
            }

            if (
                int.TryParse(_teamCount.text, out int teamCount) is false
                || (teamCount != 0 && (teamCount < 2 || playerCount % teamCount != 0))
            )
            {
                _statusText.text = "Invalid team count";
                return;
            }

            try
            {
                _statusText.text = "Creating room...";
                await _session.CreateRoomRequestAsync(_modeInput.text, playerCount, teamCount);
            }
            catch (Exception e)
            {
                _statusText.text = $"Error: {e.Message}";
            }
        }

        public async void ExitClicked()
        {
            if (_session.IsConnected is false)
            {
                _statusText.text = "Not connected to server";
                return;
            }

            try
            {
                _statusText.text = "Disconnected...";
                await _session.DisconnectAsync(
                    (int)WebSocketCloseCode.Normal,
                    "Client disconnected"
                );
            }
            catch (Exception e)
            {
                _statusText.text = $"Error: {e.Message}";
            }
        }

        private void OnRoomCreated(ref CreateRoomResponse response)
        {
            _statusText.text = $"Room created: {response.RoomId}";
            Debug.Log($"[RoomCreateTest] Room created: {response.RoomId}");
        }

        private void OnError(string message)
        {
            _statusText.text = $"Error: {message}";
            Debug.LogError($"[RoomCreateTest] Error: {message}");
        }

        private void OnDisconnected(int closeCode, string closeStatusDescription)
        {
            _statusText.text = $"Disconnected: {closeCode}";
        }

        private void OnDestroy()
        {
            if (_clientRoot == null || _session == null)
            {
                return;
            }
            _session.RoomCreated -= (response) => OnRoomCreated(ref response);
            _session.Error -= OnError;
            _session.Disconnected -= OnDisconnected;
        }
    }
}
