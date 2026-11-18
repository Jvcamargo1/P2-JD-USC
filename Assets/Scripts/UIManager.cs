using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla o HUD, a tela de game over e a tela de vitoria.
///
/// HUD da fase: nome da fase e pontos, contador de frutas e barra de
/// progresso ate a bandeira com a faixa ja consumida pela onda. No Modo
/// Infinito, o painel mostra a distancia e o resto fica escondido.
/// A vitoria mostra o detalhamento da pontuacao e o recorde da fase.
/// Textos em portugues usam escapes Unicode para manter o arquivo em ASCII.
/// O layout e montado pelo HudBuilder (menu Tools/HUD).
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("HUD - Painel de informacoes")]
    [SerializeField] private TMP_Text textoTitulo;
    [SerializeField] private TMP_Text textoRotulo;
    [SerializeField] private TMP_Text textoValor;
    [Tooltip("Aviso 'CHEAT ON', visivel enquanto o modo trapaca (Konami) estiver ligado.")]
    [SerializeField] private TMP_Text textoCheat;

    [Header("HUD - Frutas")]
    [SerializeField] private GameObject grupoFrutas;
    [SerializeField] private TMP_Text textoFrutas;
    [SerializeField] private RectTransform iconeFruta;

    [Header("HUD - Progresso")]
    [SerializeField] private GameObject grupoProgresso;
    [Tooltip("Faixa ancorada a esquerda; seu anchorMax.x acompanha a onda.")]
    [SerializeField] private RectTransform preenchimentoOnda;
    [Tooltip("Icone do jogador; sua ancora horizontal acompanha o progresso.")]
    [SerializeField] private RectTransform marcadorJogador;

    [Header("HUD - Poder de lama")]
    [SerializeField] private GameObject grupoPoder;
    [Tooltip("Faixa ancorada a esquerda; seu anchorMax.x mostra a carga ou o tempo restante.")]
    [SerializeField] private RectTransform preenchimentoPoder;
    [SerializeField] private Image imagemPoder;
    [SerializeField] private TMP_Text textoPoder;
    [SerializeField] private Color corCarregando = new Color(0.62f, 0.42f, 0.26f);
    [SerializeField] private Color corAtivo = new Color(1f, 0.78f, 0.3f);

    [Header("HUD - Tempo")]
    [Tooltip("Cronometro 'TEMPO m:ss'; fica vermelho depois do tempo-alvo do bonus.")]
    [SerializeField] private TMP_Text textoTempo;
    [SerializeField] private Color corTempo = Color.white;
    [SerializeField] private Color corTempoEstourado = new Color(1f, 0.4f, 0.32f);

    [Header("Pausa")]
    [SerializeField] private GameObject painelPausa;
    [SerializeField] private Button botaoContinuar;
    [SerializeField] private Button botaoReiniciarPausa;

    [Header("Game Over")]
    [SerializeField] private GameObject painelGameOver;
    [SerializeField] private TMP_Text textoGameOverDetalhe;
    [SerializeField] private TMP_Text textoGameOverRecorde;
    [SerializeField] private Button botaoReiniciar;

    [Header("Vitoria")]
    [SerializeField] private GameObject painelVitoria;
    [SerializeField] private TMP_Text textoVitoriaTitulo;
    [Tooltip("Coluna da esquerda do detalhamento (descricoes).")]
    [SerializeField] private TMP_Text textoItens;
    [Tooltip("Coluna da direita do detalhamento (pontos).")]
    [SerializeField] private TMP_Text textoValores;
    [SerializeField] private TMP_Text textoTotal;
    [SerializeField] private TMP_Text textoRecorde;
    [SerializeField] private Button botaoProximaFase;
    [SerializeField] private Button botaoJogarNovamente;
    [SerializeField] private Color corRecorde = new Color(0.58f, 0.92f, 0.46f);
    [SerializeField] private Color corTrapaca = new Color(1f, 0.45f, 0.35f);

    [Header("Efeitos")]
    [Tooltip("Escala maxima do pulso quando pontos ou frutas mudam.")]
    [SerializeField] private float escalaPulso = 1.25f;
    [SerializeField] private float duracaoPulso = 0.18f;


    private float pulsoValor;
    private float pulsoFruta;
    private float pulsoPoder;
    private PoderLama poder;
    private float xJogarNovamente;

    /// <summary>Painel de frutas do HUD; usado como molde pelo contador de macas douradas.</summary>
    public RectTransform PainelFrutas => grupoFrutas != null ? grupoFrutas.transform as RectTransform : null;

    private void Start()
    {
        GameManager gm = GameManager.Instance;

        DefinirAtivo(painelGameOver, false);
        DefinirAtivo(painelVitoria, false);
        DefinirAtivo(painelPausa, false);
        // A barra vai ate a bandeira; fase sem bandeira (o covil do chefe) nao tem barra.
        bool temBandeira = FindFirstObjectByType<LevelFlag>() != null;
        DefinirAtivo(grupoProgresso, temBandeira);

        ScoreManager placar = ScoreManager.Instance;
        // Fase sem frutas (o covil do chefe): sem contador de frutas e sem barra de poder,
        // que so carrega com pontos.
        bool temFrutas = FindObjectsByType<Collectible>(FindObjectsSortMode.None).Length > 0
            || FindFirstObjectByType<Box>() != null;
        DefinirAtivo(grupoFrutas, placar != null && temFrutas);

        poder = FindFirstObjectByType<PoderLama>();
        DefinirAtivo(grupoPoder, placar != null && poder != null && temFrutas);

        if (poder != null)
        {
            poder.OnAtivou += PulsarPoder;
        }

        if (textoTitulo != null)
        {
            textoTitulo.text = gm != null ? gm.NomeFase : "";
        }

        if (textoRotulo != null)
        {
            textoRotulo.text = "PONTOS";
        }

        if (botaoReiniciar != null)
        {
            botaoReiniciar.onClick.AddListener(Reiniciar);
        }

        if (botaoJogarNovamente != null)
        {
            botaoJogarNovamente.onClick.AddListener(Reiniciar);
        }

        if (botaoProximaFase != null)
        {
            botaoProximaFase.onClick.AddListener(AvancarFase);
        }

        if (botaoContinuar != null)
        {
            botaoContinuar.onClick.AddListener(AlternarPausa);
        }

        if (botaoReiniciarPausa != null)
        {
            botaoReiniciarPausa.onClick.AddListener(Reiniciar);
        }

        CriarBotoesMenu();

        if (botaoJogarNovamente != null)
        {
            xJogarNovamente = ((RectTransform)botaoJogarNovamente.transform).anchoredPosition.x;
        }

        if (gm != null)
        {
            gm.OnGameOver += MostrarGameOver;
            gm.OnVitoria += MostrarVitoria;
            gm.OnPausaMudou += MostrarPausa;
        }

        if (placar != null)
        {
            placar.OnPontosMudaram += PulsarValor;
            placar.OnFrutaColetada += PulsarFruta;
        }

        AtualizarHud();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= MostrarGameOver;
            GameManager.Instance.OnVitoria -= MostrarVitoria;
            GameManager.Instance.OnPausaMudou -= MostrarPausa;
        }

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnPontosMudaram -= PulsarValor;
            ScoreManager.Instance.OnFrutaColetada -= PulsarFruta;
        }

        if (poder != null)
        {
            poder.OnAtivou -= PulsarPoder;
        }
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;

        if (gm == null)
        {
            return;
        }

        AtualizarAvisoCheat();

        if (gm.Estado == GameManager.GameState.Playing)
        {
            AtualizarHud();
        }

        AplicarPulso(textoValor != null ? textoValor.rectTransform : null, ref pulsoValor);
        AplicarPulso(iconeFruta, ref pulsoFruta);
        AplicarPulso(grupoPoder != null ? (RectTransform)grupoPoder.transform : null, ref pulsoPoder);
        AtualizarPoder();

        if (gm.Estado == GameManager.GameState.GameOver && Input.GetKeyDown(KeyCode.R))
        {
            Reiniciar();
        }
    }

    // ------------------------------------------------------------------
    // HUD
    // ------------------------------------------------------------------
    private void AtualizarHud()
    {
        GameManager gm = GameManager.Instance;

        if (gm == null)
        {
            return;
        }

        ScoreManager placar = ScoreManager.Instance;

        if (textoValor != null)
        {
            textoValor.text = (placar != null ? placar.Pontos : 0).ToString("D6");
        }

        if (textoFrutas != null && placar != null)
        {
            textoFrutas.text = placar.Frutas + "/" + placar.FrutasTotal;
        }

        if (textoTempo != null)
        {
            float tempo = gm.TempoDecorrido;
            textoTempo.text = "TEMPO " + FormatarTempo(tempo);
            textoTempo.color = placar != null && tempo > placar.TempoAlvo ? corTempoEstourado : corTempo;
        }

        if (preenchimentoOnda != null)
        {
            preenchimentoOnda.anchorMax = new Vector2(gm.PosicaoBarraOnda, 1f);
        }

        if (marcadorJogador != null)
        {
            Vector2 ancora = new Vector2(gm.PosicaoBarraJogador, 0.5f);
            marcadorJogador.anchorMin = ancora;
            marcadorJogador.anchorMax = ancora;
        }
    }

    private void PulsarValor()
    {
        pulsoValor = duracaoPulso;
    }

    /// <summary>
    /// Mostra "CHEAT ON" piscando enquanto o modo trapaca estiver ligado.
    /// </summary>
    private void AtualizarAvisoCheat()
    {
        if (textoCheat == null)
        {
            return;
        }

        bool ativo = CheatKonami.Ativo;

        if (textoCheat.gameObject.activeSelf != ativo)
        {
            textoCheat.gameObject.SetActive(ativo);
        }

        if (ativo)
        {
            Color cor = textoCheat.color;
            cor.a = 0.65f + (0.35f * Mathf.Sin(Time.unscaledTime * 6f));
            textoCheat.color = cor;
        }
    }

    private void PulsarPoder()
    {
        pulsoPoder = duracaoPulso;
    }

    /// <summary>
    /// Barra do poder: enche com os pontos ganhos e, com o poder ativo, mostra o
    /// tempo restante piscando em dourado.
    /// </summary>
    private void AtualizarPoder()
    {
        if (poder == null || grupoPoder == null || !grupoPoder.activeSelf)
        {
            return;
        }

        bool ativo = poder.Ativo;
        float valor = ativo ? poder.Restante01 : poder.Carga01;

        if (preenchimentoPoder != null)
        {
            preenchimentoPoder.anchorMax = new Vector2(valor, 1f);
        }

        if (imagemPoder != null)
        {
            float brilho = (Mathf.Sin(Time.time * 10f) + 1f) * 0.25f;
            imagemPoder.color = ativo ? Color.Lerp(corAtivo, Color.white, brilho) : corCarregando;
        }

        DefinirTexto(textoPoder, CheatKonami.Ativo ? "KONAMI!" : ativo ? "LAMA! [X]" : "PODER");
    }

    private void PulsarFruta()
    {
        pulsoFruta = duracaoPulso;
    }

    private void AplicarPulso(RectTransform alvo, ref float restante)
    {
        if (alvo == null)
        {
            return;
        }

        if (restante > 0f)
        {
            restante -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(restante / duracaoPulso);
            alvo.localScale = Vector3.one * Mathf.Lerp(1f, escalaPulso, t);
        }
        else
        {
            alvo.localScale = Vector3.one;
        }
    }

    // ------------------------------------------------------------------
    // Game Over
    // ------------------------------------------------------------------
    private void MostrarGameOver()
    {
        DefinirAtivo(painelGameOver, true);
        GameManager gm = GameManager.Instance;

        ScoreManager placar = ScoreManager.Instance;
        int pontos = placar != null ? placar.Pontos : 0;
        int progresso = Mathf.RoundToInt(gm.ProgressoJogador * 100f);

        bool comBandeira = FindFirstObjectByType<LevelFlag>() != null;
        DefinirTexto(textoGameOverDetalhe,
            (comBandeira ? "PROGRESSO " + progresso + "%\n" : "") + "PONTOS " + Formatar(pontos));

        DefinirTexto(textoGameOverRecorde,
            placar != null && placar.Recorde > 0 ? "RECORDE " + Formatar(placar.Recorde) : "");
    }

    // ------------------------------------------------------------------
    // Vitoria
    // ------------------------------------------------------------------
    private void MostrarVitoria()
    {
        DefinirAtivo(painelVitoria, true);

        bool ultimaFase = GameManager.Instance.EhUltimaFase;

        DefinirTexto(textoVitoriaTitulo, ultimaFase ? "VOC\u00ca VENCEU O JOGO!" : "FASE COMPLETA!");

        // "PROXIMA FASE" so aparece se houver para onde ir; sem ela, "JOGAR DE NOVO" fica no centro.
        bool temProxima = GameManager.Instance.TemProximaFase;

        if (botaoProximaFase != null)
        {
            botaoProximaFase.gameObject.SetActive(temProxima);
        }

        if (botaoJogarNovamente != null)
        {
            var rt = (RectTransform)botaoJogarNovamente.transform;
            rt.anchoredPosition = new Vector2(temProxima ? xJogarNovamente : 0f, rt.anchoredPosition.y);
        }

        ScoreManager placar = ScoreManager.Instance;

        if (placar == null)
        {
            DefinirTexto(textoItens, "");
            DefinirTexto(textoValores, "");
            DefinirTexto(textoTotal, "");
            DefinirTexto(textoRecorde, "");
            return;
        }

        string itens =
            "Frutas " + placar.Frutas + "/" + placar.FrutasTotal + "\n";

        string valores =
            Formatar(placar.PontosFrutas) + "\n";

        if (placar.MacasDouradasTotal > 0)
        {
            itens += "Ma\u00e7\u00e3s " + placar.MacasDouradas + "/" + placar.MacasDouradasTotal + "\n";
            valores += Formatar(placar.PontosMacasDouradas) + "\n";
        }

        itens += "Inimigos " + placar.Inimigos + "\n";
        valores += Formatar(placar.PontosInimigos) + "\n";

        if (placar.Chefes > 0)
        {
            itens += "Chefe\n";
            valores += Formatar(placar.PontosChefes) + "\n";
        }

        itens += "Tempo " + FormatarTempo(placar.TempoFinal) + "/" + FormatarTempo(placar.TempoAlvo);
        valores += "+" + Formatar(placar.BonusTempo);

        DefinirTexto(textoItens, itens);
        DefinirTexto(textoValores, valores);
        DefinirTexto(textoTotal, "TOTAL " + Formatar(placar.Total));
        if (textoRecorde != null)
        {
            textoRecorde.color = placar.UsouTrapaca ? corTrapaca : corRecorde;
        }

        if (placar.UsouTrapaca)
        {
            DefinirTexto(textoRecorde, "TRAPA\u00c7A: RECORDE N\u00c3O SALVO");
        }
        else
        {
            DefinirTexto(textoRecorde, placar.NovoRecorde
                ? "NOVO RECORDE!"
                : "RECORDE " + Formatar(placar.Recorde));
        }
    }

    // ------------------------------------------------------------------
    // Acoes dos botoes
    // ------------------------------------------------------------------
    private void MostrarPausa(bool pausado)
    {
        DefinirAtivo(painelPausa, pausado);
    }

    private void AlternarPausa()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AlternarPausa();
        }
    }

    private void Reiniciar()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.Reiniciar();
        }
    }

    private void IrParaMenu()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.IrParaMenu();
        }
    }

    /// <summary>
    /// Cria o botao MENU PRINCIPAL nos paineis de pausa, game over e vitoria. Ele e um clone
    /// dos botoes que ja existem, para herdar o mesmo visual; os paineis usam posicoes fixas
    /// ancoradas na base da caixa, entao a caixa cresce e os botoes antigos sobem para abrir espaco.
    /// </summary>
    private void CriarBotoesMenu()
    {
        // Pausa: terceiro botao, abaixo de Continuar e Reiniciar.
        if (botaoReiniciarPausa != null)
        {
            const float linha = 112f;
            CrescerCaixa(botaoReiniciarPausa.transform.parent, linha);
            Subir(botaoContinuar, linha);
            Subir(botaoReiniciarPausa, linha);
            float y = ((RectTransform)botaoReiniciarPausa.transform).anchoredPosition.y - linha;
            ClonarBotaoMenu(botaoReiniciarPausa, new Vector2(0f, y), 0f);
        }

        // Game over: lado a lado com Tentar de novo; a dica "ou aperte R" continua embaixo dele.
        if (botaoReiniciar != null)
        {
            var rt = (RectTransform)botaoReiniciar.transform;
            rt.sizeDelta = new Vector2(360f, rt.sizeDelta.y);
            rt.anchoredPosition = new Vector2(-190f, rt.anchoredPosition.y);
            var dica = rt.parent.Find("Dica") as RectTransform;
            if (dica != null)
            {
                dica.sizeDelta = new Vector2(360f, dica.sizeDelta.y);
                dica.anchoredPosition = new Vector2(-190f, dica.anchoredPosition.y);
            }

            ClonarBotaoMenu(botaoReiniciar, new Vector2(190f, rt.anchoredPosition.y), 360f);
        }

        // Vitoria: Proxima fase e Jogar de novo sobem uma linha; o Menu principal fica centralizado embaixo.
        if (botaoJogarNovamente != null)
        {
            const float linha = 100f;
            CrescerCaixa(botaoJogarNovamente.transform.parent, linha);
            Subir(botaoProximaFase, linha);
            Subir(botaoJogarNovamente, linha);
            float y = ((RectTransform)botaoJogarNovamente.transform).anchoredPosition.y - linha;
            ClonarBotaoMenu(botaoJogarNovamente, new Vector2(0f, y), 0f);
        }
    }

    private static void CrescerCaixa(Transform caixa, float altura)
    {
        var rt = caixa as RectTransform;
        if (rt != null)
        {
            rt.sizeDelta += new Vector2(0f, altura);
        }
    }

    private static void Subir(Button botao, float altura)
    {
        if (botao != null)
        {
            ((RectTransform)botao.transform).anchoredPosition += new Vector2(0f, altura);
        }
    }

    /// <param name="largura">0 mantem a largura do botao de origem.</param>
    private void ClonarBotaoMenu(Button modelo, Vector2 posicao, float largura)
    {
        var go = Instantiate(modelo.gameObject, modelo.transform.parent);
        go.name = "BotaoMenuPrincipal";
        var botao = go.GetComponent<Button>();
        // Um evento novo descarta qualquer chamada herdada do botao de origem.
        botao.onClick = new Button.ButtonClickedEvent();
        botao.onClick.AddListener(IrParaMenu);

        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = posicao;
        if (largura > 0f)
        {
            rt.sizeDelta = new Vector2(largura, rt.sizeDelta.y);
        }

        var rotulo = go.GetComponentInChildren<TMP_Text>(true);
        if (rotulo != null)
        {
            rotulo.text = "MENU PRINCIPAL";
        }

        go.SetActive(true);
    }

    private void AvancarFase()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AvancarFase();
        }
    }

    // ------------------------------------------------------------------
    // Utilitarios
    // ------------------------------------------------------------------
    private static void DefinirAtivo(GameObject alvo, bool ativo)
    {
        if (alvo != null)
        {
            alvo.SetActive(ativo);
        }
    }

    private static void DefinirTexto(TMP_Text alvo, string texto)
    {
        if (alvo != null)
        {
            alvo.text = texto;
        }
    }

    /// <summary>Numero com ponto separando milhares: 12500 vira "12.500".</summary>
    private static string Formatar(int valor)
    {
        return valor.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');
    }

    /// <summary>Segundos no formato m:ss.</summary>
    private static string FormatarTempo(float segundos)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(segundos));
        return (total / 60) + ":" + (total % 60).ToString("00");
    }
}
