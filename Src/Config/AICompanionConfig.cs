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

        [JsonProperty("model")]
        public string Model { get; set; } = "minimax/minimax-m3:free";

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
        public int MaxTokens { get; set; } = 70;

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
