using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// HUD das macas douradas: um painel igual ao de frutas, logo abaixo dele, com o
/// contador "coletadas/total", e um aviso no centro da tela quando o jogador toca a
/// bandeira sem ter pegado todas.
///
/// Nao precisa estar na cena: e criado sozinho em qualquer fase que tenha macas
/// douradas (veja Iniciar) e some junto com a cena.
/// </summary>
public class HudMacasDouradas : MonoBehaviour
{
    private static readonly Color CorOuro = new Color(1f, 0.82f, 0.2f);
    private static readonly Color CorAviso = new Color(1f, 0.45f, 0.35f);

    private static bool registrado;

    private ScoreManager placar;
    private TMP_Text textoContador;
    private RectTransform icone;
    private RectTransform painel;
    private TMP_Text textoAviso;
    private Coroutine rotinaAviso;
    private float pulso;
    private Color corContadorOriginal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        if (!registrado)
        {
            registrado = true;
            SceneManager.sceneLoaded += (cena, modo) => Criar();
        }

        Criar();
    }

    private static void Criar()
    {
        ScoreManager sm = ScoreManager.Instance;

        if (sm == null || sm.MacasDouradasTotal <= 0 || FindFirstObjectByType<HudMacasDouradas>() != null)
        {
            return;
        }

        new GameObject("HudMacasDouradas").AddComponent<HudMacasDouradas>();
    }

    private void Start()
    {
        placar = ScoreManager.Instance;
        UIManager ui = FindFirstObjectByType<UIManager>();
        RectTransform molde = ui != null ? ui.PainelFrutas : null;

        if (placar == null || molde == null)
        {
            Destroy(gameObject);
            return;
        }

        // Clona o painel de frutas e tira o texto de tempo (o ultimo filho "Tempo").
        painel = Instantiate(molde.gameObject, molde.parent).GetComponent<RectTransform>();
        painel.name = "PainelMacasDouradas";

        Transform tempo = painel.Find("Tempo");

        if (tempo != null)
        {
            Destroy(tempo.gameObject);
        }

        float alturaPainel = 84f;
        painel.sizeDelta = new Vector2(molde.sizeDelta.x, alturaPainel);
        painel.anchoredPosition = molde.anchoredPosition + new Vector2(0f, -(molde.sizeDelta.y + 10f));

        Transform tIcone = painel.Find("Icone");
        Transform tQuantidade = painel.Find("Quantidade");

        if (tIcone != null)
        {
            icone = tIcone as RectTransform;
            icone.anchoredPosition = new Vector2(50f, -alturaPainel * 0.5f);
            icone.sizeDelta = new Vector2(92f, 92f);

            Image img = tIcone.GetComponent<Image>();
            MacaDourada modelo = FindFirstObjectByType<MacaDourada>();
            Sprite sp = modelo != null ? modelo.SpriteIcone : null;

            if (img != null && sp != null)
            {
                img.sprite = sp;
                img.color = Color.white;
            }
        }

        if (tQuantidade != null)
        {
            RectTransform rt = tQuantidade as RectTransform;
            rt.anchoredPosition = new Vector2(112f, -alturaPainel * 0.5f);
            textoContador = tQuantidade.GetComponent<TMP_Text>();

            if (textoContador != null)
            {
                corContadorOriginal = textoContador.color;
            }
        }

        CriarAviso(molde);
        Atualizar();

        placar.OnMacaDourada += AoColetar;
        placar.OnFaltamMacas += AoFaltarMacas;
    }

    private void OnDestroy()
    {
        if (placar != null)
        {
            placar.OnMacaDourada -= AoColetar;
            placar.OnFaltamMacas -= AoFaltarMacas;
        }

        if (painel != null)
        {
            Destroy(painel.gameObject);
        }

        if (textoAviso != null)
        {
            Destroy(textoAviso.gameObject);
        }
    }

    private void Update()
    {
        if (icone == null || pulso <= 0f)
        {
            return;
        }

        pulso -= Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(pulso / 0.25f);
        icone.localScale = Vector3.one * (1f + (0.35f * Mathf.Sin(t * Mathf.PI)));

        if (pulso <= 0f)
        {
            icone.localScale = Vector3.one;
        }
    }

    private void CriarAviso(RectTransform molde)
    {
        Canvas canvas = molde.GetComponentInParent<Canvas>();
        TMP_Text fonteModelo = textoContador;

        if (canvas == null || fonteModelo == null)
        {
            return;
        }

        GameObject go = new GameObject("AvisoMacasDouradas", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 160f);
        rt.sizeDelta = new Vector2(1500f, 120f);

        textoAviso = go.AddComponent<TextMeshProUGUI>();
        textoAviso.font = fonteModelo.font;
        textoAviso.fontSharedMaterial = fonteModelo.fontSharedMaterial;
        textoAviso.fontSize = 40f;
        textoAviso.alignment = TextAlignmentOptions.Center;
        textoAviso.raycastTarget = false;
        textoAviso.enableWordWrapping = true;
        textoAviso.gameObject.SetActive(false);
    }

    private void Atualizar()
    {
        if (textoContador == null)
        {
            return;
        }

        textoContador.text = placar.MacasDouradas + "/" + placar.MacasDouradasTotal;
        textoContador.color = placar.TodasMacasColetadas ? CorOuro : corContadorOriginal;
    }

    private void AoColetar()
    {
        Atualizar();
        pulso = 0.25f;

        if (placar.TodasMacasColetadas)
        {
            MostrarAviso("TODAS AS MAÇÃS! VÁ ATÉ A BANDEIRA", CorOuro, 3f);
        }
    }

    private void AoFaltarMacas()
    {
        int faltam = placar.MacasDouradasTotal - placar.MacasDouradas;
        string texto = faltam == 1
            ? "FALTA 1 MAÇÃ DOURADA!"
            : "FALTAM " + faltam + " MAÇÃS DOURADAS!";

        MostrarAviso(texto, CorAviso, 2.2f);
        pulso = 0.25f;
    }

    private void MostrarAviso(string texto, Color cor, float duracao)
    {
        if (textoAviso == null)
        {
            return;
        }

        if (rotinaAviso != null)
        {
            StopCoroutine(rotinaAviso);
        }

        rotinaAviso = StartCoroutine(RotinaAviso(texto, cor, duracao));
    }

    private IEnumerator RotinaAviso(string texto, Color cor, float duracao)
    {
        textoAviso.text = texto;
        textoAviso.gameObject.SetActive(true);

        float tempo = 0f;

        while (tempo < duracao)
        {
            tempo += Time.unscaledDeltaTime;
            float fade = Mathf.Clamp01((duracao - tempo) / 0.5f);
            textoAviso.color = new Color(cor.r, cor.g, cor.b, fade);
            yield return null;
        }

        textoAviso.gameObject.SetActive(false);
        rotinaAviso = null;
    }
}
