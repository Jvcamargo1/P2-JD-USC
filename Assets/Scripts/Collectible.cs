using System.Collections;
using UnityEngine;

/// <summary>
/// Fruta coletavel. Ao ser tocada pelo jogador, soma pontos e some com a
/// animacao de coleta. Flutua levemente no lugar e, se a onda de destruicao
/// passar por ela, desaparece sem pontuar. Frutas que saem de caixas
/// quebradas usam Surgir() para saltar ate a posicao final.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Collectible : MonoBehaviour
{
    [Header("Pontuacao")]
    [SerializeField] private int pontos = 100;
    [Tooltip("Texto '+pontos' mostrado ao coletar (prefab Popup_Pontos).")]
    [SerializeField] private PontosFlutuantes popupPontos;
    [SerializeField] private Color corPopup = Color.white;

    [Header("Visual")]
    [SerializeField] private Animator animator;
    [Tooltip("Tempo da animacao de coleta antes de remover o objeto.")]
    [SerializeField] private float duracaoColeta = 0.35f;
    [Tooltip("Amplitude da flutuacao, em unidades.")]
    [SerializeField] private float amplitudeFlutuacao = 0.08f;
    [SerializeField] private float velocidadeFlutuacao = 2.5f;

    private static readonly int ParamColetar = Animator.StringToHash("Coletar");

    private Collider2D colisor;
    private Transform onda;
    private Vector3 posicaoBase;
    private float fase;
    private bool coletado;
    private bool surgindo;

    private void Awake()
    {
        colisor = GetComponent<Collider2D>();
        colisor.isTrigger = true;
        posicaoBase = transform.position;
    }

    private void Start()
    {
        if (!surgindo)
        {
            posicaoBase = transform.position;
        }

        // Fase diferente por posicao: frutas vizinhas nao flutuam em sincronia.
        fase = posicaoBase.x * 0.7f;

        DestructionWave ondaDestruicao = FindFirstObjectByType<DestructionWave>();

        if (ondaDestruicao != null)
        {
            onda = ondaDestruicao.transform;
        }
    }

    private void Update()
    {
        if (coletado || surgindo)
        {
            return;
        }

        float deslocamento = Mathf.Sin((Time.time * velocidadeFlutuacao) + fase) * amplitudeFlutuacao;
        transform.position = posicaoBase + (Vector3.up * deslocamento);

        if (onda != null && onda.position.x > posicaoBase.x)
        {
            Sumir();
        }
    }

    /// <summary>
    /// Faz a fruta saltar da posicao atual ate o destino em arco, e so depois
    /// comecar a flutuar. Usado pelas caixas quebradas.
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
        fase = posicaoBase.x * 0.7f;
        transform.position = destino;
        surgindo = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (coletado || other.GetComponent<PlayerController>() == null)
        {
            return;
        }

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.FrutaColetada(pontos);
        }

        GerenciadorDeAudio.Tocar(b => b.fruta, 0.55f);
        PontosFlutuantes.Mostrar(popupPontos, transform.position + (Vector3.up * 0.3f), pontos, corPopup);
        Sumir();
    }

    private void Sumir()
    {
        coletado = true;
        surgindo = false;
        StopAllCoroutines();
        colisor.enabled = false;

        if (animator != null)
        {
            animator.SetTrigger(ParamColetar);
        }

        Destroy(gameObject, duracaoColeta);
    }
}
