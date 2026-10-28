using System.Net.WebSockets;
using MistForest.Client.Consts;
using MistForest.Core.Protocol;
using UnityEngine;

namespace MistForest.Client.App
{
    public sealed class ClientRoot : MonoBehaviour
    {
        [SerializeField]
        private string serverUrl = $"ws://localhost:{GeneralConsts.DefaultPort}/ws";

        public ClientSession Session { get; set; } = null!;

        private void Awake()
        {
            Application.runInBackground = true;

            DontDestroyOnLoad(gameObject);

            Session = ClientSession.New();
            Session.RoomCreated += (response) => OnRoomCreated(ref response);
            Session.Error += message => Debug.LogError($"[Client] Error: {message}");
            Session.Disconnected += (closeStatus, closeStatusDescription) =>
                Debug.Log($"[Client] Disconnected: {closeStatus} {closeStatusDescription}");
        }

        private async void Start()
        {
            await Session.ConnectAsync(serverUrl);
        }

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
    }
}
