using System.Collections;
using UnityEngine;

/// <summary>
/// Inimigo com fisica propria: patrulha sem cair das plataformas, persegue o
/// jogador ao avista-lo e respeita gravidade e colisao com o chao.
///
/// Pular em cima causa dano (morre apos N pisadas); tocar de lado mata o jogador.
/// Usa dois colisores: um fisico (so colide com o chao) e um gatilho que detecta
/// o jogador sem empurra-lo.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    [Header("Patrulha")]
    [SerializeField] private float velocidadePatrulha = 1.6f;
    [Tooltip("Distancia maxima a partir do ponto de patrulha antes de dar meia-volta.")]
    [SerializeField] private float distanciaPatrulha = 2.5f;

    [Header("Perseguicao")]
    [SerializeField] private float velocidadePerseguicao = 3.2f;
    [SerializeField] private float raioDeteccao = 4.5f;
    [SerializeField] private float raioPerdaAlvo = 7.2f;
    [Tooltip("Ligado: o inimigo nao se joga em buracos (mas desce degraus). Desligado: segue o jogador e cai no vazio.")]
    [SerializeField] private bool pararNaBordaAoPerseguir = true;
    [Tooltip("So comeca a perseguir se o jogador estiver em pe no mesmo nivel (mesma plataforma). Jogador no ar e ignorado.")]
    [SerializeField] private bool perseguirSoNoMesmoNivel;
    [Tooltip("Diferenca maxima de altura dos pes para valer como mesmo nivel.")]
    [SerializeField] private float toleranciaNivel = 0.8f;
    [Tooltip("Tempo sem o jogador no mesmo nivel antes de desistir da perseguicao.")]
    [SerializeField] private float tempoPerdaNivel = 0.4f;

    private PlayerController controleJogador;
    private float semNivelDesde = -1f;
    [Tooltip("Ao perseguir, desce para um nivel mais baixo se houver chao ate esta altura abaixo da borda.")]
    [SerializeField] private float alturaMaximaDescida = 3.5f;

    [Header("Terreno")]
    [SerializeField] private LayerMask camadaChao;
    [SerializeField] private float profundidadeChecagemBorda = 0.6f;
    [Tooltip("Altura abaixo da qual o inimigo e removido da cena.")]
    [SerializeField] private float limiteQueda = -25f;

    [Header("Vida")]
    [Tooltip("Quantas pisadas o inimigo aguenta antes de morrer. 1 = comum, 3 = chefe.")]
    [SerializeField] private int pisadasParaMorrer = 1;
    [Tooltip("Tempo parado depois de levar uma pisada que nao mata.")]
    [SerializeField] private float tempoAtordoado = 0.5f;

    [Header("Pontuacao")]
    [Tooltip("Pontos ao ser derrotado por pisada. Comum = 200, chefe = 1000.")]
    [SerializeField] private int pontos = 200;
    [Tooltip("Texto '+pontos' mostrado ao ser derrotado (prefab Popup_Pontos).")]
    [SerializeField] private PontosFlutuantes popupPontos;

    [Header("Poder de lama")]
    [Tooltip("Bolas de lama que equivalem a uma pisada. Comum = 1, chefe = 3.")]
    [SerializeField] private int tirosPorDano = 1;

    [Header("Explosivo")]
    [Tooltip("Ligado: ao ser pisado o inimigo nao morre na hora; acende o pavio e explode. Atingido por lama, explode na hora.")]
    [SerializeField] private bool explosivo;

    private float ultimaPisada = -99f;
    [Tooltip("Segundos entre a pisada e a explosao.")]
    [SerializeField] private float tempoPavio = 1.2f;
    [Tooltip("Raio da explosao, em unidades. Mata o jogador e os inimigos dentro dele e quebra caixas.")]
    [SerializeField] private float raioExplosao = 2.5f;
    [SerializeField] private ExplosaoVisual prefabExplosao;
    [Tooltip("Cor para a qual o inimigo pisca enquanto o pavio queima.")]
    [SerializeField] private Color corPavio = new Color(1f, 0.2f, 0.15f);

    [Header("Combate")]
    [SerializeField] private float forcaRicochete = 9f;
    [SerializeField] private float invencibilidadePosPisada = 0.25f;
    [Tooltip("Margem acima do centro do inimigo que conta como pisada por cima.")]
    [SerializeField] private float margemPisada = 0.05f;

    [Header("Componentes")]
    [SerializeField] private Collider2D colisorCorpo;
    [SerializeField] private Collider2D gatilho;
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private static readonly int ParamVelocidade = Animator.StringToHash("Velocidade");
    private static readonly int ParamDano = Animator.StringToHash("Dano");

    private Rigidbody2D rb;
    private Transform player;
    private float pontoPatrulhaX;
    private float atordoadoAte;
    private int direcao = 1;
    private int vidaAtual;
    private bool perseguindo;
    private bool morto;
    private int tirosAcumulados;
    private bool armado;
    private Color corBase = Color.white;

    public bool Morto => morto;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // O corpo fisico so colide com o chao; o jogador e detectado pelo gatilho.
        if (colisorCorpo != null && camadaChao.value != 0)
        {
            colisorCorpo.excludeLayers = ~camadaChao.value;
        }

        if (gatilho != null)
        {
            gatilho.isTrigger = true;
        }
    }

    private void Start()
    {
        pontoPatrulhaX = rb.position.x;
        vidaAtual = pisadasParaMorrer;

        if (spriteRenderer != null)
        {
            corBase = spriteRenderer.color;
        }

        GameObject jogadorGO = GameObject.FindGameObjectWithTag("Player");
        if (jogadorGO != null)
        {
            player = jogadorGO.transform;
            controleJogador = jogadorGO.GetComponent<PlayerController>();
        }
    }

    private void Update()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = direcao < 0;
        }

        if (animator != null)
        {
            animator.SetFloat(ParamVelocidade, Mathf.Abs(rb.linearVelocity.x));
        }
    }

    private void FixedUpdate()
    {
        if (transform.position.y < limiteQueda)
        {
            Destroy(gameObject);
            return;
        }

        bool jogoRodando = GameManager.Instance == null ||
            GameManager.Instance.Estado == GameManager.GameState.Playing;

        bool podeAgir = !morto && player != null && jogoRodando &&
            Time.time >= atordoadoAte;

        if (!podeAgir)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        AtualizarPerseguicao();

        float velocidade = perseguindo ? velocidadePerseguicao : velocidadePatrulha;
        bool apoiado = NoChao();

        if (perseguindo)
        {
            float dx = player.position.x - rb.position.x;

            // Zona morta evita tremer de um lado pro outro quando o jogador esta em cima.
            if (Mathf.Abs(dx) > 0.1f)
            {
                direcao = dx > 0f ? 1 : -1;
            }

            // Degrau (chao logo abaixo da borda) nao bloqueia: o inimigo desce atras
            // do jogador. Buraco sem fundo continua bloqueando.
            bool bordaBloqueia = pararNaBordaAoPerseguir && !HaChaoAFrente() && !HaChaoSeguroAbaixoDaBorda();

            if (apoiado && (HaParedeAFrente() || bordaBloqueia))
            {
                velocidade = 0f;
            }
        }
        else if (apoiado)
        {
            float deslocamento = rb.position.x - pontoPatrulhaX;

            if (Mathf.Abs(deslocamento) >= distanciaPatrulha &&
                Mathf.Sign(deslocamento) == direcao)
            {
                direcao = -direcao;
            }
            else if (HaParedeAFrente() || !HaChaoAFrente())
            {
                direcao = -direcao;
            }
        }

        rb.linearVelocity = new Vector2(direcao * velocidade, rb.linearVelocity.y);
    }

    private void AtualizarPerseguicao()
    {
        float distancia = Vector2.Distance(rb.position, player.position);
        bool alvoValido = !perseguirSoNoMesmoNivel || JogadorNoMesmoNivel();

        if (!perseguindo && distancia <= raioDeteccao && alvoValido)
        {
            perseguindo = true;
            semNivelDesde = -1f;
        }
        else if (perseguindo)
        {
            bool desistir = distancia > raioPerdaAlvo;

            if (!desistir && perseguirSoNoMesmoNivel)
            {
                if (alvoValido)
                {
                    semNivelDesde = -1f;
                }
                else
                {
                    if (semNivelDesde < 0f)
                    {
                        semNivelDesde = Time.time;
                    }

                    desistir = (Time.time - semNivelDesde) >= tempoPerdaNivel;
                }
            }

            if (desistir)
            {
                perseguindo = false;
                semNivelDesde = -1f;
                pontoPatrulhaX = rb.position.x;
            }
        }
    }

    /// <summary>
    /// Jogador em pe no chao e com os pes na mesma altura dos do inimigo (mesma plataforma).
    /// Um jogador no meio do pulo nao conta.
    /// </summary>
    private bool JogadorNoMesmoNivel()
    {
        if (controleJogador == null || !controleJogador.NoChao)
        {
            return false;
        }

        Collider2D colJogador = controleJogador.GetComponent<Collider2D>();
        float pesJogador = colJogador != null ? colJogador.bounds.min.y : player.position.y;

        return Mathf.Abs(pesJogador - colisorCorpo.bounds.min.y) <= toleranciaNivel;
    }

    private bool NoChao()
    {
        Bounds b = colisorCorpo.bounds;
        Vector2 origem = new Vector2(b.center.x, b.min.y + 0.05f);
        return Physics2D.Raycast(origem, Vector2.down, 0.2f, camadaChao).collider != null;
    }

    private bool HaChaoAFrente()
    {
        Bounds b = colisorCorpo.bounds;
        Vector2 origem = new Vector2(
            b.center.x + (direcao * (b.extents.x + 0.1f)),
            b.min.y + 0.05f);

        return Physics2D.Raycast(
            origem, Vector2.down, profundidadeChecagemBorda, camadaChao).collider != null;
    }

    /// <summary>
    /// Procura chao alem da borda, ate alturaMaximaDescida abaixo dos pes. O raio
    /// parte um pouco depois da quina para nao acertar o proprio degrau.
    /// </summary>
    private bool HaChaoSeguroAbaixoDaBorda()
    {
        Bounds b = colisorCorpo.bounds;
        Vector2 origem = new Vector2(
            b.center.x + (direcao * (b.extents.x + 0.35f)),
            b.min.y + 0.05f);

        return Physics2D.Raycast(
            origem, Vector2.down, alturaMaximaDescida, camadaChao).collider != null;
    }

    private bool HaParedeAFrente()
    {
        Bounds b = colisorCorpo.bounds;
        Vector2 origem = new Vector2(b.center.x, b.min.y + (b.size.y * 0.3f));

        return Physics2D.Raycast(
            origem, Vector2.right * direcao, b.extents.x + 0.12f, camadaChao).collider != null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (morto)
        {
            return;
        }

        PlayerController jogador = other.GetComponent<PlayerController>();
        if (jogador == null)
        {
            return;
        }

        Rigidbody2D rbJogador = other.attachedRigidbody;

        bool caindoDeCima = rbJogador != null &&
            rbJogador.linearVelocity.y <= 0.1f &&
            other.bounds.min.y >= gatilho.bounds.center.y + margemPisada;

        if (caindoDeCima)
        {
            LevarPisada(jogador, rbJogador);
        }
        else
        {
            jogador.Morrer();
        }
    }

    private void LevarPisada(PlayerController jogador, Rigidbody2D rbJogador)
    {
        vidaAtual--;
        GerenciadorDeAudio.TocarEm(transform.position, b => b.pisada);
        ultimaPisada = Time.time;

        if (rbJogador != null)
        {
            rbJogador.linearVelocity = new Vector2(rbJogador.linearVelocity.x, forcaRicochete);
        }

        jogador.ConcederInvencibilidade(invencibilidadePosPisada);

        if (animator != null)
        {
            animator.SetTrigger(ParamDano);
        }

        if (vidaAtual <= 0)
        {
            // Explosivo: a pisada nao mata na hora, acende o pavio.
            if (explosivo)
            {
                Armar();
            }
            else
            {
                Morrer();
            }
        }
        else
        {
            atordoadoAte = Time.time + tempoAtordoado;
        }
    }

    /// <summary>
    /// Recebe uma bola de lama do poder do jogador. A cada tirosPorDano acertos
    /// conta como uma pisada (o chefe precisa de mais acertos).
    /// </summary>
    public void LevarTiro()
    {
        if (morto)
        {
            return;
        }

        tirosAcumulados++;

        if (tirosAcumulados < tirosPorDano)
        {
            return;
        }

        tirosAcumulados = 0;
        vidaAtual--;

        if (animator != null)
        {
            animator.SetTrigger(ParamDano);
        }

        if (vidaAtual <= 0)
        {
            // Explosivo atingido de longe explode na hora (o jeito seguro de derrota-lo).
            if (explosivo)
            {
                ExplodirAgora();
            }
            else
            {
                Morrer();
            }
        }
        else
        {
            atordoadoAte = Time.time + tempoAtordoado;
        }
    }

    private void Morrer()
    {
        morto = true;

        // Se acabou de levar uma pisada, o som ja saiu ali: nao repete.
        if (Time.time - ultimaPisada > 0.15f)
        {
            GerenciadorDeAudio.TocarEm(transform.position, b => b.pisada);
        }
        gatilho.enabled = false;
        rb.linearVelocity = Vector2.zero;
        RegistrarDerrota();

        // Deixa a animacao de dano terminar antes de remover.
        Destroy(gameObject, 0.5f);
    }

    /// <summary>Pontos e texto flutuante da derrota (pisada, lama ou explosao).</summary>
    private void RegistrarDerrota()
    {
        // Mais de uma pisada para morrer identifica o chefe.
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.InimigoDerrotado(pontos, pisadasParaMorrer > 1);
        }

        PontosFlutuantes.Mostrar(popupPontos, transform.position + (Vector3.up * 1.1f), pontos, new Color(1f, 0.62f, 0.42f));
    }

    /// <summary>Atingido pela explosao de outro inimigo explosivo.</summary>
    public void MorrerPorExplosao()
    {
        if (morto)
        {
            return;
        }

        Morrer();
    }

    /// <summary>
    /// Pisada num explosivo: fica parado e inofensivo, pisca cada vez mais rapido
    /// e explode quando o pavio acaba. Os pontos saem na pisada.
    /// </summary>
    private void Armar()
    {
        if (armado)
        {
            return;
        }

        armado = true;
        morto = true;
        gatilho.enabled = false;
        rb.linearVelocity = Vector2.zero;
        RegistrarDerrota();
        StartCoroutine(RotinaPavio());
    }

    private IEnumerator RotinaPavio()
    {
        Vector3 escalaBase = transform.localScale;
        float tempo = 0f;

        while (tempo < tempoPavio)
        {
            tempo += Time.deltaTime;
            float avanco = Mathf.Clamp01(tempo / tempoPavio);
            float frequencia = Mathf.Lerp(4f, 16f, avanco);
            float pulso = (Mathf.Sin(tempo * frequencia * Mathf.PI * 2f) + 1f) * 0.5f;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.Lerp(corBase, corPavio, pulso);
            }

            transform.localScale = escalaBase * (1f + (0.12f * avanco * pulso));
            yield return null;
        }

        Explodir();
    }

    /// <summary>Atingido por lama: explode sem esperar o pavio.</summary>
    private void ExplodirAgora()
    {
        if (armado)
        {
            return;
        }

        armado = true;
        morto = true;
        gatilho.enabled = false;
        rb.linearVelocity = Vector2.zero;
        RegistrarDerrota();
        Explodir();
    }

    /// <summary>
    /// Explode: mata o jogador e os outros inimigos dentro do raio e quebra as
    /// caixas. Fora do estado de jogo (chegada, vitoria, game over) nao machuca
    /// o jogador, para nao estragar a sequencia final.
    /// </summary>
    private void Explodir()
    {
        GerenciadorDeAudio.TocarEm(transform.position, b => b.explosao, 0.8f);
        Vector2 centro = transform.position + (Vector3.up * 0.4f);
        ExplosaoVisual.Criar(prefabExplosao, centro, raioExplosao);

        bool jogoRodando = GameManager.Instance == null ||
            GameManager.Instance.Estado == GameManager.GameState.Playing;

        foreach (Collider2D c in Physics2D.OverlapCircleAll(centro, raioExplosao))
        {
            PlayerController jogador = c.GetComponent<PlayerController>();

            if (jogador != null)
            {
                if (jogoRodando)
                {
                    jogador.Morrer();
                }

                continue;
            }

            Box caixa = c.GetComponentInParent<Box>();

            if (caixa != null)
            {
                caixa.ReceberExplosao();
                continue;
            }

            Enemy outro = c.GetComponentInParent<Enemy>();

            if (outro != null && outro != this)
            {
                outro.MorrerPorExplosao();
            }
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, raioDeteccao);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, raioPerdaAlvo);
    }
}
