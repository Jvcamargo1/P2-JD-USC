using UnityEngine;

/// <summary>
/// Caixa quebravel com frutas dentro, no estilo Mario.
///
/// Conta uma batida quando o jogador bate a cabeca por baixo ou cai em cima
/// dela com velocidade (nesse caso ele quica). Aguenta algumas batidas,
/// conforme o tipo, e ao quebrar espalha os pedacos e solta as frutas, que
/// saltam para fora e ficam flutuando para o jogador coletar.
/// As frutas da caixa entram no total da fase desde o inicio.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class Box : MonoBehaviour
{
    [Header("Resistencia e conteudo")]
    [Tooltip("Quantas batidas a caixa aguenta antes de quebrar.")]
    [SerializeField] private int batidasParaQuebrar = 1;
    [Tooltip("Quantas frutas a caixa solta ao quebrar.")]
    [SerializeField] private int frutas = 2;
    [SerializeField] private GameObject prefabFruta;

    [Header("Batidas")]
    [Tooltip("Velocidade minima de impacto para cair em cima contar como batida.")]
    [SerializeField] private float velocidadeMinimaPorCima = 2f;
    [Tooltip("Velocidade vertical dada ao jogador ao quebrar a caixa caindo em cima.")]
    [SerializeField] private float ricocheteAoPisar = 9f;
    [Tooltip("Intervalo minimo entre batidas, para um contato nao contar duas vezes.")]
    [SerializeField] private float intervaloEntreBatidas = 0.15f;

    [Header("Visual")]
    [SerializeField] private Animator animator;
    [Tooltip("Pedacos que voam ao quebrar (quadros da animacao Break).")]
    [SerializeField] private Sprite[] pedacos;
    [SerializeField] private float forcaPedacos = 4f;

    private static readonly int ParamBatida = Animator.StringToHash("Batida");

    private int batidasRestantes;
    private float proximaBatidaPermitida;
    private bool quebrada;

    private void Awake()
    {
        batidasRestantes = Mathf.Max(1, batidasParaQuebrar);
    }

    private void Start()
    {
        if (ScoreManager.Instance != null)
        {
            if (ContemMacaDourada())
            {
                ScoreManager.Instance.RegistrarMacaDouradaExtra(frutas);
            }
            else
            {
                ScoreManager.Instance.RegistrarFrutasExtras(frutas);
            }
        }
    }

    /// <summary>Esta caixa guarda uma (ou mais) maca dourada em vez de fruta comum.</summary>
    private bool ContemMacaDourada()
    {
        return prefabFruta != null && prefabFruta.GetComponent<MacaDourada>() != null;
    }

    private void OnCollisionEnter2D(Collision2D colisao)
    {
        if (quebrada || Time.time < proximaBatidaPermitida)
        {
            return;
        }

        if (colisao.collider.GetComponent<PlayerController>() == null)
        {
            return;
        }

        // A normal aponta do outro corpo para esta caixa (mesma convencao
        // usada em MovingFallingPlatform): y > 0 = jogador por baixo.
        bool porBaixo = false;
        bool porCima = false;

        foreach (ContactPoint2D contato in colisao.contacts)
        {
            porBaixo |= contato.normal.y > 0.5f;
            porCima |= contato.normal.y < -0.5f;
        }

        bool impactoForte = Mathf.Abs(colisao.relativeVelocity.y) >= velocidadeMinimaPorCima;

        if (!porBaixo && !(porCima && impactoForte))
        {
            return;
        }

        if (porCima && colisao.rigidbody != null)
        {
            Rigidbody2D rbJogador = colisao.rigidbody;
            rbJogador.linearVelocity = new Vector2(rbJogador.linearVelocity.x, ricocheteAoPisar);
        }

        Bater();
    }

    /// <summary>
    /// Uma bola de lama do poder conta como uma batida.
    /// </summary>
    public void ReceberTiro()
    {
        if (quebrada || Time.time < proximaBatidaPermitida)
        {
            return;
        }

        Bater();
    }

    /// <summary>
    /// A explosao de um inimigo explosivo quebra a caixa de uma vez, qualquer que seja a resistencia.
    /// </summary>
    public void ReceberExplosao()
    {
        if (quebrada)
        {
            return;
        }

        Quebrar();
    }

    private void Bater()
    {
        GerenciadorDeAudio.TocarEm(transform.position, b => b.batida, 0.7f);
        proximaBatidaPermitida = Time.time + intervaloEntreBatidas;
        batidasRestantes--;

        if (batidasRestantes > 0)
        {
            if (animator != null)
            {
                animator.SetTrigger(ParamBatida);
            }

            return;
        }

        Quebrar();
    }

    private void Quebrar()
    {
        GerenciadorDeAudio.TocarEm(transform.position, b => b.quebra, 0.65f);
        quebrada = true;
        GetComponent<BoxCollider2D>().enabled = false;
        SoltarFrutas();
        EspalharPedacos();
        Destroy(gameObject);
    }

    private void SoltarFrutas()
    {
        if (prefabFruta == null)
        {
            return;
        }

        for (int i = 0; i < frutas; i++)
        {
            // Leque acima da caixa, em duas fileiras quando ha muitas frutas.
            int porFileira = Mathf.Min(frutas, 3);
            int fileira = i / porFileira;
            int coluna = i % porFileira;
            int naFileira = Mathf.Min(porFileira, frutas - (fileira * porFileira));
            float x = (coluna - ((naFileira - 1) * 0.5f)) * 0.7f;
            float y = 1.1f + (fileira * 0.7f);

            GameObject fruta = Instantiate(prefabFruta, transform.position, Quaternion.identity);
            Vector3 destino = transform.position + new Vector3(x, y, 0f);
            Collectible coletavel = fruta.GetComponent<Collectible>();
            MacaDourada dourada = fruta.GetComponent<MacaDourada>();

            if (coletavel != null)
            {
                coletavel.Surgir(destino, 0.3f);
            }
            else if (dourada != null)
            {
                dourada.Surgir(destino, 0.3f);
            }
        }
    }

    private void EspalharPedacos()
    {
        if (pedacos == null)
        {
            return;
        }

        for (int i = 0; i < pedacos.Length; i++)
        {
            var pedaco = new GameObject("PedacoCaixa");
            pedaco.transform.position = transform.position;

            SpriteRenderer sr = pedaco.AddComponent<SpriteRenderer>();
            sr.sprite = pedacos[i];
            sr.sortingOrder = 4;

            // Sem colisor: os pedacos so voam e caem, sem atrapalhar o jogo.
            Rigidbody2D rb = pedaco.AddComponent<Rigidbody2D>();
            rb.gravityScale = 2.5f;
            float lado = (i % 2 == 0) ? -1f : 1f;
            rb.linearVelocity = new Vector2(lado * Random.Range(1.5f, forcaPedacos), Random.Range(3f, 6f));
            rb.angularVelocity = Random.Range(-360f, 360f);

            Destroy(pedaco, 1.2f);
        }
    }
}
