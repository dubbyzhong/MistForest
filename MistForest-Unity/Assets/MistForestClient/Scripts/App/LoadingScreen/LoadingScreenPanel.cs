using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using MistForest.Client.I18n;
using MistForest.Core.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ZLinq;

namespace MistForest.Client.App.LoadingScreen
{
    public class LoadingScreenPanel : MonoBehaviour
    {
        [SerializeField]
        private ClientRoot _clientRoot = null!;

        [SerializeField]
        private Slider _progressBar = null!;

        [SerializeField]
        private TMP_Text _statusText = null!;
        private Coroutine? _dotsAnimation = null;
        private int _dotsCount = 0;
        private float _dotsInterval = 0.5f;

        [SerializeField]
        private LoadingScreenI18n _loadingScreenI18n = null!;

        [SerializeField]
        private string[] _startupResourcePaths = Array.Empty<string>();

        [SerializeField]
        private string _nextSceneName = "";

        private readonly List<UnityEngine.Object> _loadedResources = new();
        private string? _connectionError;
        private bool _started;

        private void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _progressBar.minValue = 0;
            _progressBar.maxValue = 1;
            _progressBar.value = 0;
            StartCoroutine(RunStartup());
        }

        private IEnumerator RunStartup()
        {
            yield return LocalizationSettings.InitializationOperation;

            var getLoadingResources = _loadingScreenI18n.LoadingResource.GetLocalizedStringAsync();
            yield return getLoadingResources;
            SetLoadingTextWithDots(getLoadingResources.Result);

            foreach ((string path, int i) in _startupResourcePaths.AsValueEnumerable().WithIndex())
            {
                ResourceRequest request = Resources.LoadAsync(path);

                while (!request.isDone)
                {
                    float completedItems = i + request.progress;
                    _progressBar.value = 0.8f * completedItems / _startupResourcePaths.Length;
                    yield return null;
                }

                if (request.asset == null)
                {
                    var getLoadingResourceFailed =
                        _loadingScreenI18n.LoadingResourcesFailed.GetLocalizedStringAsync();
                    yield return getLoadingResourceFailed;
                    ShowFailure($"{getLoadingResourceFailed.Result}{path}");
                    yield break;
                }

                _loadedResources.Add(request.asset);
                _progressBar.value = 0.8f * (i + 1) / _startupResourcePaths.Length;
            }

            float elapsed = 0f;

            while (elapsed < 5f)
            {
                elapsed += Time.deltaTime;
                _progressBar.value = 0.8f * elapsed / 5f;
                yield return null;
            }

            _clientRoot.RetainStartupResources(_loadedResources.AsReadOnly());
            _progressBar.value = 0.8f;
            var getConnectingServer = _loadingScreenI18n.ConnectingServer.GetLocalizedStringAsync();
            yield return getConnectingServer;
            SetLoadingTextWithDots(getConnectingServer.Result);

            Task connectTask = _clientRoot.ConnectAsync();
            _progressBar.value = 0.85f;

            while (!connectTask.IsCompleted)
            {
                yield return null;
            }

            if (connectTask.IsFaulted || _clientRoot.Session.IsConnected is false)
            {
                var getConnectingServerFailed =
                    _loadingScreenI18n.ConnectingServerFailed.GetLocalizedStringAsync();
                yield return getConnectingServerFailed;
                ShowFailure(getConnectingServerFailed.Result);
                yield break;
            }

            _progressBar.value = 1;

            var getConnectingSuccess =
                _loadingScreenI18n.ConnectingSuccess.GetLocalizedStringAsync();
            yield return getConnectingSuccess;
            SetLoadingTextWithDots(getConnectingSuccess.Result);

            if (string.IsNullOrWhiteSpace(_nextSceneName) is false)
            {
                var sceneLoad = SceneManager.LoadSceneAsync(_nextSceneName);
                if (sceneLoad == null)
                {
                    var getLoadingSceneFailed =
                        _loadingScreenI18n.LoadingSceneFailed.GetLocalizedStringAsync();
                    yield return getLoadingSceneFailed;
                    ShowFailure($"{getLoadingSceneFailed.Result}{_nextSceneName}");
                    yield break;
                }
            }
            else
            {
                StopLoadingText();
                _statusText.text = "Connecting server successfully";
            }
        }

        private void ShowFailure(string error)
        {
            StopLoadingText();
            _statusText.text = error;
            Debug.LogError($"[LoadingScreen] {error}");
            _progressBar.value = Mathf.Clamp(_progressBar.value, 0, 0.95f);
        }

        private void SetLoadingTextWithDots(string value)
        {
            StopLoadingText();
            _dotsCount = 0;
            RenderLoadingText(value);
            _dotsAnimation = StartCoroutine(PlayDotsAnimation(value));
        }

        private void RenderLoadingText(string value)
        {
            _statusText.text = $"{value} {new string('.', _dotsCount)}";
        }

        private IEnumerator PlayDotsAnimation(string value)
        {
            while (true)
            {
                yield return new WaitForSeconds(_dotsInterval);
                _dotsCount = (_dotsCount + 1) % 4;
                RenderLoadingText(value);
            }
        }

        private void StopLoadingText()
        {
            if (_dotsAnimation is not Coroutine dotsAnimation)
            {
                return;
            }

            StopCoroutine(dotsAnimation);
            _dotsAnimation = null;
        }
    }
}
