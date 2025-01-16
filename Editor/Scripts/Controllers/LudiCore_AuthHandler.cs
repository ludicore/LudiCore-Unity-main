using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace IndieBuff.Editor
{
    public class IndieBuff_AuthHandler
    {
        public event Action OnLoginSuccess;
        public event Action<string> OnLoginError;
        private HttpListener listener;
        private bool isListening;
        private int currentPort;
        public bool isCheckingLoginStatus = true;
        private readonly CancellationTokenSource cancellationTokenSource;

        private const int LOGIN_TIMEOUT_SECONDS = 300;
        private const int MAX_SERVER_START_ATTEMPTS = 5;

        public IndieBuff_AuthHandler()
        {
            cancellationTokenSource = new CancellationTokenSource();
            _ = CheckLoginStatus();
        }

        public async Task CheckLoginStatus()
        {
            try
            {
                var response = await IndieBuff_ApiClient.Instance.CheckAuthAsync();
                if (response.IsSuccessStatusCode)
                {
                    OnLoginSuccess?.Invoke();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Login status check failed: {e}");
            }
            finally
            {
                isCheckingLoginStatus = false;
            }
        }

        public async void StartLoginProcessAsync()
        {
            if (isListening)
            {
                OpenLoginPage();
                return;
            }
            try
            {
                bool serverStarted = await StartLocalServerWithRetry();

                if (serverStarted)
                {
                    OpenLoginPage();
                    _ = HandleLoginTimeout();
                }
                else
                {
                    Debug.LogError("Failed to start local server after multiple attempts");
                    OnLoginError?.Invoke("Error with login, ports unavailable!");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to start login process: {e}");
                await StopServer();
            }
        }

        private async Task<bool> StartLocalServerWithRetry()
        {
            for (int attempt = 0; attempt < MAX_SERVER_START_ATTEMPTS; attempt++)
            {
                foreach (int port in IndieBuff_EndpointData.GetLocalServerPorts())
                {
                    try
                    {
                        listener = new HttpListener();
                        string prefix = $"http://localhost:{port}/";
                        listener.Prefixes.Add(prefix);
                        listener.Start();

                        isListening = true;
                        currentPort = port;

                        _ = ListenForRequests();
                        return true;
                    }
                    catch (HttpListenerException)
                    {
                        CleanupListener();
                        continue;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to start server on port {port}: {e}");
                        CleanupListener();
                        continue;
                    }
                }
                await Task.Delay(1000);
            }
            return false;
        }

        private async Task ListenForRequests()
        {
            try
            {
                while (isListening && !cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var getContextTask = Task.Run(() => listener.GetContext());
                    var context = await getContextTask;
                    _ = HandleRequest(context);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e) when (e is ObjectDisposedException || e is HttpListenerException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in request listener: {e}");
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            try
            {
                ConfigureCorsHeaders(context.Response);

                if (context.Request.HttpMethod == "OPTIONS")
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                    context.Response.Close();
                    return;
                }

                string requestBody = await ReadRequestBody(context.Request);

                if (context.Request.RawUrl.Contains("status=success"))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    await HandleSuccessfulLogin(requestBody);
                }
                else
                {

                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error handling request: {e}");
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            finally
            {
                try
                {
                    context.Response.Close();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error closing response: {e}");
                }
            }
        }

        private void ConfigureCorsHeaders(HttpListenerResponse response)
        {
            try
            {
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
                response.Headers.Add("Access-Control-Max-Age", "86400");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error configuring CORS headers: {e}");
            }
        }

        private async Task<string> ReadRequestBody(HttpListenerRequest request)
        {
            try
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                return await reader.ReadToEndAsync();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error reading request body: {e}");
                return string.Empty;
            }
        }

        private async Task HandleSuccessfulLogin(string requestBody)
        {
            try
            {
                var tokens = JsonUtility.FromJson<TokenResponse>(requestBody);
                TokenManager.Instance.SaveTokens(tokens.accessToken, tokens.refreshToken);
                await StopServer();
                OnLoginSuccess?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error handling login: {e}");
            }
            finally
            {
                await StopServer();
            }
        }

        private async Task HandleLoginTimeout()
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(LOGIN_TIMEOUT_SECONDS), cancellationTokenSource.Token);
                await StopServer();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in login timeout: {e}");
                OnLoginError?.Invoke("Error with login, timed out!");
                await StopServer();
            }
        }

        public async Task StopServer()
        {
            if (!isListening) return;

            isListening = false;
            cancellationTokenSource.Cancel();

            await Task.Delay(100);
            CleanupListener();
        }

        private void CleanupListener()
        {
            try
            {
                if (listener != null)
                {
                    listener.Close();
                    listener = null;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error stopping server: {e}");
            }
        }

        private void OpenLoginPage()
        {
            try
            {
                string redirectUri = $"http://localhost:{currentPort}";
                string encodedRedirectUri = UnityWebRequest.EscapeURL(redirectUri);
                string loginUrl = $"{IndieBuff_EndpointData.GetFrontendBaseUrl()}/login?source=unity&redirectUri={encodedRedirectUri}";
                Application.OpenURL(loginUrl);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error opening login page: {e}");
                _ = StopServer();
            }
        }

        private class TokenResponse
        {
            public string accessToken;
            public string refreshToken;
        }
    }
}