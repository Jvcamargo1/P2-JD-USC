using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Final do jogo, disparado ao tocar a Maca Dourada Gigante: congela o jogo, escurece a tela,
/// mostra o texto final (amarelo sobre preto), depois "FIM" e volta ao menu principal.
/// Qualquer tecla ou clique adianta a leitura (depois de um tempo minimo).
/// Usa tempo real, porque o jogo fica congelado (timeScale = 0) durante o final.
/// </summary>
public class FinalJogo : MonoBehaviour
{
    [SerializeField] private CanvasGroup fundo;
    [SerializeField] private CanvasGroup grupoTexto;
    [SerializeField] private CanvasGroup grupoDica;
    [SerializeField] private CanvasGroup grupoFim;
    [SerializeField] private CanvasGroup grupoCreditos;
    [Tooltip("Bloco de texto dos creditos, que sobe de baixo para cima.")]
    [SerializeField] private RectTransform textoCreditos;
    [SerializeField] private string cenaMenu = "MainMenu";

    [Header("Tempos (segundos, tempo real)")]
    [SerializeField] private float fadeFundo = 1.5f;
    [SerializeField] private float fadeTexto = 1.2f;
    [SerializeField] private float tempoLeitura = 16f;
    [SerializeField] private float leituraMinima = 1.5f;
    [SerializeField] private float fadeFim = 1.2f;
    [SerializeField] private float tempoFim = 3f;
    [Tooltip("Altura reservada ao bloco do FIM, usada para centraliza-lo antes da subida.")]
    [SerializeField] private float alturaFim = 90f;
    [Tooltip("Velocidade de subida dos creditos, em unidades de tela por segundo.")]
    [SerializeField] private float velocidadeCreditos = 110f;
    [Tooltip("Pausa depois que o ultimo nome sai da tela.")]
    [SerializeField] private float tempoAposCreditos = 1.5f;

    private bool iniciado;

    private void Awake()
    {
        Definir(fundo, 0f);
        Definir(grupoTexto, 0f);
        Definir(grupoDica, 0f);
        Definir(grupoFim, 0f);
        Definir(grupoCreditos, 0f);
    }

    /// <summary>Comeca o final (so uma vez).</summary>
    public void Iniciar()
    {
        if (iniciado)
        {
            return;
        }

        iniciado = true;
        gameObject.SetActive(true);
        StartCoroutine(Sequencia());
    }

    private IEnumerator Sequencia()
    {
        Time.timeScale = 0f;

        if (fundo != null)
        {
            fundo.blocksRaycasts = true;
        }

        yield return Fade(fundo, 0f, 1f, fadeFundo);
        yield return Fade(grupoTexto, 0f, 1f, fadeTexto);
        Definir(grupoDica, 1f);
        yield return Esperar(tempoLeitura, leituraMinima);
        Definir(grupoDica, 0f);
        yield return Fade(grupoTexto, 1f, 0f, 0.8f);
        yield return RolarCreditos();
        yield return EsperarReal(tempoAposCreditos);

        Time.timeScale = 1f;
        SceneManager.LoadScene(cenaMenu);
    }

    /// <summary>
    /// Sobe o bloco de creditos de baixo da tela ate o ultimo nome sair por cima.
    /// Nao pode ser pulado: o jogo so volta ao menu quando os creditos terminam.
    /// </summary>
    private IEnumerator RolarCreditos()
    {
        if (grupoCreditos == null || textoCreditos == null)
        {
            yield break;
        }

        var area = textoCreditos.parent as RectTransform;
        float alturaTela = area != null ? area.rect.height : Screen.height;

        // A altura so pode ser medida com o painel ja ativo e o texto montado; medir antes
        // (com o objeto desligado) devolve zero e os creditos passariam voando.
        var texto = textoCreditos.GetComponent<TMP_Text>();

        if (texto != null)
        {
            texto.ForceMeshUpdate();
            float medida = texto.preferredHeight;

            if (medida > 1f)
            {
                textoCreditos.sizeDelta = new Vector2(textoCreditos.sizeDelta.x, medida);
            }
        }

        float alturaTexto = textoCreditos.rect.height;

        // O pivo do bloco fica embaixo: o texto ocupa de y ate y + alturaTexto, e a primeira
        // linha (o "FIM") fica no topo do bloco. Ele entra sozinho no centro da tela, fica
        // parado alguns segundos e entao sobe junto com o resto dos creditos, como uma peca so.
        float inicio = -alturaTexto + alturaFim;
        float fim = (alturaTela * 0.5f) + 40f;

        textoCreditos.anchoredPosition = new Vector2(textoCreditos.anchoredPosition.x, inicio);
        yield return Fade(grupoCreditos, 0f, 1f, fadeFim);
        yield return EsperarReal(tempoFim);

        float y = inicio;

        while (y < fim)
        {
            y += velocidadeCreditos * Time.unscaledDeltaTime;
            textoCreditos.anchoredPosition = new Vector2(textoCreditos.anchoredPosition.x, y);
            yield return null;
        }

        yield return Fade(grupoCreditos, 1f, 0f, 1f);
    }

    private static IEnumerator EsperarReal(float duracao)
    {
        float t = 0f;

        while (t < duracao)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static IEnumerator Fade(CanvasGroup grupo, float de, float para, float duracao)
    {
        float t = 0f;

        while (t < duracao)
        {
            t += Time.unscaledDeltaTime;
            Definir(grupo, Mathf.Lerp(de, para, t / duracao));
            yield return null;
        }

        Definir(grupo, para);
    }

    private static IEnumerator Esperar(float duracao, float minimo)
    {
        float t = 0f;

        while (t < duracao)
        {
            t += Time.unscaledDeltaTime;

            if (t > minimo && Input.anyKeyDown)
            {
                yield break;
            }

            yield return null;
        }
    }

    private static void Definir(CanvasGroup grupo, float alpha)
    {
        if (grupo != null)
        {
            grupo.alpha = alpha;
        }
    }
}
