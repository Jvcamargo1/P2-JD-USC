using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Plataforma que pode se mover (horizontal ou vertical) e/ou cair quando o
/// jogador pisa nela.
///
/// O movimento usa Rigidbody2D cinematico com MovePosition e repassa o mesmo
/// deslocamento para quem esta em cima, para que o jogador seja carregado
/// junto em vez de escorregar.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MovingFallingPlatform : MonoBehaviour
{
    public enum EixoMovimento { Horizontal, Vertical }

    [Header("Movimento")]
    [SerializeField] private bool movimentoAtivo = true;
    [SerializeField] private EixoMovimento eixo = EixoMovimento.Horizontal;
    [Tooltip("Deslocamento maximo a partir da posicao inicial, em unidades.")]
    [SerializeField] private float distancia = 3f;
    [Tooltip("Velocidade do vaivem. Valores maiores = ida e volta mais rapida.")]
    [SerializeField] private float velocidade = 2f;
    [Tooltip("Defasagem inicial do ciclo, em radianos. Use para dessincronizar plataformas.")]
    [SerializeField] private float defasagem = 0f;

    [Header("Queda ao pisar")]
    [SerializeField] private bool caiAoPisar = false;
    [Tooltip("Segundos entre o jogador pisar e a plataforma comecar a cair.")]
    [SerializeField] private float atrasoQueda = 0.6f;
    [Tooltip("Segundos ate a plataforma ser destruida depois de cair.")]
    [SerializeField] private float tempoAteDestruir = 3f;
    [Tooltip("Amplitude do tremor de aviso antes de cair.")]
    [SerializeField] private float tremorAviso = 0.06f;

    private Rigidbody2D rb;
    private Vector2 posicaoInicial;
    private Vector2 posicaoAnterior;
    private bool caindo;
    private bool quedaAgendada;
    private Transform visual;

    private readonly List<Rigidbody2D> passageiros = new List<Rigidbody2D>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (transform.childCount > 0)
        {
            visual = transform.GetChild(0);
        }
    }

    private void Start()
    {
        posicaoInicial = rb.position;
        posicaoAnterior = rb.position;
    }

    private void FixedUpdate()
    {
        if (caindo || !movimentoAtivo)
        {
            return;
        }

        if (GameManager.Instance != null &&
            GameManager.Instance.Estado != GameManager.GameState.Playing)
        {
            return;
        }

        float offset = Mathf.Sin((Time.time * velocidade) + defasagem) * distancia;

        Vector2 destino = eixo == EixoMovimento.Horizontal
            ? posicaoInicial + new Vector2(offset, 0f)
            : posicaoInicial + new Vector2(0f, offset);

        Vector2 delta = destino - posicaoAnterior;

        rb.MovePosition(destino);

        // Carrega quem esta em cima, aplicando o mesmo deslocamento.
        for (int i = passageiros.Count - 1; i >= 0; i--)
        {
            Rigidbody2D passageiro = passageiros[i];

            if (passageiro == null)
            {
                passageiros.RemoveAt(i);
                continue;
            }

            passageiro.position += delta;
        }

        posicaoAnterior = destino;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!EstaEmCima(collision))
        {
            return;
        }

        Rigidbody2D outro = collision.rigidbody;

        if (outro != null && !passageiros.Contains(outro))
        {
            passageiros.Add(outro);
        }

        if (caiAoPisar && !quedaAgendada &&
            collision.gameObject.GetComponent<PlayerController>() != null)
        {
            quedaAgendada = true;
            StartCoroutine(AvisarECair());
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        Rigidbody2D outro = collision.rigidbody;

        if (outro != null)
        {
            passageiros.Remove(outro);
        }
    }

    /// <summary>
    /// Verifica se o contato veio por cima, para nao carregar quem bate de lado.
    /// </summary>
    private bool EstaEmCima(Collision2D collision)
    {
        foreach (ContactPoint2D contato in collision.contacts)
        {
            // Normal apontando para cima = o outro corpo esta acima desta plataforma.
            if (contato.normal.y < -0.5f)
            {
                return true;
            }
        }

        return false;
    }

    private System.Collections.IEnumerator AvisarECair()
    {
        // Tremor curto avisando que a plataforma vai cair.
        float tempo = 0f;
        Vector3 origemVisual = visual != null ? visual.localPosition : Vector3.zero;

        while (tempo < atrasoQueda)
        {
            tempo += Time.deltaTime;

            if (visual != null)
            {
                float tremor = Mathf.Sin(tempo * 60f) * tremorAviso;
                visual.localPosition = origemVisual + new Vector3(tremor, 0f, 0f);
            }

            yield return null;
        }

        if (visual != null)
        {
            visual.localPosition = origemVisual;
        }

        Cair();
    }

    private void Cair()
    {
        if (caindo)
        {
            return;
        }

        caindo = true;
        GerenciadorDeAudio.TocarEm(transform.position, b => b.plataformaCaindo, 0.6f);
        passageiros.Clear();

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 2.5f;

        Destroy(gameObject, tempoAteDestruir);
    }
}
