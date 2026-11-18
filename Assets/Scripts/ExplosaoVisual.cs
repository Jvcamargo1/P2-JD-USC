using UnityEngine;

/// <summary>
/// Efeito visual da explosao: percorre os quadros do sprite e se destroi no fim.
/// Os quadros foram desenhados para ocupar "diametroBase" unidades na escala 1;
/// Criar() ajusta a escala ao raio real da explosao, mantendo a densidade de
/// pixels quando o raio e o de referencia.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ExplosaoVisual : MonoBehaviour
{
    [SerializeField] private Sprite[] quadros;
    [SerializeField] private float quadrosPorSegundo = 22f;
    [Tooltip("Diametro, em unidades, que o efeito ocupa na escala 1 (5 un = raio de 2,5).")]
    [SerializeField] private float diametroBase = 5f;

    private SpriteRenderer sr;
    private float tempo;

    /// <summary>
    /// Cria o efeito na posicao indicada, dimensionado para o raio da explosao.
    /// </summary>
    public static void Criar(ExplosaoVisual prefab, Vector3 posicao, float raio)
    {
        if (prefab == null)
        {
            return;
        }

        ExplosaoVisual efeito = Instantiate(prefab, posicao, Quaternion.identity);
        efeito.transform.localScale = Vector3.one * ((raio * 2f) / efeito.diametroBase);
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        if (quadros != null && quadros.Length > 0)
        {
            sr.sprite = quadros[0];
        }
    }

    private void Update()
    {
        tempo += Time.deltaTime;
        int indice = Mathf.FloorToInt(tempo * quadrosPorSegundo);

        if (quadros == null || indice >= quadros.Length)
        {
            Destroy(gameObject);
            return;
        }

        sr.sprite = quadros[indice];
    }
}
