using System;
using UnityEngine;

/// <summary>
/// Cheat secreto: o Konami Code (cima, cima, baixo, baixo, esquerda, direita,
/// esquerda, direita, B, A) liga o modo trapaca; digitar de novo desliga.
///
/// Com o modo ligado o jogador fica imortal (PlayerController) e o poder de
/// lama fica sempre liberado (PoderLama). O estado dura ate fechar o jogo,
/// entao sobrevive a reiniciar a fase e a trocar de cena.
///
/// O detector se cria sozinho ao abrir o jogo, em qualquer cena, sem
/// precisar de configuracao.
/// </summary>
public class CheatKonami : MonoBehaviour
{
    private static readonly KeyCode[] Sequencia =
    {
        KeyCode.UpArrow, KeyCode.UpArrow,
        KeyCode.DownArrow, KeyCode.DownArrow,
        KeyCode.LeftArrow, KeyCode.RightArrow,
        KeyCode.LeftArrow, KeyCode.RightArrow,
        KeyCode.B, KeyCode.A
    };

    private static readonly KeyCode[] TodasAsTeclas = (KeyCode[])Enum.GetValues(typeof(KeyCode));

    [Tooltip("Tempo maximo entre duas teclas do codigo, em segundos.")]
    [SerializeField] private float tempoEntreTeclas = 2f;

    private int indice;
    private float ultimaTecla;

    /// <summary>O modo trapaca esta ligado.</summary>
    public static bool Ativo { get; private set; }

    /// <summary>
    /// O cheat foi usado em algum momento da partida atual (mesmo que ja tenha sido
    /// desligado). Partidas assim nao gravam recorde.
    /// </summary>
    public static bool UsadoNestaPartida { get; private set; }

    /// <summary>Disparado quando o modo trapaca liga ou desliga.</summary>
    public static event Action<bool> OnAlterado;

    // Zera o estado ao entrar no Play Mode, mesmo com "Reload Domain" desligado.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetarEstado()
    {
        Ativo = false;
        UsadoNestaPartida = false;
        OnAlterado = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CriarDetector()
    {
        if (FindFirstObjectByType<CheatKonami>() != null)
        {
            return;
        }

        var go = new GameObject("CheatKonami");
        go.AddComponent<CheatKonami>();
        DontDestroyOnLoad(go);
    }

    private void Update()
    {
        if (!Input.anyKeyDown)
        {
            return;
        }

        foreach (KeyCode tecla in TodasAsTeclas)
        {
            if (Input.GetKeyDown(tecla))
            {
                ProcessarTecla(tecla);
                break;
            }
        }
    }

    /// <summary>
    /// Avanca (ou reinicia) o progresso do codigo com a tecla apertada.
    /// </summary>
    private void ProcessarTecla(KeyCode tecla)
    {
        // Codigo parado no meio por muito tempo: recomeca.
        if (indice > 0 && Time.unscaledTime - ultimaTecla > tempoEntreTeclas)
        {
            indice = 0;
        }

        ultimaTecla = Time.unscaledTime;

        if (tecla == Sequencia[indice])
        {
            indice++;

            if (indice >= Sequencia.Length)
            {
                indice = 0;
                Alternar();
            }

            return;
        }

        indice = MaiorPrefixoAposErro(tecla);
    }

    /// <summary>
    /// Depois de uma tecla errada, procura o maior trecho do que ja foi digitado
    /// (mais a tecla errada) que ainda e o comeco do codigo. Assim um cima a
    /// mais no inicio nao estraga a tentativa.
    /// </summary>
    private int MaiorPrefixoAposErro(KeyCode tecla)
    {
        for (int tamanho = indice; tamanho >= 1; tamanho--)
        {
            // Os ultimos "tamanho" toques seriam: Sequencia[indice - tamanho + 1 .. indice - 1] e a tecla nova.
            bool combina = Sequencia[tamanho - 1] == tecla;

            for (int i = 0; combina && i < tamanho - 1; i++)
            {
                combina = Sequencia[i] == Sequencia[indice - (tamanho - 1) + i];
            }

            if (combina)
            {
                return tamanho;
            }
        }

        return 0;
    }

    /// <summary>
    /// Chamado pelo GameManager ao comecar uma partida. Ela nasce "limpa"; se o
    /// cheat ja estiver ligado (persiste entre fases), ja nasce marcada.
    /// </summary>
    public static void IniciarPartida()
    {
        UsadoNestaPartida = Ativo;
    }

    private static void Alternar()
    {
        Ativo = !Ativo;

        if (Ativo)
        {
            UsadoNestaPartida = true;
        }

        Debug.Log("[Konami] modo trapaca " + (Ativo ? "LIGADO" : "desligado"));
        OnAlterado?.Invoke(Ativo);
    }
}
