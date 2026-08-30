# AI Companion — Mount &amp; Blade II: Bannerlord

Mod que adiciona ao jogo um herói companheiro recrutável — **Cláudio, o Andarilho** — que
pode ser conversado em tempo real através da API da Anthropic (Claude). A conversa acontece
numa tela de chat dentro do próprio jogo, acessível pela árvore de diálogo do personagem.

**Objetivo do mod:** fazer companhia de verdade para quem joga sozinho. Por isso o Cláudio
não é um NPC de FAQ — ele tem personalidade, puxa assunto sobre a campanha, **lembra das
conversas anteriores** mesmo depois de você salvar e fechar o jogo (o histórico é persistido
no save), e **acompanha o que está acontecendo no mundo** junto com o jogador: a cada mensagem,
o mod monta um resumo do estado atual da campanha (reino, guerras, localização, exército, ouro)
e envia junto para a Claude, então ele comenta com base no que realmente está acontecendo, não
só no que foi digitado no chat.

Ele está com você **desde o primeiro dia**: nasce direto na sua party, não precisa ser
procurado numa taverna. E a jornada é acompanhada de verdade — o mod lê o renome e o nível do
clã do jogador para saber em que estágio ele está (do "don-ninguém começando do zero" até
"governante de um reino") e Cláudio ajusta o tom e os conselhos que dá de acordo com isso, ao
invés de tratar o jogador sempre como iniciante.

Conforme a jornada do jogador avança, o vínculo também evolui: se o jogador se tornar o
governante de um reino, Cláudio deixa de ser só um andarilho e passa a ser tratado como a
**Mão do Rei** — seu conselheiro mais próximo e leal, com o título mudando no próprio jogo e o
tom das conversas passando a refletir essa proximidade.

Além de conversar, ele também **luta ao seu lado de verdade**: uma opção de diálogo "Lidere as
tropas na próxima batalha!" faz com que, na próxima missão de combate, Cláudio assuma o
comando (capitão de formação) do maior grupo de tropas do jogador.

## O que o mod faz

1. **Cria um herói personalizado** (`Cláudio`) e o coloca direto na party do jogador desde o
   início da campanha — sempre ao seu lado, sem precisar recrutar.
2. **Adiciona uma opção de diálogo** "Conversar" quando você fala com ele, que abre uma tela
   de chat dedicada.
3. **Chama a API da Claude (Anthropic)** a cada mensagem enviada pelo jogador, mostra a
   resposta na tela de chat, e mantém o histórico da conversa persistido no save.
4. **Acompanha a jornada do jogador**: renome, nível do clã, reino e o estágio narrativo atual
   (do zero até governante) entram no contexto enviado à IA a cada mensagem.
5. **Assume comando em batalha** quando pedido, via a opção de diálogo "Lidere as tropas!".

## Estrutura do projeto

```
AICompanion/
  SubModule.xml                     Descritor do módulo (nome, id, dependências)
  AICompanion.csproj                Projeto C# (.NET Framework 4.7.2, alvo do Bannerlord)
  Src/
    SubModule.cs                    Ponto de entrada do mod
    Config/AICompanionConfig.cs     Carrega a chave de API de um arquivo local (fora do git)
    Companion/CompanionDefinition.cs  Nome, títulos e traços do herói
    Companion/CompanionBehavior.cs  CampaignBehavior: cria e posiciona o herói no mundo
    Companion/HandOfTheKingBehavior.cs  Eleva o herói a "Mão do Rei" quando o jogador vira governante
    Companion/WorldContextBuilder.cs  Monta o resumo do estado atual da campanha para a IA
    Companion/CommandDelegationState.cs  Guarda o pedido de "lidere as tropas" para a próxima batalha
    Mission/CompanionCommandMissionBehavior.cs  Faz Cláudio assumir uma formação em combate
    Dialog/ChatDialogBehavior.cs    Adiciona as opções de diálogo "Conversar" e "Lidere as tropas!"
    Chat/ClaudeApiClient.cs         Cliente HTTP para a Anthropic Messages API
    Chat/ChatHistoryBehavior.cs     Persiste o histórico de conversa no save do jogo
    Chat/ChatScreen.cs              Tela de chat (Gauntlet) exibida no jogo
  ModuleData/
    (dados do módulo, se necessário)
```

## Pré-requisitos para compilar

Este repositório contém apenas o **código-fonte** do mod. Para compilar você precisa, na sua
própria máquina, de uma instalação local do Bannerlord (as DLLs do jogo não podem ser
redistribuídas aqui):

1. Instale o Bannerlord (Steam/Epic) e o **Modding Kit** oficial (via Steam → Ferramentas).
2. Abra `AICompanion.csproj` num projeto .NET (Visual Studio ou `dotnet build`).
3. Ajuste a variável de ambiente `BANNERLORD_DIR` (ou o caminho no `.csproj`) para apontar
   para a pasta de instalação do jogo, de onde o projeto referencia as DLLs em
   `bin/Win64_Shipping_Client/`.
4. Compile — o `.dll` gerado deve ser copiado para
   `Modules/AICompanion/bin/Win64_Shipping_Client/` na pasta do jogo.
5. Ative o módulo "AI Companion" no launcher do Bannerlord.

## Configurar a chave de API

A chave da API da Anthropic **não fica no repositório**. Crie o arquivo:

```
Modules/AICompanion/ai-companion.config.json
```

com o conteúdo:

```json
{
  "apiKey": "sk-ant-...",
  "model": "claude-sonnet-5",
  "systemPrompt": "Você é Cláudio, um herói andarilho de Calradia, sábio e leal ao jogador."
}
```

Esse arquivo está no `.gitignore` — nunca será versionado. Sem ele, o mod carrega mas a opção
de chat mostra um aviso pedindo para configurar a chave.

## Estado atual / próximos passos

- [x] Estrutura do projeto e descritor do módulo
- [x] Definição do herói e comportamento de spawn/recrutamento
- [x] Opção de diálogo "Conversar"
- [x] Cliente HTTP assíncrono para a API da Claude
- [x] Tela de chat in-game (Gauntlet) com histórico de mensagens
- [x] Histórico de conversa persistido no save (Cláudio lembra de sessões anteriores)
- [x] Contexto do mundo (reino, guerras, localização, ouro) enviado junto em cada mensagem
- [x] Elevação a "Mão do Rei" quando o jogador se torna governante de um reino
- [x] Spawn direto na party do jogador (sempre ao lado, desde o início)
- [x] Conselhos e tom calibrados ao estágio da jornada (renome/nível do clã)
- [x] Comando de batalha: Cláudio pode assumir uma formação quando pedido
- [ ] Testado dentro do jogo (requer instalação local do Bannerlord — não disponível neste
      ambiente de execução)

Depois de compilar e testar no seu PC, me diga o que encontrar (erros de compilação, textos
que quer mudar, comportamento do herói) que eu ajusto o código.
