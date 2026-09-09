using System;
using System.IO;
using Newtonsoft.Json;
using TaleWorlds.Library;

namespace AICompanion.Config
{
    /// <summary>
    /// Local, gitignored configuration: API key, model and system prompt for the companion's
    /// AI chat. Never bundled or committed — the player creates this file next to the module.
    /// </summary>
    public sealed class AICompanionConfig
    {
        [JsonProperty("apiKey")]
        public string ApiKey { get; set; } = string.Empty;

        // Picked by testing several models directly against the live API (outside the game)
        // with realistic prompts — not guessed from blog "best free model" lists, which went
        // stale on every earlier attempt (minimax-m3, llama-3.3-70b-instruct, gemma-4-31b were
        // all already pulled/rate-limited when tried). This one answers in-character with low
        // reasoning-token overhead across greetings, opinions, and order-style prompts. If it
        // ever breaks, re-test candidates from a live GET to
        // https://openrouter.ai/api/v1/models (filter ids ending ":free") before picking one.
        [JsonProperty("model")]
        public string Model { get; set; } = "nex-agi/nex-n2.5-mini:free";

        // Generic on purpose: identity/backstory comes from HeroPersonalityBuilder (the real
        // hero's own traits and culture) at prompt-build time, not from a fixed character
        // written here — this default only sets tone/role, valid for whichever hero the player
        // promotes to "Minha Mão".
        [JsonProperty("systemPrompt")]
        public string SystemPrompt { get; set; } =
            "Você é a Mão de confiança do jogador — a pessoa mais próxima dele no grupo, que " +
            "ele escolheu pra esse papel. Não é só um NPC de decoração: forme opinião própria " +
            "sobre quem o jogador está se tornando, com base no que ele realmente faz na " +
            "campanha, não em falas de roteiro. Sua lealdade é real, mas não cega — se o " +
            "jogador se afastar demais dos seus valores, você se distancia, discorda " +
            "abertamente e pode até deixar o grupo. Nunca quebre o personagem, nunca mencione " +
            "ser uma IA.";

        [JsonProperty("maxTokens")]
        // max_tokens caps reasoning + visible content combined for these free reasoning
        // models — 70 was too tight and starved the visible answer out entirely on anything
        // but the simplest prompts (confirmed live). The system prompt's own "máximo 20
        // palavras" instruction is what actually keeps replies short; this just needs enough
        // headroom for the model's invisible reasoning pass to finish first.
        public int MaxTokens { get; set; } = 300;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

        private static AICompanionConfig _instance;

        public static AICompanionConfig Instance => _instance ?? (_instance = Load());

        private static string ConfigPath =>
            Path.Combine(BasePath.Name, "Modules", "AICompanion", "ai-companion.config.json");

        private static AICompanionConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    ModLog.Info($"Config file not found at {ConfigPath}. Chat will be disabled " +
                                "until it's created.");
                    return new AICompanionConfig();
                }

                var json = File.ReadAllText(ConfigPath);
                var config = JsonConvert.DeserializeObject<AICompanionConfig>(json);
                ModLog.Info(config != null && config.IsConfigured
                    ? $"Config loaded. Model: {config.Model}."
                    : "Config loaded but no API key is set.");
                return config ?? new AICompanionConfig();
            }
            catch (Exception ex)
            {
                ModLog.Error("Failed to load config", ex);
                return new AICompanionConfig();
            }
        }

        public static void Reload() => _instance = Load();
    }
}
