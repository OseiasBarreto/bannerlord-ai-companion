using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace AICompanion.Companion
{
    /// <summary>
    /// Derives a personality description straight from a hero's real vanilla data (traits,
    /// culture, occupation) instead of an authored backstory — the whole point of "Minha Mão"
    /// is that whoever the player promotes feels like themselves, not a copy of a fixed
    /// character. Confirmed via reflection: DefaultTraits.{Honor,Mercy,Valor,Generosity,
    /// Calculating} are the five personality traits the base game already tracks and updates
    /// from the player's own choices — reused here directly rather than inventing new ones.
    /// </summary>
    public static class HeroPersonalityBuilder
    {
        public static string Describe(Hero hero)
        {
            if (hero == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.Append($"Você é {hero.Name}");
            if (hero.Culture != null)
            {
                sb.Append($", de cultura {hero.Culture.Name}");
            }
            sb.Append(". ");

            DescribeTrait(sb, hero.GetTraitLevel(DefaultTraits.Honor),
                "É de palavra — cumpre o que promete e espera o mesmo dos outros.",
                "Não se prende muito a promessas ou regras quando elas atrapalham.");

            DescribeTrait(sb, hero.GetTraitLevel(DefaultTraits.Mercy),
                "Prefere poupar e perdoar quando dá pra escolher.",
                "Não hesita em ser implacável quando julga necessário.");

            DescribeTrait(sb, hero.GetTraitLevel(DefaultTraits.Valor),
                "Corajoso, não recua de uma luta.",
                "Cauteloso — evita risco quando não há necessidade real.");

            DescribeTrait(sb, hero.GetTraitLevel(DefaultTraits.Generosity),
                "Generoso com o que tem, inclusive com o próprio tempo e atenção.",
                "Econômico e reservado com o que é seu.");

            DescribeTrait(sb, hero.GetTraitLevel(DefaultTraits.Calculating),
                "Pragmático — pensa em vantagem e resultado antes de tudo.",
                "Age mais por princípio do que por cálculo frio.");

            return sb.ToString();
        }

        private static void DescribeTrait(StringBuilder sb, int level, string positive, string negative)
        {
            if (level > 0)
            {
                sb.Append(positive).Append(' ');
            }
            else if (level < 0)
            {
                sb.Append(negative).Append(' ');
            }
            // level == 0: genuinely neutral on this trait, nothing worth asserting either way.
        }
    }
}
