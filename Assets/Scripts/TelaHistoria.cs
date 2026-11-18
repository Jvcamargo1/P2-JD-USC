using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Tela de historia em fundo preto: o texto (e a dica de pular) entram clareando, ficam visiveis
/// por um tempo generoso e saem escurecendo. Qualquer tecla ou clique pula direto para o fade de
/// saida. Ao terminar, chama o callback informado (normalmente: carregar a primeira fase).
/// </summary>
public class TelaHistoria : MonoBehaviour
{
    [Header("Textos")]
    [SerializeField] private CanvasGroup grupo;
    [SerializeField] private TMP_Text texto;
    [SerializeField] private TMP_Text dica;

    [Header("Tempos (segundos)")]
    [SerializeField] private float duracaoFadeEntrada = 1.2f;
    [SerializeField] private float duracaoExibicao = 7f;
    [SerializeField] private float duracaoFadeSaida = 1f;

    private Action aoTerminar;

    /// <summary>Mostra a tela e, ao final (ou se o jogador pular), chama aoTerminar.</summary>
    public void Mostrar(Action aoTerminar)
    {
        this.aoTerminar = aoTerminar;
        gameObject.SetActive(true);

        if (grupo != null)
        {
            grupo.alpha = 0f;
        }

        StartCoroutine(Sequencia());
    }

    private IEnumerator Sequencia()
    {
        bool pulou = false;

        // fade de entrada (pode ser interrompido por qualquer tecla/clique)
        float t = 0f;

        while (t < duracaoFadeEntrada)
        {
            if (Input.anyKeyDown)
            {
                pulou = true;
                break;
            }

            t += Time.unscaledDeltaTime;

            if (grupo != null)
            {
                grupo.alpha = Mathf.Clamp01(t / duracaoFadeEntrada);
            }

            yield return null;
        }

        if (!pulou && grupo != null)
        {
            grupo.alpha = 1f;
        }

        // tempo de leitura (tambem pulavel)
        t = 0f;

        while (!pulou && t < duracaoExibicao)
        {
            if (Input.anyKeyDown)
            {
                pulou = true;
                break;
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // fade de saida, sempre a partir da transparencia atual
        float alphaInicial = grupo != null ? grupo.alpha : 1f;
        t = 0f;

        while (t < duracaoFadeSaida)
        {
            t += Time.unscaledDeltaTime;

            if (grupo != null)
            {
                grupo.alpha = Mathf.Lerp(alphaInicial, 0f, t / duracaoFadeSaida);
            }

            yield return null;
        }

        if (grupo != null)
        {
            grupo.alpha = 0f;
        }

        gameObject.SetActive(false);
        aoTerminar?.Invoke();
    }
}
