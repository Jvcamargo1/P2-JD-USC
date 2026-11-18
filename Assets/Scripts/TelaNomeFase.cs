using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Tela preta com o nome do nivel, mostrada no comeco da fase (ex.: "NIVEL 1: A PLANICIE").
///
/// Enquanto ela esta na tela o jogo fica congelado (timeScale = 0), para a onda de destruicao
/// nao avancar enquanto o jogador le. Qualquer tecla ou clique pula direto para o fade de saida.
/// </summary>
public class TelaNomeFase : MonoBehaviour
{
    [Header("Conteudo")]
    [Tooltip("Texto mostrado, ex.: NIVEL 1: A PLANICIE")]
    [SerializeField] private string nome = "NIVEL 1";
    [SerializeField] private TMP_Text texto;
    [SerializeField] private CanvasGroup grupo;

    [Header("Tempos (segundos reais)")]
    [SerializeField] private float fadeEntrada = 0.5f;
    [SerializeField] private float duracao = 2.5f;
    [SerializeField] private float fadeSaida = 0.6f;
    [Tooltip("Tempo minimo antes de aceitar que o jogador pule.")]
    [SerializeField] private float minimoAntesDePular = 0.4f;

    private void Start()
    {
        if (texto != null)
        {
            texto.text = nome;
        }

        // Alguns paineis do HUD (contador de macas douradas) nascem em runtime e ficariam
        // na frente desta tela; por isso ela se poe por ultimo ao aparecer.
        transform.SetAsLastSibling();

        gameObject.SetActive(true);
        StartCoroutine(Mostrar());
    }

    private IEnumerator Mostrar()
    {
        float escalaAnterior = Time.timeScale;
        Time.timeScale = 0f;

        yield return Fade(0f, 1f, fadeEntrada);

        float t = 0f;

        while (t < duracao)
        {
            t += Time.unscaledDeltaTime;

            if (t > minimoAntesDePular && Input.anyKeyDown)
            {
                break;
            }

            yield return null;
        }

        yield return Fade(1f, 0f, fadeSaida);

        // Devolve o jogo ao ritmo normal (1 se nada mais mexeu nisso).
        Time.timeScale = escalaAnterior > 0f ? escalaAnterior : 1f;
        gameObject.SetActive(false);
    }

    private IEnumerator Fade(float de, float para, float duracaoFade)
    {
        float t = 0f;

        while (t < duracaoFade)
        {
            t += Time.unscaledDeltaTime;

            if (grupo != null)
            {
                grupo.alpha = Mathf.Lerp(de, para, t / duracaoFade);
            }

            yield return null;
        }

        if (grupo != null)
        {
            grupo.alpha = para;
        }
    }
}
