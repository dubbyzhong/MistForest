using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using MistForest.Client.Consts;
using MistForest.Core.Protocol;
using UnityEngine;

namespace MistForest.Client.App
{
    public sealed class ClientRoot : MonoBehaviour
    {
        [SerializeField]
        private string serverUrl = $"ws://localhost:{GeneralConsts.DefaultPort}/ws";

        private readonly List<UnityEngine.Object> _startupResources = new();

        public ClientSession Session { get; set; } = null!;

        private void Awake()
        {
            Application.runInBackground = true;

            DontDestroyOnLoad(gameObject);

            Session = ClientSession.New();
            Session.RoomCreated += (response) => OnRoomCreated(ref response);
            Session.Error += OnError;
            Session.Disconnected += OnDisconnected;
        }

        public Task ConnectAsync()
        {
            return Session.ConnectAsync(serverUrl);
        }

        public void RetainStartupResources(IReadOnlyList<UnityEngine.Object> assets)
        {
            _startupResources.Clear();
            foreach (UnityEngine.Object asset in assets)
            {
                _startupResources.Add(asset);
            }
        }

        public void ClearStartupResources()
        {
            _startupResources.Clear();
        }

        private async void Start() { }

        private void Update()
        {
            Session.Pump();
        }

        private void OnDestroy()
        {
            if (Session != null)
            {
                _ = Session.DisconnectAsync(
                    (int)WebSocketCloseStatus.NormalClosure,
                    "Client disconnected"
                );
            }
        }

        private void OnRoomCreated(ref CreateRoomResponse response)
        {
            Debug.Log($"[Client] Room created: {response.RoomId}");
        }

        private void OnError(string message)
        {
            Debug.LogError($"[Client] Error: {message}");
        }

        private void OnDisconnected(int closeStatus, string closeStatusDescription)
        {
            Debug.Log($"[Client] Disconnected: {closeStatus} {closeStatusDescription}");
        }
    }
}
