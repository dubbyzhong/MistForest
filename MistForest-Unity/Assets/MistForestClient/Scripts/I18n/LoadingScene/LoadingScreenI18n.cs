using UnityEngine;
using UnityEngine.Localization;

namespace MistForest.Client.I18n
{
    public class LoadingScreenI18n : MonoBehaviour
    {
        [SerializeField]
        public LocalizedString LoadingResource = new();

        [SerializeField]
        public LocalizedString LoadingResourcesFailed = new();

        [SerializeField]
        public LocalizedString ConnectingServer = new();

        [SerializeField]
        public LocalizedString ConnectingServerFailed = new();

        [SerializeField]
        public LocalizedString ConnectingSuccess = new();

        [SerializeField]
        public LocalizedString LoadingSceneFailed = new();
    }
}
