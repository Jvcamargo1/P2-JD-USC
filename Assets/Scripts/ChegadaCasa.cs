using System.Collections;
using UnityEngine;

/// <summary>
/// Sequencia de chegada no estilo Mario. Depois da bandeira, o jogador caminha
/// sozinho ate a porta da casa, entra (encolhe e some pela porta) e so entao
/// aparece a tela de vitoria. O GameManager ja congelou o cronometro, a onda e
/// os inimigos ao tocar a bandeira, entao a caminhada nao conta no tempo.
/// </summary>
public class ChegadaCasa : MonoBehaviour
{
    [Tooltip("Ponto da porta: o jogador caminha ate este x.")]
    [SerializeField] private Transform porta;
    [Tooltip("Pausa curta na bandeira antes de comecar a caminhar.")]
    [SerializeField] private float pausaNaBandeira = 0.35f;
    [Tooltip("Tempo para o sapo sumir pela porta.")]
    [SerializeField] private float duracaoEntrada = 0.4f;
    [Tooltip("Pausa depois de entrar, antes da tela de vitoria.")]
    [SerializeField] private float pausaAntesDaVitoria = 0.5f;
    [Tooltip("Limite de seguranca da caminhada, para a sequencia nunca travar.")]
    [SerializeField] private float tempoMaximoCaminhada = 8f;

    private bool iniciada;

    /// <summary>
    /// Inicia a sequencia. Chamado pelo GameManager quando a bandeira e tocada.
    /// </summary>
    public void Iniciar(PlayerController jogador)
    {
        if (iniciada || jogador == null)
        {
            return;
        }

        iniciada = true;
        StartCoroutine(Sequencia(jogador));
    }

    private IEnumerator Sequencia(PlayerController jogador)
    {
        yield return new WaitForSeconds(pausaNaBandeira);

        bool chegou = false;
        float alvo = porta != null ? porta.position.x : transform.position.x;
        jogador.CaminharAte(alvo, () => chegou = true);

        float limite = Time.time + tempoMaximoCaminhada;
        yield return new WaitUntil(() => chegou || Time.time > limite);

        // Entrando: o sapo encolhe e desaparece na porta.
        SpriteRenderer visual = jogador.GetComponentInChildren<SpriteRenderer>();

        if (visual != null)
        {
            Color cor = visual.color;
            Vector3 escala = visual.transform.localScale;
            float tempo = 0f;

            while (tempo < duracaoEntrada)
            {
                tempo += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(tempo / duracaoEntrada);
                visual.color = new Color(cor.r, cor.g, cor.b, k);
                visual.transform.localScale = escala * Mathf.Lerp(0.6f, 1f, k);
                yield return null;
            }

            visual.enabled = false;
        }

        yield return new WaitForSeconds(pausaAntesDaVitoria);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.VenceuFase();
        }
    }
}
