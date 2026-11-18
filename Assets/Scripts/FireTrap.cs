using UnityEngine;

/// <summary>
/// Chama que alterna entre apagada, faisca (aviso) e acesa. So queima, e so
/// mata, no estado Acesa: o colisor da chama fica desligado nas outras fases
/// do ciclo. A morte em si vem do componente Obstacle, no mesmo objeto, que
/// tambem faz a chama cair junto com o chao quando a onda o derruba.
///
/// Chamas vizinhas usam "faseInicial" diferentes, formando uma ondulacao que o
/// jogador precisa ler para atravessar. O relogio e proprio (nao usa o tempo
/// global), entao pausar o jogo congela o ciclo.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FireTrap : MonoBehaviour
{
    public enum Estado { Apagada, Faisca, Acesa }

    [Header("Ciclo (segundos)")]
    [SerializeField] private float tempoApagada = 1.5f;
    [Tooltip("Aviso antes de acender: a chama solta faiscas, mas ainda nao machuca.")]
    [SerializeField] private float tempoFaisca = 0.6f;
    [SerializeField] private float tempoAcesa = 1.5f;
    [Tooltip("Deslocamento no ciclo. Chamas vizinhas usam valores diferentes.")]
    [SerializeField] private float faseInicial;

    [Header("Componentes")]
    [Tooltip("Colisor (gatilho) da chama; fica ligado somente quando acesa.")]
    [SerializeField] private Collider2D areaDeFogo;
    [SerializeField] private Animator animator;

    private static readonly string[] NomesDeAnimacao = { "Apagada", "Faisca", "Acesa" };

    private float relogio;
    private bool iniciada;
    private AudioSource fonte;

    /// <summary>Duracao de um ciclo completo.</summary>
    public float Ciclo => tempoApagada + tempoFaisca + tempoAcesa;

    public Estado EstadoAtual { get; private set; }

    /// <summary>
    /// Estado de uma chama num instante do ciclo. Usado tambem pela auditoria
    /// do construtor de fases, para conferir se ha janela segura.
    /// </summary>
    public static Estado EstadoEm(float tempo, float apagada, float faisca, float acesa)
    {
        float t = Mathf.Repeat(tempo, apagada + faisca + acesa);

        if (t < apagada)
        {
            return Estado.Apagada;
        }

        return t < apagada + faisca ? Estado.Faisca : Estado.Acesa;
    }

    private void Start()
    {
        PrepararSom();
        relogio = faseInicial;
        Aplicar(EstadoEm(relogio, tempoApagada, tempoFaisca, tempoAcesa), true);
    }

    private void Update()
    {
        // Se o chao desabou, o Obstacle ja deu um Rigidbody2D a chama: ela esta
        // caindo e nao deve mais acender (nem religar o colisor).
        if (GetComponent<Rigidbody2D>() != null)
        {
            enabled = false;
            return;
        }

        relogio += Time.deltaTime;
        Estado novo = EstadoEm(relogio, tempoApagada, tempoFaisca, tempoAcesa);

        if (novo != EstadoAtual)
        {
            Aplicar(novo, false);
        }
    }

    /// <summary>
    /// Som proprio da chama, em 3D: so se ouve quem esta perto, para varias chamas numa
    /// mesma tela nao virarem barulheira. Toca em loop enquanto a chama esta acesa.
    /// </summary>
    private void PrepararSom()
    {
        AudioClip clipe = GerenciadorDeAudio.Banco != null ? GerenciadorDeAudio.Banco.fogo : null;

        if (clipe == null)
        {
            return;
        }

        fonte = gameObject.AddComponent<AudioSource>();
        fonte.clip = clipe;
        fonte.loop = true;
        fonte.playOnAwake = false;
        fonte.volume = 0.3f;
        fonte.spatialBlend = 1f;                       // posicional: o volume cai com a distancia
        fonte.rolloffMode = AudioRolloffMode.Linear;
        fonte.minDistance = 5f;
        fonte.maxDistance = 18f;
        fonte.dopplerLevel = 0f;
    }

    private void Aplicar(Estado novo, bool forcar)
    {
        if (!forcar && novo == EstadoAtual && iniciada)
        {
            return;
        }

        iniciada = true;
        EstadoAtual = novo;

        if (areaDeFogo != null)
        {
            areaDeFogo.enabled = novo == Estado.Acesa;
        }

        if (fonte != null)
        {
            if (novo == Estado.Acesa && !fonte.isPlaying)
            {
                fonte.Play();

                // O arquivo tem quase meio segundo mudo no comeco; sem pular isso, a chama
                // ficaria quase toda a fase acesa em silencio.
                fonte.time = 0.45f;
            }
            else if (novo != Estado.Acesa && fonte.isPlaying)
            {
                fonte.Stop();
            }
        }

        if (animator != null)
        {
            animator.Play(NomesDeAnimacao[(int)novo]);
        }

        // Quem ja estava parado em cima quando a chama acendeu tambem queima.
        if (novo == Estado.Acesa && areaDeFogo != null)
        {
            QueimarQuemEstiverDentro();
        }
    }

    private void QueimarQuemEstiverDentro()
    {
        Bounds area = areaDeFogo.bounds;
        Collider2D[] dentro = Physics2D.OverlapBoxAll(area.center, area.size, 0f);

        foreach (Collider2D c in dentro)
        {
            PlayerController jogador = c.GetComponent<PlayerController>();

            if (jogador != null)
            {
                jogador.Morrer();
            }
        }
    }
}
