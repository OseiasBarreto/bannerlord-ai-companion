using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AICompanion.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AICompanion.Chat
{
    /// <summary>
    /// Minimal async client for the Anthropic Messages API. Kept dependency-free (just
    /// HttpClient + Newtonsoft.Json) so it doesn't need anything beyond what ships with the
    /// game's .NET runtime.
    /// </summary>
    public sealed class ClaudeApiClient
    {
        private const string Endpoint = "https://api.anthropic.com/v1/messages";
        private const string AnthropicVersion = "2023-06-01";

        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        /// <summary>
        /// Sends the running conversation to Claude and returns the companion's reply.
        /// Throws on network/API errors — callers should catch and show a fallback line.
        /// </summary>
        public async Task<string> SendAsync(IReadOnlyList<ChatMessage> history)
        {
            var config = AICompanionConfig.Instance;
            if (!config.IsConfigured)
            {
                throw new InvalidOperationException(
                    "Nenhuma chave de API configurada em Modules/AICompanion/ai-companion.config.json");
            }

            var payload = new JObject
            {
                ["model"] = config.Model,
                ["max_tokens"] = config.MaxTokens,
                ["system"] = config.SystemPrompt,
                ["messages"] = new JArray(history
                    .Where(m => m.Role != ChatRole.System)
                    .Select(m => new JObject
                    {
                        ["role"] = m.Role == ChatRole.Player ? "user" : "assistant",
                        ["content"] = m.Text
                    }))
            };

            using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
            {
                request.Headers.Add("x-api-key", config.ApiKey);
                request.Headers.Add("anthropic-version", AnthropicVersion);
                request.Content = new StringContent(
                    payload.ToString(Formatting.None), Encoding.UTF8, "application/json");

                using (var response = await Http.SendAsync(request).ConfigureAwait(false))
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorMessage = TryExtractError(body) ?? $"HTTP {(int)response.StatusCode}";
                        throw new InvalidOperationException($"Falha na API da Claude: {errorMessage}");
                    }

                    return ExtractReplyText(body);
                }
            }
        }

        private static string ExtractReplyText(string responseBody)
        {
            var parsed = JObject.Parse(responseBody);
            var blocks = parsed["content"] as JArray;
            if (blocks == null || blocks.Count == 0)
            {
                return "(sem resposta)";
            }

            var text = string.Concat(blocks
                .Where(b => (string)b["type"] == "text")
                .Select(b => (string)b["text"]));

            return string.IsNullOrWhiteSpace(text) ? "(sem resposta)" : text.Trim();
        }

        private static string TryExtractError(string responseBody)
        {
            try
            {
                var parsed = JObject.Parse(responseBody);
                return (string)parsed["error"]?["message"];
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
