# Sapo Wilson e as Maçãs Douradas

Jogo de plataforma 2D feito em Unity para a P2 da disciplina Desenvolvimento de Jogos Digitais, do curso de Ciência da Computação da USC, com o professor Luis Filipe Grael Tinos.

O jogador controla o Sapo Wilson por cinco fases e precisa coletar 30 maçãs douradas para chegar ao reduto de um dragão. Nas quatro primeiras fases o cenário desaba atrás do jogador. A última fase é a luta contra o chefe.

## Integrante

| Nome | Responsabilidades |
| --- | --- |
| João Vitor Camargo | Game design, programação, level design, integração de arte e áudio |

O projeto foi feito individualmente, em acordo com o professor.

## Executando o jogo

Requisito: Windows 10 ou 11, 64 bits.

O executável pode ser encontrado nas releases do repositorio ou através deste link: https://github.com/Jvcamargo1/P2-JD-USC/releases/tag/Build

1. Extraia o .zip inteiro para uma pasta.
2. Execute `SapoWilson.exe`.

O `SapoWilson.exe` depende dos arquivos que ficam ao lado dele: a pasta `SapoWilson_Data/`, a pasta `MonoBleedingEdge/` e o `UnityPlayer.dll`. Copiado sozinho para outro lugar, ele não abre.

Como o executável não é assinado, o Windows pode bloqueá-lo na primeira execução. Nos testes, o antivírus fechou o jogo sem mostrar nenhuma janela. Se aparecer o aviso do SmartScreen, clique em "Mais informações" e depois em "Executar assim mesmo". Se o antivírus bloquear, adicione a pasta do jogo às exclusões.

## Controles

| Ação | Teclas |
| --- | --- |
| Mover | Setas esquerda e direita, ou A e D |
| Pular | Espaço ou W |
| Atirar lama | X ou J, com a barra de poder cheia |
| Pausar | Esc |
| Pular telas de texto | Qualquer tecla |

## Abrindo o projeto no Unity

1. Instale o Unity 6000.0.84f1 pelo Unity Hub, com o módulo Windows Build Support.
2. Clone o repositório:

   ```
   git clone https://github.com/Jvcamargo1/P2-JD-USC.git
   ```

3. No Unity Hub, use Add > Add project from disk e selecione a pasta clonada.
4. Abra `Assets/Scenes/MainMenu.unity` e aperte Play.

A primeira abertura reconstrói a pasta `Library/` e leva alguns minutos.

Para gerar um executável novo, use File > Build Profiles, escolha Windows e clique em Build. As seis cenas já estão registradas na ordem de execução: MainMenu, Phase1, Phase2, Phase3, Phase4 e Phase5.

## Estrutura de pastas

| Pasta | Conteúdo |
| --- | --- |
| `Assets/Scenes` | Menu principal e as cinco fases |
| `Assets/Scripts` | Código do jogo |
| `Assets/Editor` | Ferramentas de editor que geram as fases, o HUD e o menu |
| `Assets/Levels` | Mapas das fases em texto, de `Phase1.txt` a `Phase5.txt` |
| `Assets/Prefabs` | Inimigos, frutas, obstáculos, chefe e elementos do HUD |
| `Assets/Resources` | `BancoDeSons.asset`, que referencia todos os sons |
| `Assets/Sounds` | Músicas e efeitos sonoros |

## Organização do código

Os scripts ficam em `Assets/Scripts`, com nomes e comentários em português.

| Área | Scripts |
| --- | --- |
| Estado do jogo e pontuação | `GameManager`, `ScoreManager`, `LevelFlag`, `ChegadaCasa`, `FinalJogo` |
| Jogador | `PlayerController`, `PoderLama`, `ProjetilLama`, `CameraFollow` |
| Inimigos e chefe | `Enemy`, `ChefeDragao`, `ProjetilFogo`, `PortaoChefe`, `MacaDouradaGigante`, `ExplosaoVisual` |
| Cenário e obstáculos | `Obstacle`, `FireTrap`, `PedraEsmagadora`, `MovingFallingPlatform`, `PlataformaEsteira`, `Trampoline`, `Box`, `PlatformSegment`, `DestructionWave`, `ParallaxLayer` |
| Coleta | `Collectible`, `MacaDourada`, `HudMacasDouradas`, `PontosFlutuantes` |
| Interface e menus | `UIManager`, `MenuPrincipal`, `MenuBotaoVisual`, `TelaHistoria`, `TelaNomeFase`, `DicaControles`, `DicaObjetivo` |
| Áudio | `GerenciadorDeAudio`, `BancoDeSons` |
| Teste | `CheatKonami` |

`SombraPerseguidora` continua no projeto, mas o inimigo foi retirado e não aparece em nenhuma fase.

### Fases geradas a partir de texto

`Assets/Editor/LevelBuilder.cs` lê o arquivo `Assets/Levels/PhaseN.txt` e monta a cena correspondente. Cada caractere do arquivo representa um tipo de bloco ou objeto, e o mapeamento está no próprio `LevelBuilder.cs`. A Fase 3, que é uma torre vertical, usa também `LevelBuilder.Torre.cs` e `Fase3Gerador.cs`.

O terreno é montado em blocos de Tilemap, cada um com um `PlatformSegment`. São esses blocos que a onda de destruição derruba, um por vez.

As fases 1, 2 e 3 receberam ajustes manuais na cena depois de geradas. Rodar o gerador de novo nessas fases desfaz os ajustes.

### Áudio

O `GerenciadorDeAudio` é criado automaticamente na inicialização, por `RuntimeInitializeOnLoadMethod`, e sobrevive à troca de cenas com `DontDestroyOnLoad`. Ele escolhe a música pelo nome da cena carregada.

O gerenciador tem um `AudioListener` próprio, que segue o jogador no plano z = 0. Com o ouvinte na câmera, a 22 unidades do plano do jogo, os sons posicionais ficavam praticamente mudos. Os efeitos posicionais usam atenuação linear entre 4 e 16 unidades de distância.

Vários arquivos de som começavam com até meio segundo de silêncio. O gerenciador mede esse trecho em cada clipe e começa a tocar depois dele.

## Pontuação

| Evento | Pontos |
| --- | --- |
| Maçã, banana ou laranja | 100 |
| Kiwi ou morango | 250 |
| Maçã dourada | 500 |
| Inimigo derrotado | 200 |
| Dragão derrotado | 1000 |

No fim de cada fase entra um bônus de 20 pontos por segundo que sobrar do tempo-alvo. O tempo-alvo é de 65 s nas fases 1 e 2, 100 s na fase 3 e 70 s nas fases 4 e 5. O melhor resultado de cada fase fica salvo e aparece no menu Fases.

## Modo de teste

Digitar o Konami Code (↑ ↑ ↓ ↓ ← → ← → B A) durante uma fase liga um modo de teste: o jogador fica imortal e o tiro de lama fica sempre liberado. Digitar a sequência de novo desliga. Partidas em que o modo foi usado não gravam recorde.

## Créditos

Arte:

- Pixel Adventure, de Pixel Frog
- Mountains Tileset and Backgrounds, de karsiori
- Dragon Warrior
- 2D Mega Pack, da Brackeys
- Free Platform Game Assets, da Bayat Games
- 2D Cartoon Parallax Background
- Ilustração do fundo do menu principal: gerada com inteligência artificial, no ChatGPT

Fonte: Press Start 2P.

Músicas e efeitos sonoros: todos obtidos em [pixabay.com](https://pixabay.com), dos autores 666herohero, alex-morgan, alice_soundz, composiia, dennish18, dominic251, dragon-studio, edr, emand_edroff, freesound_community, lesiakower, liecio, nickpanek, sound_garage, sunspellauditory e universfield.

Os mesmos créditos aparecem no jogo, no botão Créditos do menu e na rolagem final.

## Pacotes

| Pacote | Versão |
| --- | --- |
| Input System | 1.20.0 |
| Universal Render Pipeline | 17.0.4 |
| uGUI (inclui TextMeshPro) | 2.0.0 |
| Timeline | 1.8.13 |
| `com.unity.pipeline` | 0.8.0-exp.1 |

O `com.unity.pipeline` é uma ferramenta de automação do Editor usada durante o desenvolvimento. O jogo não depende dele para rodar.
