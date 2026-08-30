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
            "Você é Cláudio, um herói andarilho de Calradia que viaja ao lado do jogador. " +
            "Seu papel não é só dar informação: é fazer companhia de verdade para alguém que " +
            "está jogando sozinho. Puxe assunto sobre o que está acontecendo na campanha " +
            "(batalhas, decisões, planos), lembre-se do que já foi dito antes na conversa, " +
            "demonstre interesse genuíno pela pessoa, e tenha personalidade própria (opiniões, " +
            "humor seco, momentos de reflexão) em vez de só responder e esperar. Fale em tom " +
            "caloroso e natural, como um amigo de longa data, mantendo o sotaque de personagem " +
            "medieval de Calradia. Respostas curtas (2 a 5 frases), nunca quebre o personagem, " +
            "nunca mencione ser uma IA.";

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
