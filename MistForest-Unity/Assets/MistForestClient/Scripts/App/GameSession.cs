using System.Threading.Tasks;

namespace MistForest.Client.App
{
    public class GameSession
    {
        public NetworkGameDriver Driver { get; } = new();

        public Task StartAsync(string url)
        {
            return Driver.ConnectAsync(url);
        }
    }
}
