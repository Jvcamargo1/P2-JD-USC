using UnityEngine;

/// <summary>
/// Movimentacao do jogador: esquerda/direita, pulo e troca de animacoes.
/// Usa Rigidbody2D para fisica e um ponto de checagem de chao.
/// Morre ao cair no vazio abaixo do limite de queda.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimentacao")]
    [SerializeField] private float velocidade = 7f;
    [SerializeField] private float forcaPulo = 13f;

    [Header("Checagem de chao")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float raioGroundCheck = 0.15f;
    [SerializeField] private LayerMask camadaChao;

    [Header("Ajuste de pulo")]
    [Tooltip("Tempo apos sair da plataforma em que o pulo ainda e aceito.")]
    [SerializeField] private float coyoteTime = 0.12f;
    [Tooltip("Tempo de antecedencia em que o pulo fica guardado antes de tocar o chao.")]
    [SerializeField] private float jumpBuffer = 0.12f;
    [Tooltip("Multiplicador de gravidade na descida, deixa o pulo menos flutuante.")]
    [SerializeField] private float multiplicadorQueda = 2.2f;

    [Header("Queda no vazio")]
    [Tooltip("Altura abaixo da qual o jogador morre. Deve ficar abaixo de qualquer plataforma da fase.")]
    [SerializeField] private float limiteQueda = -12f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    private float inputHorizontal;
    private float contadorCoyote;
    private float contadorBuffer;
    private bool noChao;

    /// <summary>O jogador esta em pe sobre o chao (usado pelos inimigos da torre).</summary>
    public bool NoChao => noChao;
    private bool morto;
    private float invencivelAteTempo;
    private float bloqueioChao;

    // Modo trapaca (Konami): ultimo chao firme pisado, para reaparecer apos uma queda.
    private Vector2 ultimoChaoSeguro;
    private bool temChaoSeguro;

    // Caminhada automatica da sequencia de chegada (ate a porta da casa).
    [Tooltip("Fracao da velocidade normal usada na caminhada automatica da chegada.")]
    [SerializeField] private float fatorCaminhadaAutomatica = 0.6f;
    private bool caminhadaAutomatica;
    private float alvoCaminhada;
    private System.Action aoChegarCaminhada;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (!morto && transform.position.y < limiteQueda)
        {
            MorrerPorQueda();
            return;
        }

        if (caminhadaAutomatica)
        {
            AtualizarCaminhadaAutomatica();
            return;
        }

        if (morto || GameManager.Instance == null ||
            GameManager.Instance.Estado != GameManager.GameState.Playing)
        {
            inputHorizontal = 0f;
            return;
        }

        inputHorizontal = Input.GetAxisRaw("Horizontal");

        if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.W) ||
            Input.GetKeyDown(KeyCode.UpArrow))
        {
            contadorBuffer = jumpBuffer;
        }
        else
        {
            contadorBuffer -= Time.deltaTime;
        }

        AtualizarAnimacao();
        AtualizarDirecaoSprite();
    }

    private void FixedUpdate()
    {
        if (morto)
        {
            return;
        }

        noChao = Physics2D.OverlapCircle(
            groundCheck.position, raioGroundCheck, camadaChao);

        // Logo apos um lancamento (trampolim), ignora o chao por um instante: o
        // coyote time nao reabre um pulo comum que sobrescreveria o impulso.
        if (bloqueioChao > 0f)
        {
            bloqueioChao -= Time.fixedDeltaTime;
            noChao = false;
        }

        if (noChao)
        {
            contadorCoyote = coyoteTime;
            RegistrarChaoSeguro();
        }
        else
        {
            contadorCoyote -= Time.fixedDeltaTime;
        }

        rb.linearVelocity = new Vector2(
            inputHorizontal * velocidade, rb.linearVelocity.y);

        if (contadorBuffer > 0f && contadorCoyote > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, forcaPulo);
            GerenciadorDeAudio.Tocar(b => b.pulo, 0.45f);
            contadorBuffer = 0f;
            contadorCoyote = 0f;
        }

        if (rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y *
                (multiplicadorQueda - 1f) * Time.fixedDeltaTime;
        }
    }

    private void AtualizarDirecaoSprite()
    {
        if (spriteRenderer == null || Mathf.Approximately(inputHorizontal, 0f))
        {
            return;
        }

        spriteRenderer.flipX = inputHorizontal < 0f;
    }

    private void AtualizarAnimacao()
    {
        if (animator == null)
        {
            return;
        }

        animator.SetFloat("VelocidadeX", Mathf.Abs(inputHorizontal));
        animator.SetFloat("VelocidadeY", rb.linearVelocity.y);
        animator.SetBool("NoChao", noChao);
    }

    /// <summary>
    /// Chamado por obstaculos, inimigos (toque lateral) e pela onda de destruicao.
    /// Ignorado durante a janela de invencibilidade pos-pisada num inimigo.
    /// </summary>
    public void Morrer()
    {
        if (morto || CheatKonami.Ativo || Time.time < invencivelAteTempo)
        {
            return;
        }

        EncerrarVida();
    }

    /// <summary>
    /// Queda no vazio e sempre fatal, mesmo durante a invencibilidade.
    /// </summary>
    private void MorrerPorQueda()
    {
        if (morto)
        {
            return;
        }

        // Modo trapaca: a queda nao mata, o jogador volta para um chao firme.
        if (CheatKonami.Ativo)
        {
            ReaparecerNoChaoSeguro();
            return;
        }

        EncerrarVida();
    }

    private void EncerrarVida()
    {
        morto = true;
        GerenciadorDeAudio.Tocar(b => b.morte, 0.8f);
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;

        if (animator != null)
        {
            animator.SetTrigger("Morreu");
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.FinalizarJogo();
        }
    }

    /// <summary>
    /// Faz o jogador andar sozinho ate a posicao x indicada (sequencia de chegada)
    /// e avisa ao chegar. Os controles sao ignorados durante a caminhada.
    /// </summary>
    public void CaminharAte(float x, System.Action aoChegar)
    {
        caminhadaAutomatica = true;
        alvoCaminhada = x;
        aoChegarCaminhada = aoChegar;
        contadorBuffer = 0f;
    }

    private void AtualizarCaminhadaAutomatica()
    {
        float dx = alvoCaminhada - transform.position.x;

        if (Mathf.Abs(dx) <= 0.08f)
        {
            caminhadaAutomatica = false;
            inputHorizontal = 0f;
            AtualizarAnimacao();

            System.Action aviso = aoChegarCaminhada;
            aoChegarCaminhada = null;
            aviso?.Invoke();
            return;
        }

        inputHorizontal = Mathf.Sign(dx) * fatorCaminhadaAutomatica;
        AtualizarAnimacao();
        AtualizarDirecaoSprite();
    }

    /// <summary>
    /// Lanca o jogador para cima (usado pelo trampolim). Zera o jump buffer e o
    /// coyote time para que um pulo comum nao substitua o lancamento.
    /// </summary>
    public void Lancar(float velocidadeVertical)
    {
        if (morto)
        {
            return;
        }

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, velocidadeVertical);
        contadorBuffer = 0f;
        contadorCoyote = 0f;
        bloqueioChao = 0.15f;
    }

    /// <summary>
    /// Guarda a posicao do ultimo chao firme (que nao esteja desabando) para o
    /// modo trapaca reaparecer ali depois de uma queda.
    /// </summary>
    private void RegistrarChaoSeguro()
    {
        Collider2D chao = Physics2D.OverlapCircle(groundCheck.position, raioGroundCheck, camadaChao);

        if (chao == null || EstaDesabando(chao))
        {
            return;
        }

        ultimoChaoSeguro = rb.position;
        temChaoSeguro = true;
    }

    /// <summary>
    /// Procura, na coluna x, a superficie onde o jogador pode ficar em pe: trecho livre em cima e
    /// solido embaixo, sem estar desabando e com espaco para o jogador. Entre as candidatas escolhe
    /// a mais proxima da altura de referencia.
    ///
    /// Varre pontos com OverlapPoint em vez de usar raios: um raio so reporta UM acerto por colisor,
    /// e o chao e o teto de uma caverna fazem parte do mesmo colisor composto do chunk, entao o raio
    /// so enxergaria o topo do teto.
    /// </summary>
    private bool BuscarSuperficieDeApoio(float x, float alturaReferencia, out float y)
    {
        const float passo = 0.25f;
        y = 0f;
        bool achou = false;
        float melhorDistancia = float.MaxValue;

        float topo = alturaReferencia + 14f;
        float baixo = Mathf.Max(alturaReferencia - 14f, limiteQueda + 3f);

        for (float yy = topo; yy > baixo; yy -= passo)
        {
            if (Physics2D.OverlapPoint(new Vector2(x, yy), camadaChao) != null)
            {
                continue;
            }

            // ponto livre: ha solido logo abaixo?
            RaycastHit2D chao = Physics2D.Raycast(new Vector2(x, yy), Vector2.down, passo + 0.05f, camadaChao);

            if (chao.collider == null || EstaDesabando(chao.collider))
            {
                continue;
            }

            // precisa caber o jogador em pe: nada solido acima da superficie
            bool livre = true;

            for (float h = 0.3f; h <= 1.6f; h += 0.3f)
            {
                if (Physics2D.OverlapPoint(new Vector2(x, chao.point.y + h), camadaChao) != null)
                {
                    livre = false;
                    break;
                }
            }

            if (!livre)
            {
                continue;
            }

            float distancia = Mathf.Abs(chao.point.y - alturaReferencia);

            if (distancia < melhorDistancia)
            {
                melhorDistancia = distancia;
                y = chao.point.y;
                achou = true;
            }
        }

        return achou;
    }

    private static bool EstaDesabando(Collider2D chao)
    {
        PlatformSegment segmento = chao.GetComponentInParent<PlatformSegment>();
        return segmento != null && segmento.Caindo;
    }

    /// <summary>
    /// Modo trapaca: em vez de morrer na queda, volta ao ultimo chao firme. Se esse
    /// chao foi destruido pela onda, ficou atras dela ou da camera, ou nao existe
    /// mais, reaparece sobre o primeiro chao encontrado logo adiante.
    /// </summary>
    private void ReaparecerNoChaoSeguro()
    {
        Vector2 origem = temChaoSeguro ? ultimoChaoSeguro : rb.position;
        float xMinimo = origem.x;

        if (Camera.main != null)
        {
            xMinimo = Mathf.Max(xMinimo, Camera.main.transform.position.x - 5f);
        }

        DestructionWave onda = FindFirstObjectByType<DestructionWave>();

        if (onda != null)
        {
            xMinimo = Mathf.Max(xMinimo, onda.transform.position.x + 3f);
        }

        RaycastHit2D abaixo = Physics2D.Raycast(origem + (Vector2.up * 0.5f), Vector2.down, 1.5f, camadaChao);
        bool origemValida = temChaoSeguro && xMinimo <= origem.x &&
            abaixo.collider != null && !EstaDesabando(abaixo.collider);

        Vector2 destino = origem;

        if (!origemValida)
        {
            // Altura de referencia: onde o jogador estava de pe pela ultima vez. Em cavernas o
            // primeiro colisor visto de cima e o TOPO DO TETO, entao escolhe-se a superficie mais
            // proxima dessa altura (e com espaco livre em cima), em vez da mais alta.
            float alturaReferencia = temChaoSeguro ? ultimoChaoSeguro.y : rb.position.y;

            for (int i = 0; i < 60; i++)
            {
                float x = xMinimo + i;

                if (!BuscarSuperficieDeApoio(x, alturaReferencia, out float ySuperficie))
                {
                    continue;
                }

                destino = new Vector2(x, ySuperficie + 0.6f);
                break;
            }
        }

        rb.position = destino;
        transform.position = destino;
        rb.linearVelocity = Vector2.zero;
        contadorBuffer = 0f;
        contadorCoyote = 0f;
        Physics2D.SyncTransforms();
    }

    /// <summary>
    /// Concede uma janela curta de invencibilidade, usada logo apos pisar
    /// em um inimigo (evita morrer no mesmo frame por toque lateral).
    /// </summary>
    public void ConcederInvencibilidade(float segundos)
    {
        invencivelAteTempo = Mathf.Max(invencivelAteTempo, Time.time + segundos);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, raioGroundCheck);
    }
}
