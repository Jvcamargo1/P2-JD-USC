using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sombra: inimigo da Fase 4. Grava o caminho do jogador e o refaz com um atraso fixo,
/// como um eco. Nao pode ser derrotada; encostar nela mata.
///
/// A gravacao so comeca quando o jogador sai do ponto de inicio, para nao punir quem
/// esta lendo os controles. Se o jogador parar ou voltar atras por mais tempo que o
/// atraso, a Sombra o alcanca: voltar para pegar uma maca dourada vira um risco.
///
/// O movimento e por transform (sem fisica): ela passa por buracos e plataformas que
/// ja cairam exatamente como o jogador passou. A animacao e feita por codigo.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class SombraPerseguidora : MonoBehaviour
{
    [Header("Perseguicao")]
    [Tooltip("Quantos segundos a Sombra fica atras do jogador.")]
    [SerializeField] private float atraso = 3f;
    [Tooltip("Distancia que o jogador precisa andar do inicio para a gravacao comecar.")]
    [SerializeField] private float distanciaPartida = 1.5f;
    [SerializeField] private float tempoSurgir = 0.6f;

    [Header("Visual")]
    [SerializeField] private Color cor = new Color(0.45f, 0.2f, 0.75f, 0.85f);
    [SerializeField] private Sprite[] quadrosParado;
    [SerializeField] private Sprite[] quadrosCorrendo;
    [SerializeField] private Sprite quadroPulo;
    [SerializeField] private Sprite quadroQueda;
    [SerializeField] private float quadrosPorSegundo = 16f;

    private struct Amostra
    {
        public float tempo;
        public Vector3 posicao;
        public bool virado;
    }

    private readonly Queue<Amostra> rastro = new Queue<Amostra>();

    private SpriteRenderer sr;
    private Collider2D colisor;
    private PlayerController jogador;
    private SpriteRenderer srJogador;
    private Vector3 pontoInicio;
    private bool gravando;
    private float inicioGravacao;
    private float alfa;
    private Vector3 posicaoAnterior;
    private bool encerrada;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        colisor = GetComponent<Collider2D>();
        colisor.isTrigger = true;
        colisor.enabled = false;
        sr.color = new Color(cor.r, cor.g, cor.b, 0f);
    }

    private void Start()
    {
        jogador = FindFirstObjectByType<PlayerController>();

        if (jogador == null)
        {
            enabled = false;
            return;
        }

        // Grava a posicao do visual do jogador, que e o que aparece na tela.
        srJogador = jogador.GetComponentInChildren<SpriteRenderer>();
        pontoInicio = jogador.transform.position;
        transform.position = PosicaoVisual();
        posicaoAnterior = transform.position;

        if (srJogador != null)
        {
            sr.sortingLayerID = srJogador.sortingLayerID;
            sr.sortingOrder = srJogador.sortingOrder - 1;
        }
    }

    private void Update()
    {
        if (jogador == null)
        {
            return;
        }

        bool jogando = GameManager.Instance == null ||
            GameManager.Instance.Estado == GameManager.GameState.Playing;

        if (!jogando)
        {
            // Fim de fase ou game over: a Sombra se dissolve e para de agir.
            encerrada = true;
            colisor.enabled = false;
            alfa = Mathf.MoveTowards(alfa, 0f, Time.deltaTime * 2f);
            AplicarCor();
            return;
        }

        if (encerrada)
        {
            return;
        }

        if (!gravando)
        {
            if (Vector2.Distance(jogador.transform.position, pontoInicio) < distanciaPartida)
            {
                return;
            }

            gravando = true;
            inicioGravacao = Time.time;
        }

        rastro.Enqueue(new Amostra
        {
            tempo = Time.time,
            posicao = PosicaoVisual(),
            virado = srJogador != null && srJogador.flipX
        });

        float tempoAlvo = Time.time - atraso;

        if (tempoAlvo < inicioGravacao)
        {
            return;
        }

        // Consome as amostras ate o instante de "atraso" segundos atras.
        bool moveu = false;
        Amostra atual = default;

        while (rastro.Count > 0 && rastro.Peek().tempo <= tempoAlvo)
        {
            atual = rastro.Dequeue();
            moveu = true;
        }

        if (moveu)
        {
            transform.position = atual.posicao;
            sr.flipX = atual.virado;
        }

        alfa = Mathf.MoveTowards(alfa, 1f, Time.deltaTime / Mathf.Max(0.01f, tempoSurgir));
        AplicarCor();

        // So mata depois de aparecer quase toda, para nao surgir ja em cima do jogador.
        colisor.enabled = alfa > 0.8f;

        Animar(transform.position - posicaoAnterior);
        posicaoAnterior = transform.position;
    }

    private Vector3 PosicaoVisual()
    {
        return srJogador != null ? srJogador.transform.position : jogador.transform.position;
    }

    private void AplicarCor()
    {
        sr.color = new Color(cor.r, cor.g, cor.b, cor.a * alfa);
    }

    private void Animar(Vector3 deslocamento)
    {
        float vy = deslocamento.y / Mathf.Max(Time.deltaTime, 0.0001f);
        float vx = deslocamento.x / Mathf.Max(Time.deltaTime, 0.0001f);

        if (vy > 1.5f && quadroPulo != null)
        {
            sr.sprite = quadroPulo;
        }
        else if (vy < -1.5f && quadroQueda != null)
        {
            sr.sprite = quadroQueda;
        }
        else
        {
            Sprite[] quadros = Mathf.Abs(vx) > 0.5f ? quadrosCorrendo : quadrosParado;

            if (quadros != null && quadros.Length > 0)
            {
                sr.sprite = quadros[Mathf.FloorToInt(Time.time * quadrosPorSegundo) % quadros.Length];
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Tocar(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        Tocar(other);
    }

    private void Tocar(Collider2D other)
    {
        if (encerrada || !colisor.enabled)
        {
            return;
        }

        PlayerController pc = other.GetComponent<PlayerController>();

        if (pc != null)
        {
            pc.Morrer();
        }
    }
}
