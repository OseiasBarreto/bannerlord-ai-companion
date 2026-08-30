# AI Companion — Mount &amp; Blade II: Bannerlord

Mod que adiciona ao jogo um herói companheiro recrutável — **Cláudio, o Andarilho** — que
pode ser conversado em tempo real através da API da Anthropic (Claude). A conversa acontece
numa tela de chat dentro do próprio jogo, acessível pela árvore de diálogo do personagem.

**Objetivo do mod:** fazer companhia de verdade para quem joga sozinho. Por isso o Cláudio
não é um NPC de FAQ — ele tem personalidade, puxa assunto sobre a campanha, e **lembra das
conversas anteriores** mesmo depois de você salvar e fechar o jogo (o histórico é persistido
no save).

## O que o mod faz

1. **Cria um herói personalizado** (`Cláudio`) e o coloca como andarilho (`Wanderer`) numa
   taverna do mundo de Calradia, para que possa ser encontrado e recrutado normalmente pelo
   jogador, como qualquer outro companheiro.
2. **Adiciona uma opção de diálogo** "Conversar" quando você fala com ele (recrutado ou não),
   que abre uma tela de chat dedicada.
3. **Chama a API da Claude (Anthropic)** a cada mensagem enviada pelo jogador, mostra a
   resposta na tela de chat, e mantém o histórico da conversa durante a sessão de jogo.

## Estrutura do projeto

```
AICompanion/
  SubModule.xml                     Descritor do módulo (nome, id, dependências)
  AICompanion.csproj                Projeto C# (.NET Framework 4.7.2, alvo do Bannerlord)
  Src/
    SubModule.cs                    Ponto de entrada do mod
    Config/AICompanionConfig.cs     Carrega a chave de API de um arquivo local (fora do git)
    Companion/CompanionDefinition.cs  Nome, aparência e traços do herói
    Companion/CompanionBehavior.cs  CampaignBehavior: cria e posiciona o herói no mundo
    Dialog/ChatDialogBehavior.cs    Adiciona a opção de diálogo "Conversar"
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
- [ ] Testado dentro do jogo (requer instalação local do Bannerlord — não disponível neste
      ambiente de execução)

Depois de compilar e testar no seu PC, me diga o que encontrar (erros de compilação, textos
que quer mudar, comportamento do herói) que eu ajusto o código.
