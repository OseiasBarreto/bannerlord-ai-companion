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
        public string Model { get; set; } = "claude-sonnet-5";

        [JsonProperty("systemPrompt")]
        public string SystemPrompt { get; set; } =
            "Você é Cláudio, um herói andarilho de Calradia: sábio, leal ao jogador e com um " +
            "toque de humor seco. Responda sempre em poucas frases, em tom de personagem " +
            "medieval, sem quebrar o personagem.";

        [JsonProperty("maxTokens")]
        public int MaxTokens { get; set; } = 400;

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
                    Debug.Print($"[AICompanion] Config file not found at {ConfigPath}. " +
                                "Chat will be disabled until it's created.");
                    return new AICompanionConfig();
                }

                var json = File.ReadAllText(ConfigPath);
                var config = JsonConvert.DeserializeObject<AICompanionConfig>(json);
                return config ?? new AICompanionConfig();
            }
            catch (Exception ex)
            {
                Debug.Print($"[AICompanion] Failed to load config: {ex.Message}");
                return new AICompanionConfig();
            }
        }

        public static void Reload() => _instance = Load();
    }
}
