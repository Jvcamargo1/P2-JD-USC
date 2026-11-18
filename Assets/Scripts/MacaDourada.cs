using System.Collections;
using UnityEngine;

/// <summary>
/// Maca dourada: coletavel obrigatorio da fase. Alem de chegar ao fim, o jogador
/// precisa pegar todas as macas douradas antes que a bandeira libere a vitoria.
///
/// Vale bem mais pontos que uma fruta comum e fica em lugares arriscados, para dar
/// motivo de enfrentar inimigos e armadilhas em vez de correr direto para o fim.
///
/// A animacao de brilho e feita por codigo (troca de quadros do sprite sheet), sem
/// precisar de AnimationClip. Ao ser coletada, cresce e some.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class MacaDourada : MonoBehaviour
{
    [Header("Pontuacao")]
    [SerializeField] private int pontos = 500;
    [SerializeField] private PontosFlutuantes popupPontos;
    [SerializeField] private Color corPopup = new Color(1f, 0.85f, 0.25f);

    [Header("Animacao")]
    [Tooltip("Quadros do brilho, em ordem.")]
    [SerializeField] private Sprite[] quadros;
    [SerializeField] private float quadrosPorSegundo = 12f;

    [Header("Movimento")]
    [SerializeField] private float amplitudeFlutuacao = 0.12f;
    [SerializeField] private float velocidadeFlutuacao = 2f;

    [Header("Coleta")]
    [SerializeField] private float duracaoColeta = 0.4f;
    [SerializeField] private float escalaColeta = 1.8f;

    /// <summary>Primeiro quadro, usado como icone no HUD.</summary>
    public Sprite SpriteIcone => (quadros != null && quadros.Length > 0) ? quadros[0] : null;

    private SpriteRenderer sr;
    private Collider2D colisor;
    private Vector3 posicaoBase;
    private Vector3 escalaBase;
    private float fase;
    private bool coletada;
    private bool surgindo;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        colisor = GetComponent<Collider2D>();
        colisor.isTrigger = true;
        posicaoBase = transform.position;
        escalaBase = transform.localScale;
    }

    private void Start()
    {
        posicaoBase = transform.position;
        fase = (posicaoBase.x + posicaoBase.y) * 0.9f;
    }

    private void Update()
    {
        if (coletada || surgindo)
        {
            return;
        }

        if (quadros != null && quadros.Length > 0)
        {
            int i = Mathf.FloorToInt(Time.time * quadrosPorSegundo) % quadros.Length;
            sr.sprite = quadros[i];
        }

        float deslocamento = Mathf.Sin((Time.time * velocidadeFlutuacao) + fase) * amplitudeFlutuacao;
        transform.position = posicaoBase + (Vector3.up * deslocamento);
    }

    /// <summary>
    /// Faz a maca saltar da posicao atual ate o destino em arco, e so depois comecar a
    /// flutuar no lugar. Usado pelas caixas quebradas (mesmo padrao de Collectible.Surgir).
    /// </summary>
    public void Surgir(Vector3 destino, float duracao)
    {
        StartCoroutine(RotinaSurgir(destino, duracao));
    }

    private IEnumerator RotinaSurgir(Vector3 destino, float duracao)
    {
        surgindo = true;
        Vector3 origem = transform.position;
        float tempo = 0f;

        while (tempo < duracao)
        {
            tempo += Time.deltaTime;
            float t = Mathf.Clamp01(tempo / duracao);
            Vector3 p = Vector3.Lerp(origem, destino, t);
            p.y += Mathf.Sin(t * Mathf.PI) * 0.6f;
            transform.position = p;
            yield return null;
        }

        posicaoBase = destino;
        fase = (posicaoBase.x + posicaoBase.y) * 0.9f;
        transform.position = destino;
        surgindo = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (coletada || other.GetComponent<PlayerController>() == null)
        {
            return;
        }

        coletada = true;
        colisor.enabled = false;

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.MacaDouradaColetada(pontos);
        }

        GerenciadorDeAudio.Tocar(b => b.macaDourada, 0.75f);
        PontosFlutuantes.Mostrar(popupPontos, transform.position + (Vector3.up * 0.4f), pontos, corPopup);
        StartCoroutine(Sumir());
    }

    private IEnumerator Sumir()
    {
        float tempo = 0f;
        Color corInicial = sr.color;

        while (tempo < duracaoColeta)
        {
            tempo += Time.deltaTime;
            float t = Mathf.Clamp01(tempo / duracaoColeta);
            transform.localScale = escalaBase * Mathf.Lerp(1f, escalaColeta, t);
            transform.position = posicaoBase + (Vector3.up * (t * 0.8f));
            sr.color = new Color(corInicial.r, corInicial.g, corInicial.b, 1f - t);
            yield return null;
        }

        Destroy(gameObject);
    }
}
