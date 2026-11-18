using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Toca a musica de fundo e os efeitos do jogo. Cria-se sozinho no primeiro uso (carregando
/// Resources/BancoDeSons) e sobrevive a troca de cena, para a musica nao reiniciar a cada
/// carregamento. A musica e escolhida pelo nome da cena: MainMenu ou PhaseN.
///
/// Uso no codigo do jogo: GerenciadorDeAudio.Tocar(b => b.pulo);
/// </summary>
public class GerenciadorDeAudio : MonoBehaviour
{
    private const string CaminhoBanco = "BancoDeSons";

    [SerializeField] private BancoDeSons banco;
    [Range(0f, 1f)] [SerializeField] private float volumeMusica = 0.35f;
    [Range(0f, 1f)] [SerializeField] private float volumeEfeitos = 0.65f;

    [Header("Alcance dos sons do mundo")]
    [Tooltip("Ate esta distancia o som sai no volume cheio.")]
    [SerializeField] private float distanciaMinima = 4f;
    [Tooltip("Alem desta distancia o som nao e mais ouvido.")]
    [SerializeField] private float distanciaMaxima = 16f;

    private static GerenciadorDeAudio instancia;
    private AudioSource fonteMusica;
    private Transform ouvinte;
    private AudioSource fonteEfeitos;
    private AudioClip musicaAtual;

    // Ligado quando o chefe cai: a musica final toca ate o fim da partida e continua tocando
    // no menu para onde o jogador e levado. So desliga quando ele entra numa fase de novo.
    private bool modoFinal;

    // Muitos MP3 tem um trecho mudo no comeco (atraso do codificador). Guardamos quanto pular
    // de cada clipe para o som sair no instante do evento, e nao alguns decimos depois.
    private readonly System.Collections.Generic.Dictionary<AudioClip, float> inicioReal =
        new System.Collections.Generic.Dictionary<AudioClip, float>();

    public static BancoDeSons Banco => Instancia != null ? Instancia.banco : null;

    /// <summary>
    /// Cria o gerenciador assim que o jogo comeca, em qualquer cena, para a musica de fundo
    /// tocar sem depender de alguem pedir um efeito primeiro.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Iniciar()
    {
        _ = Instancia;
    }

    private static GerenciadorDeAudio Instancia
    {
        get
        {
            if (instancia != null)
            {
                return instancia;
            }

            // Nao existe em cena: cria na hora, para nenhuma cena precisar de preparo manual.
            var go = new GameObject("GerenciadorDeAudio");
            instancia = go.AddComponent<GerenciadorDeAudio>();
            return instancia;
        }
    }

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        instancia = this;
        DontDestroyOnLoad(gameObject);

        if (banco == null)
        {
            banco = Resources.Load<BancoDeSons>(CaminhoBanco);
        }

        fonteMusica = gameObject.AddComponent<AudioSource>();
        fonteMusica.loop = true;
        fonteMusica.playOnAwake = false;
        fonteMusica.volume = volumeMusica;

        fonteEfeitos = gameObject.AddComponent<AudioSource>();
        fonteEfeitos.playOnAwake = false;
        fonteEfeitos.volume = volumeEfeitos;

        PrepararOuvinte();
        SceneManager.sceneLoaded += AoCarregarCena;
        AtualizarMusica(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (instancia == this)
        {
            SceneManager.sceneLoaded -= AoCarregarCena;
        }
    }

    private void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        AtualizarMusica(cena.name);
        PrepararOuvinte();
    }

    /// <summary>
    /// Poe o ouvinte do audio no proprio GerenciadorDeAudio e desliga o da camera.
    ///
    /// A camera fica 10 unidades atras no eixo Z e adiantada em X, entao usa-la como ouvinte
    /// faz um som a 2 un do jogador parecer estar a mais de 20: tudo saia quase mudo. O ouvinte
    /// passa a seguir o jogador, no mesmo plano do jogo.
    /// </summary>
    private void PrepararOuvinte()
    {
        foreach (AudioListener outro in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
        {
            if (outro.gameObject != gameObject)
            {
                outro.enabled = false;
            }
        }

        if (GetComponent<AudioListener>() == null)
        {
            gameObject.AddComponent<AudioListener>();
        }

        ouvinte = transform;
    }

    private void LateUpdate()
    {
        if (ouvinte == null)
        {
            return;
        }

        // Segue o jogador; sem jogador em cena (menu), fica onde a camera aponta.
        GameObject alvo = GameObject.FindGameObjectWithTag("Player");

        if (alvo != null)
        {
            ouvinte.position = new Vector3(alvo.transform.position.x, alvo.transform.position.y, 0f);
        }
        else if (Camera.main != null)
        {
            Vector3 c = Camera.main.transform.position;
            ouvinte.position = new Vector3(c.x, c.y, 0f);
        }
    }

    /// <summary>Escolhe a musica pelo nome da cena e so troca se for diferente da atual.</summary>
    private void AtualizarMusica(string nomeCena)
    {
        if (banco == null)
        {
            return;
        }

        // Entrar numa fase significa partida nova: a musica final deixa de valer.
        if (nomeCena != null && nomeCena.StartsWith("Phase"))
        {
            modoFinal = false;
        }

        AudioClip desejada = null;

        if (nomeCena == "MainMenu")
        {
            // Depois de zerar, o menu recebe a musica final; nas demais vezes, a do menu.
            desejada = modoFinal && banco.musicaFinal != null ? banco.musicaFinal : banco.musicaMenu;
        }
        else if (nomeCena != null && nomeCena.StartsWith("Phase")
            && int.TryParse(nomeCena.Substring("Phase".Length), out int numero)
            && banco.musicasFases != null
            && numero >= 1 && numero <= banco.musicasFases.Length)
        {
            desejada = banco.musicasFases[numero - 1];
        }

        if (desejada == musicaAtual)
        {
            return;   // mesma musica: deixa tocando sem reiniciar
        }

        musicaAtual = desejada;
        fonteMusica.Stop();
        fonteMusica.clip = desejada;

        if (desejada != null)
        {
            fonteMusica.Play();
        }
    }

    /// <summary>
    /// Toca um efeito, escolhido a partir do banco. Exemplo:
    /// GerenciadorDeAudio.Tocar(b =&gt; b.pulo);
    /// </summary>
    public static void Tocar(System.Func<BancoDeSons, AudioClip> escolher, float volume = 1f)
    {
        GerenciadorDeAudio g = Instancia;

        if (g == null || g.banco == null || g.fonteEfeitos == null)
        {
            return;
        }

        AudioClip clipe = escolher(g.banco);

        if (clipe == null)
        {
            return;
        }

        float pular = g.InicioReal(clipe);

        if (pular <= 0.001f)
        {
            g.fonteEfeitos.PlayOneShot(clipe, Mathf.Clamp01(volume) * g.volumeEfeitos);
            return;
        }

        // PlayOneShot nao permite comecar no meio: usa uma fonte propria e descartavel.
        g.TocarComOffset(clipe, volume, pular, null);
    }

    /// <summary>
    /// Toca um efeito NO LUGAR onde ele acontece: o volume cai com a distancia ate a camera e
    /// some de vez alem de distanciaMaxima. Use para sons do cenario (pedra, caixa, trampolim,
    /// inimigos), para nao se ouvir o outro lado da fase.
    /// </summary>
    public static void TocarEm(Vector3 posicao, System.Func<BancoDeSons, AudioClip> escolher, float volume = 1f)
    {
        GerenciadorDeAudio g = Instancia;

        if (g == null || g.banco == null)
        {
            return;
        }

        AudioClip clipe = escolher(g.banco);

        if (clipe == null)
        {
            return;
        }

        g.TocarComOffset(clipe, volume, g.InicioReal(clipe), posicao);
    }

    /// <summary>
    /// Toca um clipe numa fonte descartavel, comecando depois do silencio inicial. Com posicao,
    /// o som e 3D (perde volume com a distancia); sem posicao, toca igual para o jogador todo.
    /// </summary>
    private void TocarComOffset(AudioClip clipe, float volume, float pular, Vector3? posicao)
    {
        var go = new GameObject("Som_" + clipe.name);
        var fonte = go.AddComponent<AudioSource>();
        fonte.clip = clipe;
        fonte.volume = Mathf.Clamp01(volume) * volumeEfeitos;
        fonte.dopplerLevel = 0f;

        if (posicao.HasValue)
        {
            Vector3 destino = posicao.Value;
            destino.z = 0f;                            // mesmo plano do ouvinte
            go.transform.position = destino;
            fonte.spatialBlend = 1f;                   // 3D: depende da distancia
            fonte.rolloffMode = AudioRolloffMode.Linear;
            fonte.minDistance = distanciaMinima;
            fonte.maxDistance = distanciaMaxima;
        }

        fonte.Play();

        if (pular > 0.001f && pular < clipe.length)
        {
            fonte.time = pular;                        // pula o trecho mudo do comeco
        }

        Destroy(go, clipe.length - pular + 0.2f);
    }

    /// <summary>
    /// Em que segundo o clipe realmente comeca a soar. Calculado uma vez por clipe e guardado.
    /// Analisa so o inicio do arquivo, o suficiente para achar a primeira amostra audivel.
    /// </summary>
    private float InicioReal(AudioClip clipe)
    {
        if (inicioReal.TryGetValue(clipe, out float valor))
        {
            return valor;
        }

        valor = 0f;

        // Analisa no maximo 1s de audio; alem disso nao vale a pena cortar.
        int amostrasParaLer = Mathf.Min(clipe.samples, clipe.frequency) * clipe.channels;

        if (clipe.loadState == AudioDataLoadState.Loaded && amostrasParaLer > 0)
        {
            var dados = new float[amostrasParaLer];

            if (clipe.GetData(dados, 0))
            {
                for (int i = 0; i < dados.Length; i++)
                {
                    if (Mathf.Abs(dados[i]) > 0.01f)
                    {
                        valor = (i / (float)clipe.channels) / clipe.frequency;
                        break;
                    }
                }
            }
        }

        // margem de 20 ms para nao cortar o ataque do som
        valor = Mathf.Max(0f, valor - 0.02f);
        inicioReal[clipe] = valor;
        return valor;
    }

    /// <summary>
    /// Comeca a musica final (chefe derrotado). Ela continua tocando na troca de cena e, ao
    /// voltar ao menu depois de zerar, segue no lugar da musica normal do menu.
    /// </summary>
    public static void TocarMusicaFinal()
    {
        GerenciadorDeAudio g = Instancia;

        if (g == null || g.banco == null || g.banco.musicaFinal == null)
        {
            return;
        }

        g.modoFinal = true;

        if (g.musicaAtual == g.banco.musicaFinal)
        {
            return;   // ja esta tocando: nao reinicia
        }

        g.musicaAtual = g.banco.musicaFinal;
        g.fonteMusica.Stop();
        g.fonteMusica.clip = g.banco.musicaFinal;
        g.fonteMusica.Play();
    }

    /// <summary>Silencia a musica (usado no final do jogo, por exemplo).</summary>
    public static void PararMusica()
    {
        if (instancia != null && instancia.fonteMusica != null)
        {
            instancia.fonteMusica.Stop();
            instancia.musicaAtual = null;
        }
    }
}
