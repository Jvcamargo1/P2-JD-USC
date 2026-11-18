using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menu inicial: JOGAR comeca pela primeira fase, FASES permite escolher (com o recorde de cada uma)
/// e SAIR fecha o jogo. As fases que existirem no Build Settings (Phase1, Phase2, ...) ficam liberadas;
/// as demais aparecem como EM BREVE. Funciona com mouse, setas + Enter e controle.
/// </summary>
[DisallowMultipleComponent]
public class MenuPrincipal : MonoBehaviour
{
    [Header("Fases")]
    [SerializeField] private string primeiraFase = "Phase1";
    [SerializeField] private string prefixoFase = "Phase";

    [Header("Paineis")]
    [SerializeField] private GameObject painelPrincipal;
    [SerializeField] private GameObject painelFases;
    [SerializeField] private GameObject painelCreditos;

    [Header("Historia")]
    [SerializeField] private TelaHistoria telaHistoria;

    [Header("Botoes")]
    [SerializeField] private Button botaoJogar;
    [SerializeField] private Button botaoFases;
    [SerializeField] private Button botaoVoltar;
    [SerializeField] private Button botaoCreditos;
    [SerializeField] private Button botaoVoltarCreditos;
    [SerializeField] private Button[] botoesFase;
    [SerializeField] private TMP_Text[] textosRecorde;

    [Header("Efeitos")]
    [SerializeField] private CanvasGroup fade;
    [SerializeField] private RectTransform fundo;
    [SerializeField] private RectTransform titulo;
    [SerializeField] private float duracaoFadeEntrada = 0.8f;
    [SerializeField] private float duracaoFadeSaida = 0.45f;
    [SerializeField] private float zoomFundo = 0.05f;
    [SerializeField] private float periodoZoom = 40f;
    [SerializeField] private float amplitudeTitulo = 7f;

    private bool carregando;
    private Vector2 tituloBase;
    private GameObject ultimoSelecionado;

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (titulo != null)
        {
            tituloBase = titulo.anchoredPosition;
        }

        if (painelFases != null)
        {
            painelFases.SetActive(false);
        }

        if (painelCreditos != null)
        {
            painelCreditos.SetActive(false);
        }

        if (painelPrincipal != null)
        {
            painelPrincipal.SetActive(true);
        }

        if (fade != null)
        {
            fade.alpha = 1f;
            fade.blocksRaycasts = true;
        }
    }

    private void Start()
    {
        PreencherFases();
        Selecionar(botaoJogar != null ? botaoJogar.gameObject : null);
        StartCoroutine(FadeEntrada());
    }

    private void Update()
    {
        float t = Time.unscaledTime;

        if (fundo != null)
        {
            float s = 1f + zoomFundo * 0.5f * (1f - Mathf.Cos(t * 2f * Mathf.PI / periodoZoom));
            fundo.localScale = new Vector3(s, s, 1f);
        }

        if (titulo != null)
        {
            titulo.anchoredPosition = tituloBase + new Vector2(0f, Mathf.Sin(t * 1.6f) * amplitudeTitulo);
        }

        if (carregando)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) && painelFases != null && painelFases.activeSelf)
        {
            Voltar();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) && painelCreditos != null && painelCreditos.activeSelf)
        {
            VoltarDosCreditos();
            return;
        }

        EventSystem es = EventSystem.current;

        if (es == null)
        {
            return;
        }

        if (es.currentSelectedGameObject != null)
        {
            ultimoSelecionado = es.currentSelectedGameObject;
        }
        else if (ultimoSelecionado != null && ultimoSelecionado.activeInHierarchy && TeclaDeNavegacao())
        {
            es.SetSelectedGameObject(ultimoSelecionado);
        }
    }

    private static bool TeclaDeNavegacao()
    {
        return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)
            || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S)
            || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
    }

    public void Jogar()
    {
        if (telaHistoria != null)
        {
            if (painelPrincipal != null)
            {
                painelPrincipal.SetActive(false);
            }

            telaHistoria.Mostrar(() => CarregarFase(primeiraFase));
            return;
        }

        CarregarFase(primeiraFase);
    }

    public void AbrirFases()
    {
        if (carregando)
        {
            return;
        }

        PreencherFases();

        if (painelPrincipal != null)
        {
            painelPrincipal.SetActive(false);
        }

        if (painelFases != null)
        {
            painelFases.SetActive(true);
        }

        GameObject alvo = botaoVoltar != null ? botaoVoltar.gameObject : null;

        if (botoesFase != null)
        {
            for (int i = 0; i < botoesFase.Length; i++)
            {
                if (botoesFase[i] != null && botoesFase[i].interactable)
                {
                    alvo = botoesFase[i].gameObject;
                    break;
                }
            }
        }

        Selecionar(alvo);
    }

    public void Voltar()
    {
        if (carregando)
        {
            return;
        }

        if (painelFases != null)
        {
            painelFases.SetActive(false);
        }

        if (painelPrincipal != null)
        {
            painelPrincipal.SetActive(true);
        }

        Selecionar(botaoFases != null ? botaoFases.gameObject : null);
    }

    /// <summary>Mostra os creditos numa tela parada (sem rolagem), como o painel de fases.</summary>
    public void AbrirCreditos()
    {
        if (carregando)
        {
            return;
        }

        if (painelPrincipal != null)
        {
            painelPrincipal.SetActive(false);
        }

        if (painelCreditos != null)
        {
            painelCreditos.SetActive(true);
        }

        Selecionar(botaoVoltarCreditos != null ? botaoVoltarCreditos.gameObject : null);
    }

    public void VoltarDosCreditos()
    {
        if (carregando)
        {
            return;
        }

        if (painelCreditos != null)
        {
            painelCreditos.SetActive(false);
        }

        if (painelPrincipal != null)
        {
            painelPrincipal.SetActive(true);
        }

        Selecionar(botaoCreditos != null ? botaoCreditos.gameObject : null);
    }

    public void CarregarFase(string cena)
    {
        if (carregando || string.IsNullOrEmpty(cena))
        {
            return;
        }

        StartCoroutine(Carregar(cena));
    }

    public void Sair()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private IEnumerator FadeEntrada()
    {
        float t = 0f;

        while (t < duracaoFadeEntrada)
        {
            t += Time.unscaledDeltaTime;

            if (fade != null)
            {
                fade.alpha = 1f - Mathf.Clamp01(t / duracaoFadeEntrada);
            }

            yield return null;
        }

        if (fade != null)
        {
            fade.alpha = 0f;
            fade.blocksRaycasts = false;
        }
    }

    private IEnumerator Carregar(string cena)
    {
        carregando = true;

        if (fade != null)
        {
            fade.blocksRaycasts = true;
        }

        float t = 0f;

        while (t < duracaoFadeSaida)
        {
            t += Time.unscaledDeltaTime;

            if (fade != null)
            {
                fade.alpha = Mathf.Clamp01(t / duracaoFadeSaida);
            }

            yield return null;
        }

        SceneManager.LoadScene(cena);
    }

    private void PreencherFases()
    {
        if (botoesFase == null || textosRecorde == null)
        {
            return;
        }

        for (int i = 0; i < botoesFase.Length && i < textosRecorde.Length; i++)
        {
            string cena = prefixoFase + (i + 1);
            bool existe = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + cena + ".unity") >= 0;
            int recorde = PlayerPrefs.GetInt("Recorde_" + cena, 0);

            botoesFase[i].interactable = existe;

            if (!existe)
            {
                textosRecorde[i].text = "EM BREVE";
            }
            else if (recorde > 0)
            {
                textosRecorde[i].text = "RECORDE " + Formatar(recorde);
            }
            else
            {
                textosRecorde[i].text = "SEM RECORDE";
            }

            MenuBotaoVisual visual = botoesFase[i].GetComponent<MenuBotaoVisual>();

            if (visual != null)
            {
                visual.Atualizar();
            }
        }
    }

    private static string Formatar(int valor)
    {
        return valor.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');
    }

    private static void Selecionar(GameObject alvo)
    {
        if (alvo == null || EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(alvo);
    }
}
