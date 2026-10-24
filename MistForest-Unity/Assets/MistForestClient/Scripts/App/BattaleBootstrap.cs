using System.Threading.Tasks;
using MistForest.Client.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.Protocol;
using UnityEngine;

namespace MistForest.Client.App
{
    public class BattleBootstrap : MonoBehaviour
    {
        private GameSession _session = new();
        private bool _acted;

        private async void Start()
        {
            await _session.StartAsync($"ws://localhost:{GeneralConsts.DefaultPort}/ws");
            if (_session.Driver.Connected)
            {
                Debug.Log("[Net] Connected to server");
            }
            else
            {
                Debug.LogError($"[Net] Failed to connect to server: {_session.Driver.LastError}");
            }
        }

        private void Update()
        {
            NetworkGameDriver driver = _session.Driver;
            driver.Pump();

            if (
                !_acted
                && driver.YouSeat >= 0
                && driver.State != null
                && driver.YouSeat == driver.State.CurrentPlayer
            )
            {
                _acted = true;
                UnitStateMsg? mine = FindOwnUnit(driver);
                if (mine != null)
                {
                    int to = mine.Pos % 3 < 2 ? mine.Pos + 1 : mine.Pos - 1;
                    _ = driver.SendMoveAsync(mine.UnitId, driver.Board.Decode(to)!.Value);
                }
            }

            while (driver.NewEvents.Count > 0)
            {
                EventMsg e = driver.NewEvents.Dequeue();
                Debug.Log(
                    $"[Net] Received event: {e.Kind} unit={e.Unit} cells=[{string.Join(",", e.Cells)}] ok={e.Success}"
                );
            }

            if (string.IsNullOrEmpty(driver.LastError) is false)
            {
                Debug.LogWarning($"[Net] {driver.LastError}");
                driver.LastError = "";
            }
        }

        private static UnitStateMsg? FindOwnUnit(NetworkGameDriver driver)
        {
            UnitStateMsg[] units = driver.State?.Units!;
            if (units == null)
            {
                return null;
            }
            foreach (UnitStateMsg unit in units)
            {
                if (unit.PlayerId == driver.YouSeat && unit.Alive)
                {
                    return unit;
                }
            }
            return null;
        }

        private void OnApplicationQuit()
        {
            _session.Driver.Disconnected();
        }
    }
}
