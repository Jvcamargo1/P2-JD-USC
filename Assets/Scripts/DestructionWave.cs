using UnityEngine;

/// <summary>
/// Onda de destruicao que avança da esquerda para a direita.
/// Derruba os segmentos de chao que alcança e mata o jogador ao toca-lo.
/// </summary>
public class DestructionWave : MonoBehaviour
{
    [Header("Alcance")]
    [Tooltip("Largura da area que derruba segmentos a frente da onda.")]
    [SerializeField] private float larguraDeteccao = 2f;
    [Tooltip("Altura da area de deteccao, deve cobrir toda a vertical jogavel.")]
    [SerializeField] private float alturaDeteccao = 40f;
    [SerializeField] private LayerMask camadaChao;

    [Header("Limite de atraso")]
    [Tooltip("Distancia maxima que a onda pode ficar atras do jogador.")]
    [SerializeField] private float distanciaMaximaDoPlayer = 22f;
    [SerializeField] private Transform player;

    [Header("Direcao")]
    [Tooltip("Fase em torre: a onda sobe em vez de avancar para a direita. Gire o objeto 90 graus para o visual acompanhar.")]
    [SerializeField] private bool modoVertical;

    private readonly Collider2D[] resultados = new Collider2D[32];

    private void Update()
    {
        if (GameManager.Instance == null ||
            GameManager.Instance.Estado != GameManager.GameState.Playing)
        {
            return;
        }

        Avancar();
        DerrubarSegmentos();
    }

    private void Avancar()
    {
        float velocidade = GameManager.Instance.VelocidadeOnda;
        if (modoVertical)
        {
            transform.Translate(Vector3.up * velocidade * Time.deltaTime, Space.World);
        }
        else
        {
            transform.Translate(Vector3.right * velocidade * Time.deltaTime);
        }

        // Se o jogador abrir muito a frente, a onda acompanha para nao sumir da tela.
        if (player != null)
        {
            if (modoVertical)
            {
                float limiteY = player.position.y - distanciaMaximaDoPlayer;
                if (transform.position.y < limiteY)
                {
                    transform.position = new Vector3(
                        transform.position.x, limiteY, transform.position.z);
                }
            }
            else
            {
                float limite = player.position.x - distanciaMaximaDoPlayer;
                if (transform.position.x < limite)
                {
                    transform.position = new Vector3(
                        limite, transform.position.y, transform.position.z);
                }
            }
        }
    }

    private void DerrubarSegmentos()
    {
        Vector2 centro = new Vector2(
            transform.position.x + (larguraDeteccao * 0.5f),
            transform.position.y);

        Vector2 tamanho = new Vector2(larguraDeteccao, alturaDeteccao);

        if (modoVertical)
        {
            centro = new Vector2(transform.position.x, transform.position.y + (larguraDeteccao * 0.5f));
            tamanho = new Vector2(alturaDeteccao, larguraDeteccao);
        }

        ContactFilter2D filtro = new ContactFilter2D();
        filtro.SetLayerMask(camadaChao);
        filtro.useLayerMask = true;
        filtro.useTriggers = false;

        int quantidade = Physics2D.OverlapBox(
            centro, tamanho, 0f, filtro, resultados);

        for (int i = 0; i < quantidade; i++)
        {
            PlatformSegment segmento =
                resultados[i].GetComponentInParent<PlatformSegment>();

            if (segmento != null && !segmento.Caindo)
            {
                segmento.Desabar();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController jogador = other.GetComponent<PlayerController>();

        if (jogador != null)
        {
            jogador.Morrer();
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.35f);
        Vector3 centro = new Vector3(
            transform.position.x + (larguraDeteccao * 0.5f),
            transform.position.y,
            transform.position.z);

        if (modoVertical)
        {
            centro = new Vector3(transform.position.x, transform.position.y + (larguraDeteccao * 0.5f), transform.position.z);
            Gizmos.DrawCube(centro, new Vector3(alturaDeteccao, larguraDeteccao, 1f));
            return;
        }

        Gizmos.DrawCube(centro, new Vector3(larguraDeteccao, alturaDeteccao, 1f));
    }
}
