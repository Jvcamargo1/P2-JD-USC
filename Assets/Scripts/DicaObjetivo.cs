using UnityEngine;

/// <summary>
/// Frase de objetivo mostrada no centro da tela ao inicio de CADA fase (sem a trava de "so uma vez"
/// do DicaControles). Fica visivel por "duracao" segundos e depois some com um fade de "duracaoFade".
///
/// Usa o tempo do jogo (nao o real): com o jogo pausado, o relogio da dica tambem para.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class DicaObjetivo : MonoBehaviour
{
    [Tooltip("Segundos com a frase totalmente visivel.")]
    [SerializeField] private float duracao = 4f;
    [Tooltip("Segundos do fade ate sumir.")]
    [SerializeField] private float duracaoFade = 1f;

    private CanvasGroup grupo;
    private float tempo;

    private void Awake()
    {
        grupo = GetComponent<CanvasGroup>();
        grupo.interactable = false;
        grupo.blocksRaycasts = false;
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
