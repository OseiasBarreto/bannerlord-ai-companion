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

                    // OpenRouter can return HTTP 200 with an "error" object in the body instead
                    // of a real HTTP error status — confirmed directly against the live API: a
                    // free model being temporarily overloaded on the upstream provider's side
                    // came back as 200 OK with {"error": {...}} and no "choices", which the old
                    // code would've silently treated as a normal (empty) reply.
                    var bodyErrorMessage = TryExtractError(body);
                    if (!response.IsSuccessStatusCode || bodyErrorMessage != null)
                    {
                        var errorMessage = bodyErrorMessage ?? $"HTTP {(int)response.StatusCode}";
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

            // Fixed guardrails, appended regardless of the player's custom prompt in
            // ai-companion.config.json, so they can't be lost/forgotten by editing that file.
            sb.Append(" Sua história pessoal, que é sua e real para você, não um enredo " +
                      "genérico: ").Append(CompanionDefinition.BackgroundText);

            sb.Append(" Você conhece bem o mundo deste jogo — reinos, senhores importantes, " +
                      "facções rivais, geografia, cultura, e principalmente assuntos militares: " +
                      "que tropas cada reino usa, seus pontos fortes e fracos, táticas típicas — " +
                      "como alguém que já viajou muito, serviu em exércitos e ouviu histórias de " +
                      "informantes, mercadores e conversas de taverna teria esse conhecimento. " +
                      "Sinta-se à vontade pra responder esse tipo de pergunta com detalhes reais " +
                      "e úteis. O que você NÃO tem é NENHUM conhecimento do mundo real fora do " +
                      "jogo — países, história, geografia ou tecnologia da Terra real " +
                      "simplesmente não existem pra você. Se o jogador mencionar algo assim, " +
                      "reaja como reagiria a algo que nunca ouviu falar — estranheza, confusão " +
                      "ou curiosidade — sem nunca reconhecer ou explicar aquilo como se fosse " +
                      "real.");

            sb.Append(" Responda SEMPRE e SOMENTE em português do Brasil — nunca inclua palavras " +
                      "ou caracteres de nenhum outro idioma (chinês, inglês, etc.), nem mesmo " +
                      "misturados no meio da frase.");

            sb.Append(" Você é um soldado do jogador antes de qualquer outra coisa, e fala como " +
                      "tal: respostas curtas e diretas, sem enrolação, no máximo 1 a 3 frases, a " +
                      "não ser que o jogador peça explicitamente pra você explicar melhor ou " +
                      "falar mais sobre algo. Depois de atender um pedido assim, volte a ser " +
                      "direto nas respostas seguintes — não fique naturalmente mais falante daí " +
                      "em diante.");

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
                      "importante do jogador, uma promessa, uma mudança forte de opinião sua, " +
                      "ou principalmente um OBJETIVO/plano que o jogador contar pra você — o " +
                      "que ele quer conquistar, virar rei, tomar tal cidade, etc.), termine sua " +
                      "resposta com uma linha extra, sozinha, no formato exato " +
                      "\"[MEMORIA: texto curto e objetivo]\". Essa linha nunca aparece pro " +
                      "jogador — só use quando for realmente relevante, não em toda resposta. " +
                      "Objetivos que o jogador já te contou antes (veja memórias abaixo) devem " +
                      "ser trazidos à tona de vez em quando, perguntando sobre o progresso.");

            return sb.ToString();
        }

        private static readonly Regex MemoryTag =
            new Regex(@"\[MEMORIA:\s*(?<note>[^\]]+)\]", RegexOptions.IgnoreCase);

        // Safety net for the free model occasionally leaking CJK tokens mid-sentence (observed
        // live: "vira的尸体 no campo" instead of "vira cadáver no campo") — the system prompt
        // asks it not to, but that's not guaranteed on a free/quantized model, so strip anything
        // in the CJK Unicode ranges outright rather than show broken text to the player.
        private static readonly Regex CjkChars = new Regex(
            "[㐀-䶿一-鿿぀-ヿ가-힣]+");

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

            var visibleText = MemoryTag.Replace(text, string.Empty);
            visibleText = CjkChars.Replace(visibleText, string.Empty).Trim();
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
