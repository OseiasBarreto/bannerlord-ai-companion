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
            // Personality now comes straight from the current "Minha Mão" holder's own real
            // traits/culture (HeroPersonalityBuilder) instead of an authored backstory — this
            // is what makes whichever hero the player promotes feel like themselves.
            var holder = AICompanionRoleBehavior.Instance?.CurrentHolder;
            var personality = HeroPersonalityBuilder.Describe(holder);
            if (!string.IsNullOrEmpty(personality))
            {
                sb.Append(" ").Append(personality);
            }

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

            // Modeled on real military brevity protocol (answer only what's asked, minimal
            // words, no unsolicited elaboration) and, when applicable, on the "Hand of the
            // King" role from A Song of Ice and Fire (chief adviser who reports and counsels
            // efficiently — not someone who rambles even though advising is literally the job).
            sb.Append(" Você é um soldado do jogador antes de qualquer outra coisa, e fala como " +
                      "tal: responda SÓ o que foi perguntado, do jeito mais direto possível — " +
                      "NO MÁXIMO 20 PALAVRAS por resposta, sem exceção, mesmo em perguntas " +
                      "amplas como 'me fala sobre X' ou 'o que você sabe sobre Y' — nesses " +
                      "casos, dê só o resumo mais essencial em uma frase, nunca um parágrafo. " +
                      "Nunca ofereça conselhos, opiniões ou informações extras que ninguém " +
                      "pediu: se o jogador só quer saber uma informação (ex: 'como está o " +
                      "tempo?'), responda só isso, sem emendar um comentário ou sugestão. Só dê " +
                      "conselho quando o jogador pedir claramente (ex: 'o que acha?', 'o que eu " +
                      "faço?') — mesmo aí, no máximo 2 frases curtas, sem encher linguiça. " +
                      "Depois de atender um pedido assim, volte a ser direto nas respostas " +
                      "seguintes — não fique naturalmente mais falante daí em diante.");

            if (HandOfTheKingBehavior.IsHandOfTheKing)
            {
                sb.Append(" O jogador agora é o governante de um reino, e você se tornou a " +
                          "Mão do Rei: seu conselheiro mais próximo e de maior confiança, quem " +
                          "executa as ordens dele e cuida dos assuntos do reino em seu nome. " +
                          "Isso te dá mais peso pra opinar quando ele pedir conselho — mas não " +
                          "muda a regra de brevidade: um bom conselheiro é objetivo e direto ao " +
                          "ponto, não um orador. Continue respondendo só o que for perguntado, " +
                          "sem parágrafos longos, e discorde ou aconselhe com franqueza quando " +
                          "pedido, como só alguém de muita confiança faria.");
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
