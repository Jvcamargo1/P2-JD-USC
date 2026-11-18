using UnityEngine;

/// <summary>
/// A Maca Dourada Gigante da sala final: flutua, gira (mesmos quadros da maca dourada comum,
/// em tamanho grande) e, quando o jogador toca nela, dispara o final do jogo.
/// </summary>
public class MacaDouradaGigante : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Sprite[] quadros;
    [SerializeField] private float quadrosPorSegundo = 10f;
    [Tooltip("Altura da flutuacao, em unidades.")]
    [SerializeField] private float amplitude = 0.25f;
    [Tooltip("Segundos para subir e descer uma vez.")]
    [SerializeField] private float periodo = 2.2f;
    [SerializeField] private FinalJogo final;

    private Vector3 posicaoBase;
    private bool pega;

    private void Start()
    {
        if (visual != null)
        {
            posicaoBase = visual.transform.localPosition;
        }
    }

    private void Update()
    {
        if (visual == null)
        {
            return;
        }

        if (quadros != null && quadros.Length > 0)
        {
            visual.sprite = quadros[Mathf.FloorToInt(Time.time * quadrosPorSegundo) % quadros.Length];
        }

        visual.transform.localPosition = posicaoBase
            + (Vector3.up * Mathf.Sin(Time.time * 2f * Mathf.PI / periodo) * amplitude);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (pega || other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        pega = true;

        if (final == null)
        {
            final = FindFirstObjectByType<FinalJogo>(FindObjectsInactive.Include);
        }

        if (final != null)
        {
            final.Iniciar();
        }
    }
}
