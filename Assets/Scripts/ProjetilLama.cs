using UnityEngine;

/// <summary>
/// Bola de lama disparada pelo poder do jogador. Voa reto girando, acerta
/// inimigos (dano) e caixas (conta uma batida), e se desfaz em respingos ao
/// bater no chao, numa parede ou ao atingir o alcance maximo.
///
/// Procura alvos por sobreposicao a cada quadro, em vez de usar um colisor
/// fisico: o corpo dos inimigos so colide com o chao, entao um colisor comum
/// atravessaria os inimigos sem acerta-los.
/// </summary>
public class ProjetilLama : MonoBehaviour
{
    [SerializeField] private float velocidade = 12f;
    [Tooltip("Distancia maxima percorrida, em unidades.")]
    [SerializeField] private float alcance = 8f;
    [SerializeField] private float raio = 0.22f;
    [Tooltip("Rotacao do sprite em graus por segundo.")]
    [SerializeField] private float giro = 720f;
    [SerializeField] private LayerMask camadaChao;

    [Header("Respingo")]
    [SerializeField] private Sprite spriteRespingo;
    [SerializeField] private int pedacosRespingo = 4;

    private int direcao = 1;
    private float percorrido;
    private bool desfeito;

    /// <summary>Define a direcao do voo: 1 = direita, -1 = esquerda.</summary>
    public void Lancar(int novaDirecao)
    {
        GerenciadorDeAudio.Tocar(b => b.tiro, 0.6f);
        direcao = novaDirecao >= 0 ? 1 : -1;
    }

    private void Update()
    {
        if (desfeito)
        {
            return;
        }

        float passo = velocidade * Time.deltaTime;
        transform.position += Vector3.right * (direcao * passo);
        transform.Rotate(0f, 0f, -giro * direcao * Time.deltaTime);
        percorrido += passo;

        if (VerificarAcertos() || percorrido >= alcance)
        {
            Desfazer();
        }
    }

    /// <summary>
    /// Verifica o que a bola tocou. Retorna true se ela deve se desfazer.
    /// </summary>
    private bool VerificarAcertos()
    {
        Collider2D[] tocados = Physics2D.OverlapCircleAll(transform.position, raio);

        foreach (Collider2D c in tocados)
        {
            // Chefe da Fase 5: a bola sempre se desfaz nele; so tira vida se ele estiver tonto.
            ChefeDragao chefe = c.GetComponentInParent<ChefeDragao>();

            if (chefe != null && !chefe.Morto)
            {
                chefe.LevarTiro();
                return true;
            }

            Enemy inimigo = c.GetComponentInParent<Enemy>();

            if (inimigo != null && !inimigo.Morto)
            {
                inimigo.LevarTiro();
                return true;
            }

            Box caixa = c.GetComponentInParent<Box>();

            if (caixa != null)
            {
                caixa.ReceberTiro();
                return true;
            }
        }

        foreach (Collider2D c in tocados)
        {
            bool ehChao = (camadaChao.value & (1 << c.gameObject.layer)) != 0;

            if (ehChao && !c.isTrigger)
            {
                return true;
            }
        }

        return false;
    }

    private void Desfazer()
    {
        desfeito = true;

        if (spriteRespingo != null)
        {
            for (int i = 0; i < pedacosRespingo; i++)
            {
                var pedaco = new GameObject("RespingoLama");
                pedaco.transform.position = transform.position;
                pedaco.transform.localScale = Vector3.one * 0.4f;

                SpriteRenderer sr = pedaco.AddComponent<SpriteRenderer>();
                sr.sprite = spriteRespingo;
                sr.sortingOrder = 6;

                Rigidbody2D rb = pedaco.AddComponent<Rigidbody2D>();
                rb.gravityScale = 2.5f;
                float angulo = Random.Range(30f, 150f) * Mathf.Deg2Rad;
                rb.linearVelocity = new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo)) * Random.Range(2f, 4f);

                Destroy(pedaco, 0.45f);
            }
        }

        Destroy(gameObject);
    }
}
