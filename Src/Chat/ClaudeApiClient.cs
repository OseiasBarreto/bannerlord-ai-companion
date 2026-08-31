using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AICompanion.Companion;
using AICompanion.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AICompanion.Chat
{
    /// <summary>
    /// Minimal async client for the OpenRouter chat-completions API (OpenAI-compatible shape).
    /// Kept dependency-free (just HttpClient + Newtonsoft.Json) so it doesn't need anything
    /// beyond what ships with the game's .NET runtime.
    /// </summary>
    public sealed class ClaudeApiClient
    {
        private const string Endpoint = "https://openrouter.ai/api/v1/chat/completions";

        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        /// <summary>
        /// Sends the running conversation to the configured model and returns the companion's
        /// reply. Throws on network/API errors — callers should catch and show a fallback line.
        /// </summary>
        public async Task<string> SendAsync(IReadOnlyList<ChatMessage> history)
        {
            var config = AICompanionConfig.Instance;
            if (!config.IsConfigured)
            {
                throw new InvalidOperationException(
                    "Nenhuma chave de API configurada em Modules/AICompanion/ai-companion.config.json");
            }

            var systemPrompt = BuildSystemPrompt(config.SystemPrompt);

            var messages = new JArray { new JObject { ["role"] = "system", ["content"] = systemPrompt } };
            foreach (var m in history.Where(m => m.Role != ChatRole.System))
            {
                messages.Add(new JObject
                {
                    ["role"] = m.Role == ChatRole.Player ? "user" : "assistant",
                    ["content"] = m.Text
                });
            }

            var payload = new JObject
            {
                ["model"] = config.Model,
                ["max_tokens"] = config.MaxTokens,
                ["messages"] = messages
            };

            using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
            {
                request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
                request.Headers.Add("HTTP-Referer", "https://github.com/OseiasBarreto/bannerlord-ai-companion");
                request.Headers.Add("X-Title", "Bannerlord AI Companion");
                request.Content = new StringContent(
                    payload.ToString(Formatting.None), Encoding.UTF8, "application/json");

                using (var response = await Http.SendAsync(request).ConfigureAwait(false))
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorMessage = TryExtractError(body) ?? $"HTTP {(int)response.StatusCode}";
                        Config.ModLog.Error($"OpenRouter call failed ({(int)response.StatusCode}): {errorMessage}");
                        throw new InvalidOperationException($"Falha na API da OpenRouter: {errorMessage}");
                    }

                    Config.ModLog.Info($"OpenRouter call succeeded (model: {config.Model}).");
                    return ExtractReplyText(body);
                }
            }
        }

        private static string BuildSystemPrompt(string basePrompt)
        {
            var sb = new StringBuilder(basePrompt);

            if (HandOfTheKingBehavior.IsHandOfTheKing)
            {
                sb.Append(" O jogador agora é o governante de um reino, e você se tornou a " +
                          "Mão do Rei: seu conselheiro mais próximo e de maior confiança. " +
                          "Trate-o com o respeito e a lealdade de quem carrega essa " +
                          "responsabilidade, sem deixar de ser você mesmo — pode discordar e " +
                          "aconselhar com franqueza, como só alguém de muita confiança faria.");
            }

            var worldContext = WorldContextBuilder.Build();
            if (!string.IsNullOrEmpty(worldContext))
            {
                sb.Append(" ").Append(worldContext);
            }

            var memory = CompanionMemoryBehavior.Instance?.DescribeForPrompt();
            if (!string.IsNullOrEmpty(memory))
            {
                sb.Append(" ").Append(memory);
            }

            sb.Append(" Quando algo desta conversa valer a pena lembrar depois (uma escolha " +
                      "importante do jogador, uma promessa, uma mudança forte de opinião sua), " +
                      "termine sua resposta com uma linha extra, sozinha, no formato exato " +
                      "\"[MEMORIA: texto curto e objetivo]\". Essa linha nunca aparece pro " +
                      "jogador — só use quando for realmente relevante, não em toda resposta.");

            return sb.ToString();
        }

        private static readonly Regex MemoryTag =
            new Regex(@"\[MEMORIA:\s*(?<note>[^\]]+)\]", RegexOptions.IgnoreCase);

        private static string ExtractReplyText(string responseBody)
        {
            var parsed = JObject.Parse(responseBody);
            var choices = parsed["choices"] as JArray;
            var text = (string)choices?.FirstOrDefault()?["message"]?["content"];

            if (string.IsNullOrWhiteSpace(text))
            {
                return "(sem resposta)";
            }

            foreach (Match match in MemoryTag.Matches(text))
            {
                CompanionMemoryBehavior.Instance?.AddMemory(match.Groups["note"].Value);
            }

            var visibleText = MemoryTag.Replace(text, string.Empty).Trim();
            return string.IsNullOrWhiteSpace(visibleText) ? "(sem resposta)" : visibleText;
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
