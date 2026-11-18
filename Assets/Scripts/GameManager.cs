using UnityEngine;
using System;

/// <summary>
/// Controla o estado global do jogo, a distancia percorrida (Modo Infinito)
/// e a velocidade da onda de destruicao ao longo do tempo.
/// Tambem controla a progressao da fase (vitoria) e calcula
/// o progresso do jogador e da onda para o HUD.
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum GameState { Menu, Playing, GameOver, Vitoria, Chegada, Pausado }

    public static GameManager Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private Transform player;

    [Header("Dificuldade")]
    [Tooltip("Velocidade inicial da onda de destruicao (unidades/segundo).")]
    [SerializeField] private float waveSpeedInicial = 2.0f;
    [Tooltip("Quanto a velocidade da onda aumenta por segundo de jogo.")]
    [SerializeField] private float waveAceleracao = 0.03f;
    [Tooltip("Velocidade maxima que a onda pode atingir.")]
    [SerializeField] private float waveSpeedMaxima = 7.0f;

    [Header("Fase")]
    [Tooltip("Nome mostrado no HUD, por exemplo FASE 1.")]
    [SerializeField] private string nomeFase = "";
    [Tooltip("Nome exato da cena da proxima fase (Build Settings). Vazio = ultima fase.")]
    [SerializeField] private string proximaFase = "";
    [Tooltip("Fase em torre: o progresso e medido na vertical (altura) em vez de na horizontal.")]
    [SerializeField] private bool modoVertical;

    public GameState Estado { get; private set; } = GameState.Menu;
    public float TempoDecorrido { get; private set; }
    public float VelocidadeOnda { get; private set; }
    public string NomeFase => nomeFase;
    public bool EhUltimaFase => string.IsNullOrEmpty(proximaFase);

    /// <summary>Existe uma proxima fase que pode ser carregada (esta no Build Settings).</summary>
    public bool TemProximaFase => !EhUltimaFase && CenaExisteNoBuild(proximaFase);

    /// <summary>O jogo esta pausado (tecla ESC).</summary>
    public bool Pausado => Estado == GameState.Pausado;

    /// <summary>Progresso do jogador entre o ponto de partida e a bandeira (0 a 1).</summary>
    public float ProgressoJogador { get; private set; }

    /// <summary>
    /// Posicao do jogador na barra do HUD (0 a 1). A barra comeca onde a onda
    /// comeca, entao o jogador ja nasce um pouco a frente da borda esquerda.
    /// </summary>
    public float PosicaoBarraJogador { get; private set; }

    /// <summary>Ate onde a onda ja destruiu, na mesma escala da barra (0 a 1).</summary>
    public float PosicaoBarraOnda { get; private set; }

    public event Action OnGameOver;
    public event Action OnGameStart;

    /// <summary>Disparado ao pausar (true) ou retomar (false) o jogo.</summary>
    public event Action<bool> OnPausaMudou;
    public event Action OnVitoria;

    private float posicaoInicialX;
    private float inicioBarraX;
    private float bandeiraX;
    private Transform onda;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        VelocidadeOnda = waveSpeedInicial;

        if (player != null)
        {
            posicaoInicialX = Eixo(player.position);
        }

        IniciarJogo();

        PrepararProgresso();
    }

    /// <summary>Coordenada ao longo do percurso da fase: X nas fases horizontais, Y na torre.</summary>
    private float Eixo(Vector3 posicao)
    {
        return modoVertical ? posicao.y : posicao.x;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            AlternarPausa();
        }

        if (Estado != GameState.Playing)
        {
            return;
        }

        TempoDecorrido += Time.deltaTime;

        VelocidadeOnda = Mathf.Min(
            waveSpeedInicial + (waveAceleracao * TempoDecorrido),
            waveSpeedMaxima);

        AtualizarProgresso();
    }

    /// <summary>
    /// Localiza a bandeira e a onda para calcular o progresso mostrado no HUD.
    /// </summary>
    private void PrepararProgresso()
    {
        LevelFlag bandeira = FindFirstObjectByType<LevelFlag>();
        DestructionWave ondaDestruicao = FindFirstObjectByType<DestructionWave>();

        if (bandeira != null)
        {
            bandeiraX = Eixo(bandeira.transform.position);
        }

        if (ondaDestruicao != null)
        {
            onda = ondaDestruicao.transform;
        }

        inicioBarraX = onda != null
            ? Mathf.Min(Eixo(onda.position), posicaoInicialX)
            : posicaoInicialX;
    }

    private void AtualizarProgresso()
    {
        if (player == null || bandeiraX <= inicioBarraX)
        {
            return;
        }

        float x = Eixo(player.position);

        ProgressoJogador = Mathf.Clamp01(Mathf.InverseLerp(posicaoInicialX, bandeiraX, x));
        PosicaoBarraJogador = Mathf.Clamp01(Mathf.InverseLerp(inicioBarraX, bandeiraX, x));

        PosicaoBarraOnda = onda != null
            ? Mathf.Clamp01(Mathf.InverseLerp(inicioBarraX, bandeiraX, Eixo(onda.position)))
            : 0f;
    }

    /// <summary>
    /// Pausa ou retoma o jogo (tecla ESC ou botao CONTINUAR). So vale durante a
    /// partida. Como a logica do jogo roda apenas no estado Playing, pausar
    /// congela jogador, onda, inimigos e cronometro ao mesmo tempo, e nao e
    /// possivel morrer nem vencer com o jogo pausado.
    /// </summary>
    public void AlternarPausa()
    {
        if (Estado == GameState.Playing)
        {
            Estado = GameState.Pausado;
            Time.timeScale = 0f;
            OnPausaMudou?.Invoke(true);
        }
        else if (Estado == GameState.Pausado)
        {
            Estado = GameState.Playing;
            Time.timeScale = 1f;
            OnPausaMudou?.Invoke(false);
        }
    }

    public void IniciarJogo()
    {
        CheatKonami.IniciarPartida();

        Estado = GameState.Playing;
        TempoDecorrido = 0f;
        VelocidadeOnda = waveSpeedInicial;

        if (player != null)
        {
            posicaoInicialX = Eixo(player.position);
        }

        Time.timeScale = 1f;
        OnGameStart?.Invoke();
    }

    public void FinalizarJogo()
    {
        if (Estado != GameState.Playing)
        {
            return;
        }

        Estado = GameState.GameOver;
        OnGameOver?.Invoke();
    }

    /// <summary>
    /// Chamado pela LevelFlag. Congela cronometro, onda e inimigos (tudo que so
    /// roda em Playing) e, se a fase tiver uma casa de chegada, roda a sequencia
    /// em que o jogador entra na casa antes da vitoria. Sem casa, vence na hora.
    /// </summary>
    public void IniciarChegada(PlayerController jogador)
    {
        if (Estado != GameState.Playing)
        {
            return;
        }

        ProgressoJogador = 1f;
        PosicaoBarraJogador = 1f;

        ChegadaCasa casa = FindFirstObjectByType<ChegadaCasa>();

        if (casa == null || jogador == null)
        {
            VenceuFase();
            return;
        }

        Estado = GameState.Chegada;
        casa.Iniciar(jogador);
    }

    /// <summary>
    /// Conclui a fase com vitoria: na hora ou ao fim da sequencia de chegada.
    /// </summary>
    public void VenceuFase()
    {
        if (Estado != GameState.Playing && Estado != GameState.Chegada)
        {
            return;
        }

        ProgressoJogador = 1f;
        PosicaoBarraJogador = 1f;
        Estado = GameState.Vitoria;

        // Fecha a pontuacao antes do aviso, para a tela de vitoria ja ter os valores.
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.FecharFase(TempoDecorrido);
        }

        OnVitoria?.Invoke();
    }

    /// <summary>
    /// Carrega a cena da proxima fase. Sem efeito se for a ultima fase
    /// ou se a cena ainda nao estiver no Build Settings.
    /// </summary>
    public void AvancarFase()
    {
        if (EhUltimaFase)
        {
            return;
        }

        if (!CenaExisteNoBuild(proximaFase))
        {
            Debug.LogWarning("GameManager: a cena '" + proximaFase +
                "' nao existe no Build Settings. Adicione-a para liberar a proxima fase.");
            return;
        }

        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(proximaFase);
    }

    public void Reiniciar()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Volta ao menu principal (botao da pausa, do game over e da vitoria).</summary>
    public void IrParaMenu()
    {
        // A pausa deixa o tempo parado; sem isso o menu abriria congelado.
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    /// <summary>
    /// Procura a cena pelo nome na lista do Build Settings. Percorre a lista direto,
    /// porque Application.CanStreamedLevelBeLoaded com nome falha fora do Play Mode
    /// mesmo para cenas que estao no build.
    /// </summary>
    private static bool CenaExisteNoBuild(string nome)
    {
        if (string.IsNullOrEmpty(nome))
        {
            return false;
        }

        int total = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;

        for (int i = 0; i < total; i++)
        {
            string cena = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);

            if (System.IO.Path.GetFileNameWithoutExtension(cena) == nome)
            {
                return true;
            }
        }

        return false;
    }
}
