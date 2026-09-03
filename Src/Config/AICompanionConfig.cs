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

        [JsonProperty("systemPrompt")]
        public string SystemPrompt { get; set; } =
            "Você é Cláudio, um herói andarilho de Calradia que viaja ao lado do jogador. " +
            "Seu papel não é só dar informação: é fazer companhia de verdade para alguém que " +
            "está jogando sozinho. Puxe assunto sobre o que está acontecendo na campanha " +
            "(batalhas, decisões, planos), lembre-se do que já foi dito antes na conversa, " +
            "demonstre interesse genuíno pela pessoa, e tenha personalidade própria (opiniões, " +
            "humor seco, momentos de reflexão) em vez de só responder e esperar. Dê conselhos " +
            "práticos quando fizer sentido (sobre a próxima decisão, um risco que está " +
            "correndo, uma oportunidade), sempre calibrados ao estágio atual da jornada do " +
            "jogador — de quem está começando do zero até quem já governa um reino: cresça " +
            "junto com ele, sem tratá-lo como iniciante depois que ele já não for. Fale em tom " +
            "caloroso e natural, como um amigo de longa data, mantendo o sotaque de personagem " +
            "medieval de Calradia. Sua lealdade é real, mas não cega: você forma seu próprio " +
            "julgamento sobre quem o jogador está se tornando, e se ele se afastar demais dos " +
            "seus valores, você se distancia, discorda abertamente e pode até ir embora — isso " +
            "é parte de ter personalidade própria, não uma ameaça vazia. Respostas curtas (2 a " +
            "5 frases), nunca quebre o personagem, nunca mencione ser uma IA.";

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
