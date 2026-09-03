using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;

namespace AICompanion.Companion
{
    /// <summary>
    /// Builds a short snapshot of the current campaign state to feed Claude alongside the
    /// chat history, so Cláudio comments on what is actually happening in the world instead
    /// of only what was typed in the chat window — clan, kingdom, location, wars, gold.
    /// </summary>
    public static class WorldContextBuilder
    {
        public static string Build()
        {
            try
            {
                var hero = Hero.MainHero;
                if (hero == null)
                {
                    return string.Empty;
                }

                var clan = hero.Clan;
                var kingdom = clan?.Kingdom;
                var sb = new StringBuilder();

                sb.Append("Contexto atual do mundo (use para comentar de forma natural, sem ")
                  .Append("recitar como lista): ");

                sb.Append($"Data atual: {CampaignTime.Now.GetDayOfSeason + 1}º dia da estação. ");
                sb.Append($"Jogador: {hero.Name}, clã {clan?.Name}, ");
                sb.Append($"renome {clan?.Renown:0}, clã de nível {clan?.Tier}. ");

                // Rough narrative stage, so Cláudio's tone/advice naturally tracks the
                // player's rise from nobody to ruler instead of staying static all game.
                var stage = kingdom != null && kingdom.RulingClan == clan
                    ? "governante de um reino"
                    : clan != null && clan.Tier >= 3
                        ? "um nobre respeitado, com terras e reputação"
                        : clan != null && clan.Tier >= 1
                            ? "um aventureiro em ascensão, ainda construindo nome"
                            : "praticamente um don-ninguém, começando do zero";
                sb.Append($"Estágio atual da jornada: {stage}. ");

                if (kingdom == null)
                {
                    sb.Append("Ainda sem reino próprio, viajando de forma independente. ");
                }
                else
                {
                    var isRuler = kingdom.RulingClan == clan;
                    sb.Append(isRuler
                        ? $"É o governante do reino {kingdom.Name}. "
                        : $"É vassalo do reino {kingdom.Name}, governado pelo clã {kingdom.RulingClan?.Name}. ");

                    var enemies = Kingdom.All
                        .Where(k => k != kingdom && kingdom.IsAtWarWith(k))
                        .Select(k => k.Name.ToString())
                        .ToList();
                    if (enemies.Count > 0)
                    {
                        sb.Append($"Em guerra com: {string.Join(", ", enemies)}. ");
                    }
                }

                var party = MobileParty.MainParty;
                if (party != null)
                {
                    var location = party.CurrentSettlement != null
                        ? party.CurrentSettlement.Name.ToString()
                        : "em campo aberto, entre assentamentos";
                    sb.Append($"Localização atual: {location}. ");
                    sb.Append($"Tamanho do exército: {party.MemberRoster?.TotalManCount ?? 0} homens. ");

                    var weatherModel = Campaign.Current?.Models?.MapWeatherModel;
                    if (weatherModel != null)
                    {
                        var weather = weatherModel.GetWeatherEventInPosition(party.GetPosition2D);
                        sb.Append($"Tempo agora: {DescribeWeather(weather)}. ");
                    }

                    var itemRoster = party.ItemRoster;
                    if (itemRoster != null && itemRoster.Count > 0)
                    {
                        var totalUnits = itemRoster.Sum(e => e.Amount);
                        sb.Append($"Inventário: {totalUnits} unidades de itens, em " +
                                  $"{itemRoster.Count} tipos diferentes (valor total aprox. " +
                                  $"{itemRoster.TotalValue} de ouro). ");
                    }
                    else
                    {
                        sb.Append("Inventário: vazio. ");
                    }
                }

                sb.Append($"Ouro do jogador: {hero.Gold}. ");
                sb.Append($"Influência do clã: {clan?.Influence:0}. ");

                sb.Append("Como Cláudio realmente enxerga o jogador agora, com base em tudo que " +
                          $"viu até aqui: {CompanionOpinionBehavior.DescribeForPrompt()} ");

                return sb.ToString();
            }
            catch (System.Exception ex)
            {
                // World-state reads touch a lot of live game objects; if anything here is
                // null/unavailable mid-campaign, just skip the extra context rather than
                // breaking the chat.
                Config.ModLog.Error("Failed to build world context", ex);
                return string.Empty;
            }
        }

        private static string DescribeWeather(MapWeatherModel.WeatherEvent weather)
        {
            switch (weather)
            {
                case MapWeatherModel.WeatherEvent.Clear: return "céu limpo";
                case MapWeatherModel.WeatherEvent.LightRain: return "chuva leve";
                case MapWeatherModel.WeatherEvent.HeavyRain: return "chuva forte";
                case MapWeatherModel.WeatherEvent.Snowy: return "nevando";
                case MapWeatherModel.WeatherEvent.Blizzard: return "nevasca";
                case MapWeatherModel.WeatherEvent.Storm: return "tempestade";
                default: return weather.ToString();
            }
        }
    }
}
