using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Pontuacao de uma fase do Modo Classico.
///
///   Fruta coletada ........ +100 (valor definido na fruta)
///   Inimigo pisado ........ +200 (chefe +1000, valor definido no inimigo)
///   Bonus de tempo ........ (tempo-alvo - tempo gasto) x pontosPorSegundo, nunca negativo
///
/// O total da fase (pontos + bonus) e comparado com o recorde salvo em
/// PlayerPrefs, com uma chave por cena.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Bonus de tempo")]
    [Tooltip("Tempo de referencia da fase, em segundos. Terminar antes disso rende bonus.")]
    [SerializeField] private float tempoAlvo = 60f;
    [Tooltip("Pontos ganhos por segundo abaixo do tempo-alvo.")]
    [SerializeField] private int pontosPorSegundo = 20;

    private const string PrefixoRecorde = "Recorde_";

    public int Pontos { get; private set; }
    public int Frutas { get; private set; }
    public int FrutasTotal { get; private set; }
    public int PontosFrutas { get; private set; }
    public int Inimigos { get; private set; }
    public int PontosInimigos { get; private set; }
    public int Chefes { get; private set; }
    public int PontosChefes { get; private set; }

    // Macas douradas: coletaveis obrigatorios da fase
    public int MacasDouradas { get; private set; }
    public int MacasDouradasTotal { get; private set; }
    public int PontosMacasDouradas { get; private set; }

    /// <summary>Nao ha macas douradas na fase, ou todas ja foram coletadas.</summary>
    public bool TodasMacasColetadas => MacasDouradas >= MacasDouradasTotal;

    public float TempoAlvo => tempoAlvo;
    public int PontosPorSegundo => pontosPorSegundo;

    // Preenchidos quando a fase termina com vitoria
    public bool FaseFechada { get; private set; }
    public float TempoFinal { get; private set; }
    public int BonusTempo { get; private set; }
    public int Total { get; private set; }
    public int Recorde { get; private set; }
    public bool NovoRecorde { get; private set; }

    /// <summary>A partida usou o cheat e, por isso, nao grava recorde.</summary>
    public bool UsouTrapaca { get; private set; }

    /// <summary>Disparado sempre que a pontuacao muda.</summary>
    public event Action OnPontosMudaram;

    /// <summary>Disparado quando uma fruta e coletada.</summary>
    public event Action OnFrutaColetada;

    /// <summary>Disparado quando uma maca dourada e coletada.</summary>
    public event Action OnMacaDourada;

    /// <summary>Disparado quando o jogador toca a bandeira faltando macas douradas.</summary>
    public event Action OnFaltamMacas;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Conta no Awake para o HUD ja encontrar o total no seu Start.
        FrutasTotal = FindObjectsByType<Collectible>(FindObjectsSortMode.None).Length;
        MacasDouradasTotal = FindObjectsByType<MacaDourada>(FindObjectsSortMode.None).Length;
        Recorde = PlayerPrefs.GetInt(ChaveRecorde(), 0);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Soma frutas que nao estavam na cena no inicio (por exemplo, dentro de caixas).
    /// </summary>
    public void RegistrarFrutasExtras(int quantidade)
    {
        FrutasTotal += Mathf.Max(0, quantidade);
    }

    /// <summary>Soma macas douradas que nao estavam na cena no inicio (dentro de uma caixa).</summary>
    public void RegistrarMacaDouradaExtra(int quantidade)
    {
        MacasDouradasTotal += Mathf.Max(0, quantidade);
    }

    public void FrutaColetada(int valor)
    {
        if (FaseFechada)
        {
            return;
        }

        Frutas++;
        PontosFrutas += valor;
        Somar(valor);
        OnFrutaColetada?.Invoke();
    }

    public void MacaDouradaColetada(int valor)
    {
        if (FaseFechada)
        {
            return;
        }

        MacasDouradas++;
        PontosMacasDouradas += valor;
        Somar(valor);
        OnMacaDourada?.Invoke();
    }

    /// <summary>Avisa o HUD de que ainda faltam macas douradas para vencer.</summary>
    public void AvisarFaltamMacas()
    {
        OnFaltamMacas?.Invoke();
    }

    public void InimigoDerrotado(int valor, bool chefe)
    {
        if (FaseFechada)
        {
            return;
        }

        if (chefe)
        {
            Chefes++;
            PontosChefes += valor;
        }
        else
        {
            Inimigos++;
            PontosInimigos += valor;
        }

        Somar(valor);
    }

    /// <summary>
    /// Calcula o bonus de tempo e o total, e atualiza o recorde da fase.
    /// Chamado pelo GameManager antes de avisar a vitoria.
    /// </summary>
    public void FecharFase(float tempoGasto)
    {
        if (FaseFechada)
        {
            return;
        }

        FaseFechada = true;
        TempoFinal = tempoGasto;
        BonusTempo = Mathf.Max(0, Mathf.RoundToInt((tempoAlvo - tempoGasto) * pontosPorSegundo));
        Total = Pontos + BonusTempo;

        Recorde = PlayerPrefs.GetInt(ChaveRecorde(), 0);

        // Partida em que o cheat foi usado nao conta para o recorde.
        UsouTrapaca = CheatKonami.UsadoNestaPartida;
        NovoRecorde = !UsouTrapaca && Total > Recorde;

        if (NovoRecorde)
        {
            Recorde = Total;
            PlayerPrefs.SetInt(ChaveRecorde(), Total);
            PlayerPrefs.Save();
        }
    }

    private void Somar(int valor)
    {
        Pontos += valor;
        OnPontosMudaram?.Invoke();
    }

    private static string ChaveRecorde()
    {
        return PrefixoRecorde + SceneManager.GetActiveScene().name;
    }
}
