using TMPro;
using UnityEngine;

/// <summary>
/// Texto "+pontos" que sobe e desaparece no lugar onde a pontuacao foi ganha.
/// Ensina ao jogador quanto vale cada coisa (maca, kiwi, melancia, inimigo).
/// Use PontosFlutuantes.Mostrar() com o prefab Popup_Pontos.
/// </summary>
public class PontosFlutuantes : MonoBehaviour
{
    [SerializeField] private TextMeshPro texto;
    [Tooltip("Tempo total do efeito, em segundos.")]
    [SerializeField] private float duracao = 0.8f;
    [Tooltip("Quanto o texto sobe durante o efeito, em unidades.")]
    [SerializeField] private float subida = 1.2f;

    private float tempo;
    private Vector3 origem;
    private Color cor = Color.white;

    /// <summary>
    /// Cria o texto "+pontos" na posicao indicada, com a cor indicada.
    /// </summary>
    public static void Mostrar(PontosFlutuantes prefab, Vector3 posicao, int pontos, Color cor)
    {
        if (prefab == null)
        {
            return;
        }

        PontosFlutuantes popup = Instantiate(prefab, posicao, Quaternion.identity);
        popup.Iniciar(pontos, cor);
    }

    private void Iniciar(int pontos, Color corTexto)
    {
        origem = transform.position;
        cor = corTexto;

        if (texto != null)
        {
            texto.text = "+" + pontos;
            texto.color = cor;
        }
    }

    private void Update()
    {
        tempo += Time.deltaTime;
        float t = Mathf.Clamp01(tempo / duracao);

        // Sobe rapido e desacelera; a segunda metade do efeito apaga o texto.
        float subidaAtual = subida * (1f - ((1f - t) * (1f - t)));
        transform.position = origem + (Vector3.up * subidaAtual);

        if (texto != null)
        {
            Color c = cor;
            c.a = 1f - Mathf.Clamp01((t - 0.5f) / 0.5f);
            texto.color = c;
        }

        if (tempo >= duracao)
        {
            Destroy(gameObject);
        }
    }
}
