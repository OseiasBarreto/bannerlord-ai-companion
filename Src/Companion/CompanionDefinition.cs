namespace AICompanion.Companion
{
    /// <summary>
    /// Static identity for the companion hero. Kept in one place so renaming/reflavoring the
    /// character doesn't require touching the spawn or dialog logic.
    /// </summary>
    public static class CompanionDefinition
    {
        public const string HeroStringId = "aicompanion_claudio";
        public const string CharacterTemplateStringId = "wanderer_scholar"; // vanilla wanderer template used as a base
        public const string Name = "Cláudio";
        public const string FullTitle = "Cláudio, o Andarilho";

        public const string BackgroundText =
            "Dizem que Cláudio já foi escriba numa corte esquecida, antes de trocar a pena " +
            "pela espada e passar a vagar por Calradia. Fala pouco sobre o próprio passado, " +
            "mas ouve tudo — e tem sempre um conselho, uma piada seca ou uma história para " +
            "quem se dispuser a conversar com ele.";
    }
}
