using UnityEngine;

/// <summary>
/// Painel de controles mostrado no inicio da primeira partida da sessao. Fica
/// visivel por alguns segundos e depois some com um fade. Nas partidas
/// seguintes (reiniciar, proxima fase) nem aparece, para nao incomodar.
///
/// Usa o tempo do jogo (nao o real): com o jogo pausado, o relogio da dica
/// tambem para.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class DicaControles : MonoBehaviour
{
    [Tooltip("Segundos com o painel totalmente visivel.")]
    [SerializeField] private float duracao = 4f;
    [Tooltip("Segundos do fade ate sumir.")]
    [SerializeField] private float duracaoFade = 1f;

    private static bool jaMostrada;

    private CanvasGroup grupo;
    private float tempo;

    // Zera ao entrar no Play Mode, mesmo com "Reload Domain" desligado.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Resetar()
    {
        jaMostrada = false;
    }

    private void Awake()
    {
        grupo = GetComponent<CanvasGroup>();
        grupo.interactable = false;
        grupo.blocksRaycasts = false;

        if (jaMostrada)
        {
            gameObject.SetActive(false);
            return;
        }

        jaMostrada = true;
    }

    private void Update()
    {
        tempo += Time.deltaTime;

        // 1 durante "duracao" e depois cai linearmente ate 0 no fade.
        float restante = duracao + duracaoFade - tempo;
        grupo.alpha = Mathf.Clamp01(restante / duracaoFade);

        if (restante <= 0f)
        {
            gameObject.SetActive(false);
        }
    }
}
