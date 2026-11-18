using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Chefe da Fase 5: o dragao.
///
/// Ciclo: parado (encara o jogador) -> rajada de bolas de fogo (baixas: o jogador precisa pular;
/// altas: passam por cima de quem fica no chao) -> tonto, com estrelinhas, que e a UNICA janela em
/// que ele leva dano -> volta ao ciclo.
///
/// Fora da janela de tonto, encostar nele mata o jogador (ate por cima: as costas tem espinhos).
/// Tonto, pisar em cima ou acertar lama tira 1 de vida. Depois de cada acerto ele salta para o
/// lado da arena mais longe do jogador e fica mais agressivo (mais bolas, menos intervalo).
/// Com 0 de vida morre, explode e dispara OnDerrotado (o portao da sala da maca se abre).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ChefeDragao : MonoBehaviour
{
    private enum Estado { Esperando, Andando, Parado, Atacando, Tonto, Ferido, Saltando, Morrendo, Morto }

    [Header("Vida")]
    [SerializeField] private int vidaMaxima = 3;
    [SerializeField] private int pontos = 1000;
    [SerializeField] private PontosFlutuantes popupPontos;

    [Header("Arena")]
    [Tooltip("Distancia horizontal do jogador que inicia a luta.")]
    [SerializeField] private float raioAtivacao = 13f;
    [Tooltip("X para onde ele salta depois de levar dano (usa o lado mais longe do jogador).")]
    [SerializeField] private float xEsquerda;
    [SerializeField] private float xDireita;
    [SerializeField] private float duracaoSalto = 0.9f;
    [SerializeField] private float alturaSalto = 3.5f;

    [Header("Ciclo (segundos)")]
    [SerializeField] private float tempoParado = 1.2f;
    [SerializeField] private float tempoTonto = 2.6f;
    [SerializeField] private float tempoFerido = 0.5f;

    [Header("Movimento")]
    [Tooltip("Velocidade ao caminhar ate o jogador entre as rajadas.")]
    [SerializeField] private float velocidadeAndar = 2f;
    [Tooltip("Tempo maximo caminhando antes de atacar.")]
    [SerializeField] private float tempoAndando = 2f;
    [Tooltip("Para de caminhar ao chegar a esta distancia do jogador.")]
    [SerializeField] private float distanciaMinima = 4f;

    [Header("Fogo (valores do inicio da luta)")]
    [SerializeField] private ProjetilFogo prefabFogo;
    [Tooltip("Bolas por rajada no comeco da luta.")]
    [SerializeField] private int fogosPorRajada = 4;
    [SerializeField] private float intervaloFogo = 1f;
    [Tooltip("Tempo da animacao de ataque antes de a bola sair.")]
    [SerializeField] private float preparoFogo = 0.3f;
    [SerializeField] private float velocidadeFogo = 6.5f;
    [Tooltip("Altura acima dos pes da bola baixa (o jogador precisa pular).")]
    [SerializeField] private float alturaFogoBaixo = 0.45f;
    [Tooltip("Altura acima dos pes da bola alta (passa por cima de quem fica no chao).")]
    [SerializeField] private float alturaFogoAlto = 1.75f;
    [Tooltip("Distancia horizontal da boca ate o centro do dragao.")]
    [SerializeField] private float distanciaBoca = 1.6f;

    [Header("Dificuldade (aplicada a cada vida perdida)")]
    [Tooltip("Bolas a mais na rajada por vida perdida.")]
    [SerializeField] private int fogosPorEstagio = 1;
    [Tooltip("Quanto a bola acelera (un/s) por vida perdida.")]
    [SerializeField] private float velocidadeFogoPorEstagio = 1.3f;
    [Tooltip("Teto da velocidade da bola. Acima de ~7 o jogador nao consegue mais fugir correndo.")]
    [SerializeField] private float velocidadeFogoMaxima = 10.5f;
    [Tooltip("Quanto o intervalo entre bolas encurta por vida perdida.")]
    [SerializeField] private float intervaloFogoPorEstagio = 0.11f;
    [SerializeField] private float intervaloFogoMinimo = 0.5f;
    [Tooltip("Quanto a janela de tonto encurta por vida perdida.")]
    [SerializeField] private float tempoTontoPorEstagio = 0.3f;
    [SerializeField] private float tempoTontoMinimo = 1.3f;
    [Tooltip("Quanto ele anda mais rapido por vida perdida.")]
    [SerializeField] private float velocidadeAndarPorEstagio = 0.45f;

    [Header("Animacao")]
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private float quadrosPorSegundo = 10f;
    [SerializeField] private Sprite[] quadrosParado;
    [SerializeField] private Sprite[] quadrosAndando;
    [SerializeField] private Sprite[] quadrosAtaque;
    [SerializeField] private Sprite[] quadrosAtaqueBaixo;
    [SerializeField] private Sprite[] quadrosTonto;
    [SerializeField] private Sprite[] quadrosFerido;
    [SerializeField] private Sprite[] quadrosSalto;
    [SerializeField] private Sprite[] quadrosMorte;
    [SerializeField] private Sprite[] quadrosExplosao;
    [SerializeField] private Color corDano = new Color(1f, 0.45f, 0.45f);

    [Header("Componentes")]
    [SerializeField] private BoxCollider2D gatilho;
    [SerializeField] private float forcaRicochete = 10f;

    /// <summary>Disparado quando o dragao termina de morrer (depois da explosao).</summary>
    public event Action OnDerrotado;
    /// <summary>Vida atual e maxima, sempre que a vida muda.</summary>
    public event Action<int, int> OnVidaMudou;

    public int Vida => vida;
    public int VidaMaxima => vidaMaxima;
    public bool Morto => estado == Estado.Morrendo || estado == Estado.Morto;
    public bool Vulneravel => estado == Estado.Tonto;
    public bool Ativo => estado != Estado.Esperando;

    private Estado estado = Estado.Esperando;
    private int vida;
    private Rigidbody2D rb;
    private Transform jogador;
    private Sprite[] animAtual;
    private bool animLoop;
    private float animInicio;
    private int fogosBase;
    private float velocidadeFogoBase;
    private float intervaloFogoBase;
    private float tempoTontoBase;
    private float velocidadeAndarBase;
    private Color corBase = Color.white;
    private float piscarAte;
    private AudioSource fonteContinua;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        vida = vidaMaxima;

        fogosBase = fogosPorRajada;
        velocidadeFogoBase = velocidadeFogo;
        intervaloFogoBase = intervaloFogo;
        tempoTontoBase = tempoTonto;
        velocidadeAndarBase = velocidadeAndar;

        if (gatilho != null)
        {
            gatilho.isTrigger = true;
        }

        if (visual != null)
        {
            corBase = visual.color;
        }

        // Passos e passarinhos sao sons continuos: ficam numa fonte 3D no proprio dragao,
        // entao so se ouvem quando o jogador esta por perto.
        fonteContinua = gameObject.AddComponent<AudioSource>();
        fonteContinua.loop = true;
        fonteContinua.playOnAwake = false;
        fonteContinua.spatialBlend = 1f;
        fonteContinua.rolloffMode = AudioRolloffMode.Linear;
        fonteContinua.minDistance = 5f;
        fonteContinua.maxDistance = 20f;
        fonteContinua.dopplerLevel = 0f;
    }

    /// <summary>Liga um som continuo (passos ou passarinhos); null desliga.</summary>
    private void SomContinuo(AudioClip clipe, float volume)
    {
        if (fonteContinua == null)
        {
            return;
        }

        if (clipe == null)
        {
            if (fonteContinua.isPlaying)
            {
                fonteContinua.Stop();
            }

            return;
        }

        if (fonteContinua.clip == clipe && fonteContinua.isPlaying)
        {
            return;
        }

        fonteContinua.clip = clipe;
        fonteContinua.volume = volume;
        fonteContinua.Play();
        fonteContinua.time = 0.025f;   // pula o silencio do comeco do arquivo
    }

    private void Start()
    {
        GameObject j = GameObject.FindGameObjectWithTag("Player");

        if (j != null)
        {
            jogador = j.transform;
        }

        Tocar(quadrosParado, true);
        OnVidaMudou?.Invoke(vida, vidaMaxima);
    }

    private void Update()
    {
        if (estado == Estado.Esperando && jogador != null
            && Mathf.Abs(jogador.position.x - transform.position.x) <= raioAtivacao)
        {
            estado = Estado.Parado;
            StartCoroutine(Ciclo());
        }

        if (estado == Estado.Esperando || estado == Estado.Andando || estado == Estado.Parado || estado == Estado.Atacando)
        {
            OlharParaJogador();
        }

        Animar();
    }

    // ---------------------------------------------------------------- ciclo de luta

    private IEnumerator Ciclo()
    {
        while (!Morto)
        {
            yield return Andar();

            estado = Estado.Parado;
            Tocar(quadrosParado, true);
            yield return new WaitForSeconds(tempoParado);

            estado = Estado.Atacando;
            int total = fogosPorRajada;
            bool teveBaixa = false;

            for (int i = 0; i < total; i++)
            {
                // Garante ao menos uma bola baixa por rajada (a que obriga a pular).
                bool baixa = (i == total - 1 && !teveBaixa) || UnityEngine.Random.value < 0.5f;
                teveBaixa |= baixa;

                Tocar(baixa ? quadrosAtaqueBaixo : quadrosAtaque, false);
                yield return new WaitForSeconds(preparoFogo);
                Disparar(baixa);
                yield return new WaitForSeconds(Mathf.Max(0.2f, intervaloFogo - preparoFogo));
            }

            estado = Estado.Tonto;
            Tocar(quadrosTonto, true);
            SomContinuo(GerenciadorDeAudio.Banco != null ? GerenciadorDeAudio.Banco.passaros : null, 0.6f);
            float fimTonto = Time.time + tempoTonto;

            while (Time.time < fimTonto && estado == Estado.Tonto)
            {
                yield return null;
            }

            SomContinuo(null, 0f);   // saiu do estado tonto

            if (estado == Estado.Ferido)
            {
                yield return new WaitForSeconds(tempoFerido);
                yield return Saltar();
            }
        }
    }

    /// <summary>
    /// Caminha em direcao ao jogador, sem sair da arena (entre xEsquerda e xDireita), ate chegar
    /// a distanciaMinima, bater na borda ou acabar o tempo.
    /// </summary>
    private IEnumerator Andar()
    {
        if (jogador == null || quadrosAndando == null || quadrosAndando.Length == 0 || velocidadeAndar <= 0f)
        {
            yield break;
        }

        estado = Estado.Andando;
        Tocar(quadrosAndando, true);
        SomContinuo(GerenciadorDeAudio.Banco != null ? GerenciadorDeAudio.Banco.passos : null, 0.55f);
        float minX = Mathf.Min(xEsquerda, xDireita);
        float maxX = Mathf.Max(xEsquerda, xDireita);
        float fim = Time.time + tempoAndando;

        while (Time.time < fim)
        {
            float dx = jogador.position.x - rb.position.x;

            if (Mathf.Abs(dx) <= distanciaMinima)
            {
                break;
            }

            float novoX = Mathf.Clamp(rb.position.x + (Mathf.Sign(dx) * velocidadeAndar * Time.fixedDeltaTime), minX, maxX);

            if (Mathf.Abs(novoX - rb.position.x) < 0.0001f)
            {
                break;   // chegou na borda da arena
            }

            rb.MovePosition(new Vector2(novoX, rb.position.y));
            yield return new WaitForFixedUpdate();
        }

        SomContinuo(null, 0f);   // parou de andar
    }

    private void Disparar(bool baixa)
    {
        if (prefabFogo == null)
        {
            return;
        }

        float dir = Mathf.Sign(transform.localScale.x);
        Vector3 pos = transform.position + new Vector3(
            dir * distanciaBoca,
            baixa ? alturaFogoBaixo : alturaFogoAlto,
            0f);

        GerenciadorDeAudio.TocarEm(transform.position, b => b.fogo, 0.45f);
        ProjetilFogo fogo = Instantiate(prefabFogo, pos, Quaternion.identity);
        fogo.Disparar(new Vector2(dir * velocidadeFogo, 0f));
    }

    private IEnumerator Saltar()
    {
        estado = Estado.Saltando;

        if (gatilho != null)
        {
            gatilho.enabled = false;
        }

        float destino = xDireita;

        if (jogador != null)
        {
            destino = Mathf.Abs(jogador.position.x - xEsquerda) > Mathf.Abs(jogador.position.x - xDireita)
                ? xEsquerda
                : xDireita;
        }

        Vector2 inicio = rb.position;
        Vector2 fim = new Vector2(destino, inicio.y);

        if (Mathf.Abs(fim.x - inicio.x) > 0.1f)
        {
            Virar(fim.x < inicio.x ? -1f : 1f);
        }

        Tocar(quadrosSalto, false);
        float t = 0f;

        while (t < duracaoSalto)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duracaoSalto);
            Vector2 p = Vector2.Lerp(inicio, fim, k);
            p.y += 4f * alturaSalto * k * (1f - k);
            rb.MovePosition(p);
            yield return null;
        }

        rb.MovePosition(fim);
        yield return new WaitForFixedUpdate();

        if (gatilho != null && !Morto)
        {
            gatilho.enabled = true;
        }
    }

    // ---------------------------------------------------------------- dano

    /// <summary>Bola de lama do jogador. So conta enquanto ele esta tonto.</summary>
    public void LevarTiro()
    {
        if (Vulneravel)
        {
            LevarDano();
        }
    }

    private void LevarDano()
    {
        vida--;
        piscarAte = Time.time + 0.6f;
        OnVidaMudou?.Invoke(vida, vidaMaxima);

        if (vida <= 0)
        {
            StopAllCoroutines();
            StartCoroutine(Morrer());
            return;
        }

        estado = Estado.Ferido;
        Tocar(quadrosFerido, false);

        AplicarDificuldade();
    }

    /// <summary>
    /// A luta comeca facil e endurece a cada vida perdida: mais bolas por rajada, bolas mais
    /// rapidas, menos intervalo entre elas, janela de tonto mais curta e passo mais rapido.
    /// Tudo calculado a partir dos valores iniciais, para nao acumular erro de arredondamento.
    /// </summary>
    private void AplicarDificuldade()
    {
        int estagio = Mathf.Max(0, vidaMaxima - vida);

        fogosPorRajada = fogosBase + (estagio * fogosPorEstagio);
        velocidadeFogo = Mathf.Min(velocidadeFogoMaxima, velocidadeFogoBase + (estagio * velocidadeFogoPorEstagio));
        intervaloFogo = Mathf.Max(intervaloFogoMinimo, intervaloFogoBase - (estagio * intervaloFogoPorEstagio));
        tempoTonto = Mathf.Max(tempoTontoMinimo, tempoTontoBase - (estagio * tempoTontoPorEstagio));
        velocidadeAndar = velocidadeAndarBase + (estagio * velocidadeAndarPorEstagio);
    }

    private IEnumerator Morrer()
    {
        estado = Estado.Morrendo;
        SomContinuo(null, 0f);
        GerenciadorDeAudio.TocarEm(transform.position, b => b.explosao, 0.9f);
        GerenciadorDeAudio.TocarMusicaFinal();

        if (gatilho != null)
        {
            gatilho.enabled = false;
        }

        if (quadrosMorte != null && quadrosMorte.Length > 0)
        {
            Tocar(quadrosMorte, false);
            yield return new WaitForSeconds((quadrosMorte.Length / quadrosPorSegundo) + 0.3f);
        }

        if (quadrosExplosao != null && quadrosExplosao.Length > 0)
        {
            Tocar(quadrosExplosao, false);
            yield return new WaitForSeconds(quadrosExplosao.Length / quadrosPorSegundo);
        }

        if (visual != null)
        {
            visual.enabled = false;
        }

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.InimigoDerrotado(pontos, true);
        }

        PontosFlutuantes.Mostrar(popupPontos, transform.position + (Vector3.up * 2f), pontos, new Color(1f, 0.84f, 0.29f));

        estado = Estado.Morto;
        OnDerrotado?.Invoke();
    }

    // ---------------------------------------------------------------- contato com o jogador

    private void OnTriggerEnter2D(Collider2D other)
    {
        TratarContato(other, true);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TratarContato(other, false);
    }

    private void TratarContato(Collider2D other, bool entrou)
    {
        // Ferido e Saltando sao transicoes: ele atravessa o jogador para se reposicionar e nao
        // da para desviar, entao nesses estados o contato nao faz nada.
        if (Morto || estado == Estado.Ferido || estado == Estado.Saltando)
        {
            return;
        }

        PlayerController jogadorTocou = other.GetComponentInParent<PlayerController>();

        if (jogadorTocou == null)
        {
            return;
        }

        if (estado == Estado.Tonto)
        {
            // Tonto: encostar de lado nao faz nada; so a pisada (vindo de cima, caindo) conta.
            if (!entrou)
            {
                return;
            }

            Rigidbody2D rbJogador = other.attachedRigidbody;
            bool deCima = rbJogador != null
                && rbJogador.linearVelocity.y <= 0.1f
                && other.bounds.min.y >= gatilho.bounds.center.y;

            if (deCima)
            {
                GerenciadorDeAudio.TocarEm(transform.position, b => b.pisada);
                rbJogador.linearVelocity = new Vector2(rbJogador.linearVelocity.x, forcaRicochete);
                jogadorTocou.ConcederInvencibilidade(0.6f);
                LevarDano();
            }

            return;
        }

        jogadorTocou.Morrer();
    }

    // ---------------------------------------------------------------- visual

    private void OlharParaJogador()
    {
        if (jogador == null)
        {
            return;
        }

        Virar(jogador.position.x < transform.position.x ? -1f : 1f);
    }

    /// <summary>Os sprites olham para a direita; escala X negativa vira tudo (colisor junto).</summary>
    private void Virar(float direcao)
    {
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * direcao;
        transform.localScale = s;
    }

    private void Tocar(Sprite[] quadros, bool loop)
    {
        if (quadros == null || quadros.Length == 0)
        {
            return;
        }

        animAtual = quadros;
        animLoop = loop;
        animInicio = Time.time;
        Animar();
    }

    private void Animar()
    {
        if (visual == null || animAtual == null || animAtual.Length == 0)
        {
            return;
        }

        int i = Mathf.FloorToInt((Time.time - animInicio) * quadrosPorSegundo);
        i = animLoop ? i % animAtual.Length : Mathf.Min(i, animAtual.Length - 1);
        visual.sprite = animAtual[i];

        bool piscando = Time.time < piscarAte && Mathf.Repeat(Time.time * 12f, 1f) < 0.5f;
        visual.color = piscando ? corDano : corBase;
    }
}
