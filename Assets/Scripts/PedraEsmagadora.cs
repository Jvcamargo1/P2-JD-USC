using UnityEngine;

/// <summary>
/// Pedra que esmaga (Rock Head) e cabeca de espinhos (Spike Head).
///
/// Ciclo: espera parada, avisa tremendo e piscando, dispara na direcao do golpe,
/// bate (com animacao de impacto), espera um instante e volta devagar.
///
/// Quem mata o jogador e o componente Obstacle (gatilho), no mesmo objeto. Quando o
/// chunk onde a pedra esta preso desaba, o Obstacle a transforma em corpo dinamico:
/// ela passa a cair com o chao e este script se desliga.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PedraEsmagadora : MonoBehaviour
{
    private enum Fase { Espera, Aviso, Golpe, Impacto, Volta }

    [Header("Movimento")]
    [Tooltip("Sentido do golpe: baixo para o pilao (Rock Head), direita para a cabeca de espinhos.")]
    [SerializeField] private Vector2 direcao = Vector2.down;
    [Tooltip("Distancia ate o impacto, em unidades. O LevelBuilder calcula ate a primeira superficie solida.")]
    [SerializeField] private float distancia = 2f;
    [Tooltip("Velocidade maxima do golpe, em unidades por segundo.")]
    [SerializeField] private float velocidadeGolpe = 16f;
    [Tooltip("Aceleracao do golpe: a pedra arranca devagar e cai com forca.")]
    [SerializeField] private float aceleracaoGolpe = 90f;
    [SerializeField] private float velocidadeVolta = 3f;

    [Header("Ciclo (segundos)")]
    [Tooltip("Parada no ponto inicial, antes do aviso. E a janela segura para passar.")]
    [SerializeField] private float tempoEspera = 1.6f;
    [Tooltip("Aviso antes do golpe: a pedra treme e pisca.")]
    [SerializeField] private float tempoAviso = 0.5f;
    [SerializeField] private float tempoImpacto = 0.45f;
    [Tooltip("Deslocamento no ciclo, para pedras vizinhas nao baterem juntas.")]
    [SerializeField] private float faseInicial;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer render;
    [SerializeField] private Sprite[] quadrosPiscar;
    [SerializeField] private Sprite[] quadrosImpacto;
    [SerializeField] private float quadrosPorSegundo = 12f;
    [Tooltip("Amplitude do tremor durante o aviso, em unidades.")]
    [SerializeField] private float tremor = 0.05f;

    private Rigidbody2D rb;
    private Vector2 origem;
    private Vector2 sentido;
    private Fase fase = Fase.Espera;
    private float relogio;
    private float deslocamento;
    private float velocidadeAtual;
    private Sprite spriteParado;
    private Vector3 posicaoVisualBase;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sentido = direcao.sqrMagnitude > 0f ? direcao.normalized : Vector2.down;

        if (render == null)
        {
            render = GetComponentInChildren<SpriteRenderer>();
        }

        if (render != null)
        {
            spriteParado = render.sprite;
            posicaoVisualBase = render.transform.localPosition;
        }
    }

    private void Start()
    {
        origem = rb.position;

        // A fase inicial adianta o relogio do ciclo (so ate o fim do aviso).
        float ciclo = Mathf.Max(0.01f, tempoEspera + tempoAviso);
        float adiantado = Mathf.Repeat(faseInicial, ciclo);

        if (adiantado < tempoEspera)
        {
            relogio = adiantado;
        }
        else
        {
            fase = Fase.Aviso;
            relogio = adiantado - tempoEspera;
        }
    }

    private void FixedUpdate()
    {
        // O chunk desabou: o Obstacle deu fisica dinamica a pedra. Ela so cai.
        if (rb.bodyType != RigidbodyType2D.Kinematic)
        {
            if (render != null)
            {
                render.transform.localPosition = posicaoVisualBase;
            }

            enabled = false;
            return;
        }

        if (GameManager.Instance != null &&
            GameManager.Instance.Estado != GameManager.GameState.Playing)
        {
            return;
        }

        float dt = Time.fixedDeltaTime;
        relogio += dt;

        switch (fase)
        {
            case Fase.Espera:
                deslocamento = 0f;

                if (relogio >= tempoEspera)
                {
                    MudarFase(Fase.Aviso);
                }

                break;

            case Fase.Aviso:
                if (relogio >= tempoAviso)
                {
                    velocidadeAtual = 0f;
                    MudarFase(Fase.Golpe);
                }

                break;

            case Fase.Golpe:
                velocidadeAtual = Mathf.Min(velocidadeGolpe, velocidadeAtual + (aceleracaoGolpe * dt));
                deslocamento += velocidadeAtual * dt;

                if (deslocamento >= distancia)
                {
                    deslocamento = distancia;
                    MudarFase(Fase.Impacto);
                }

                break;

            case Fase.Impacto:
                if (relogio >= tempoImpacto)
                {
                    MudarFase(Fase.Volta);
                }

                break;

            case Fase.Volta:
                deslocamento -= velocidadeVolta * dt;

                if (deslocamento <= 0f)
                {
                    deslocamento = 0f;
                    MudarFase(Fase.Espera);
                }

                break;
        }

        rb.MovePosition(origem + (sentido * deslocamento));
    }

    private void Update()
    {
        if (render == null || !enabled)
        {
            return;
        }

        Sprite quadro = spriteParado;
        Vector3 tremorAtual = Vector3.zero;

        if (fase == Fase.Aviso)
        {
            tremorAtual = (Vector3)(Random.insideUnitCircle * tremor);
            quadro = Quadro(quadrosPiscar, relogio, true) ?? spriteParado;
        }
        else if (fase == Fase.Impacto)
        {
            quadro = Quadro(quadrosImpacto, relogio, false) ?? spriteParado;
        }

        render.sprite = quadro;
        render.transform.localPosition = posicaoVisualBase + tremorAtual;
    }

    private void MudarFase(Fase nova)
    {
        if (nova == Fase.Impacto)
        {
            GerenciadorDeAudio.TocarEm(transform.position, b => b.pedra, 0.7f);
        }

        fase = nova;
        relogio = 0f;
    }

    private Sprite Quadro(Sprite[] quadros, float tempo, bool repetir)
    {
        if (quadros == null || quadros.Length == 0)
        {
            return null;
        }

        int indice = Mathf.FloorToInt(tempo * quadrosPorSegundo);
        indice = repetir ? indice % quadros.Length : Mathf.Min(indice, quadros.Length - 1);
        return quadros[indice];
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 inicio = Application.isPlaying ? origem : (Vector2)transform.position;
        Vector2 fim = inicio + ((direcao.sqrMagnitude > 0f ? direcao.normalized : Vector2.down) * distancia);
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.9f);
        Gizmos.DrawLine(inicio, fim);
        Gizmos.DrawWireCube(fim, Vector3.one * 1.3f);
    }
}
