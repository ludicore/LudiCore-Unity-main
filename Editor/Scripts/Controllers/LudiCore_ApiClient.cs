using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace IndieBuff.Editor
{


    public class IndieBuff_ApiClient
    {
        private static readonly Lazy<IndieBuff_ApiClient> lazy = new Lazy<IndieBuff_ApiClient>(() => new IndieBuff_ApiClient());
        public static IndieBuff_ApiClient Instance => lazy.Value;

        private readonly HttpClient client;

        private string baseUrl = IndieBuff_EndpointData.GetBackendBaseUrl() + "/";

        private IndieBuff_ApiClient()
        {
            client = new HttpClient()
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromMinutes(15),
            };
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private async Task<HttpResponseMessage> SendRequestAsync(Func<Task<HttpResponseMessage>> apiCall)
        {
            client.DefaultRequestHeaders.Authorization = null;

            if (!string.IsNullOrEmpty(TokenManager.Instance.AccessToken))
            {

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenManager.Instance.AccessToken);
            }

            var response = await apiCall();

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                if (!string.IsNullOrEmpty(TokenManager.Instance.RefreshToken) && await TokenManager.Instance.RefreshTokensAsync())
                {

                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenManager.Instance.AccessToken);
                    response = await apiCall();
                }
            }

            return response;
        }

        // auth endpoint
        public Task<HttpResponseMessage> RefreshTokenAsync(string refreshToken)
        {
            client.DefaultRequestHeaders.Authorization = null;
            var request = new HttpRequestMessage(HttpMethod.Post, "plugin-auth/refresh");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshToken);
            return client.SendAsync(request);
        }

        public Task<HttpResponseMessage> CheckAuthAsync()
        {
            return SendRequestAsync(() => client.GetAsync("plugin-auth/check-auth"));
        }

        public Task<HttpResponseMessage> LogoutAsync(string refreshToken)
        {
            IndieBuff_UserInfo.Instance.selectedModel = "Base Model";
            var request = new HttpRequestMessage(HttpMethod.Post, "plugin-auth/logout");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshToken);
            return client.SendAsync(request);
        }

        public async Task<bool> LogoutUser()
        {
            return await TokenManager.Instance.LogoutTokensAsync();
        }

        public async Task StreamChatMessageAsync(string prompt, Action<string> onChunkReceived, CancellationToken cancellationToken = default)
        {

            await TokenManager.Instance.RefreshTokensAsync();
            string contextString = await IndieBuff_ContextDriver.Instance.BuildAllContext(prompt);
            var requestData = new ChatRequest { prompt = prompt, aiModel = IndieBuff_UserInfo.Instance.selectedModel, chatMode = IndieBuff_UserInfo.Instance.currentMode.ToString(), context = contextString, gameEngine = "unity", lastModel = IndieBuff_UserInfo.Instance.lastUsedModel };
            List<MessageHistoryObject> messageHistory = IndieBuff_ConvoHandler.Instance.currentMessages.Select(message => new MessageHistoryObject
            {
                role = message.Role,
                content = message.Content,
                cmd = message.ChatMode.ToString(),
            }).ToList();
            requestData.history = messageHistory;
            var jsonPayload = JsonUtility.ToJson(requestData);
            var jsonStringContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "plugin-chat/chat")
            {
                Content = jsonStringContent
            };

            var response = await SendRequestAsync(() => client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken));


            if (response.IsSuccessStatusCode)
            {
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    using (var reader = new StreamReader(stream))
                    {
                        char[] buffer = new char[32];
                        int bytesRead;

                        while ((bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            string chunk = new string(buffer, 0, bytesRead);
                            onChunkReceived.Invoke(chunk);
                        }
                    }
                }
            }
            else
            {
                Debug.Log(response.Content.ReadAsStringAsync().Result);
                throw new Exception("Error: Response was unsuccessful");
            }
        }

        public Task<HttpResponseMessage> GetIndieBuffUserAsync()
        {
            return SendRequestAsync(() => client.GetAsync("plugin-chat/user-info"));
        }

        public Task<HttpResponseMessage> GetAvailableModelsAsync()
        {
            return SendRequestAsync(() => client.GetAsync("plugin-chat/available-models"));
        }

        [Serializable]
        public class ChatRequest
        {
            public string prompt;
            public string context;
            public List<MessageHistoryObject> history = new List<MessageHistoryObject>();
            public string gameEngine;
            public string chatMode;
            public string aiModel;
            public string lastModel;
        }

        [Serializable]
        public class MessageHistoryObject
        {
            public string role;
            public string content;
            public string cmd;
        }

    }
}