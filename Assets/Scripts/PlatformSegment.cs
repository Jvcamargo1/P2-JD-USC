using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Um pedaco do cenario (chunk de terreno, plataforma ou bloco).
/// Comeca estatico e vira dinamico (cai) quando a onda de destruicao o alcanca.
/// Obstaculos que sao filhos do segmento caem junto com ele.
///
/// Quando o segmento e um Tilemap, gera a colisao ja no Awake. Sem isso, a
/// geometria do terreno pode nao existir no primeiro passo de fisica e o
/// jogador atravessaria o chao ao comecar a fase.
/// </summary>
public class PlatformSegment : MonoBehaviour
{
    [Header("Queda")]
    [Tooltip("Forca de rotacao aplicada ao cair, para dar sensacao de desabamento.")]
    [SerializeField] private float torqueAoCair = 25f;
    [Tooltip("Segundos ate o segmento ser destruido depois de comecar a cair.")]
    [SerializeField] private float tempoAteDestruir = 4f;

    private Rigidbody2D rb;
    private bool caindo;

    public bool Caindo => caindo;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        // Plataformas moveis ja sao cinematicas; as demais comecam estaticas.
        if (rb.bodyType != RigidbodyType2D.Kinematic)
        {
            rb.bodyType = RigidbodyType2D.Static;
        }

        GerarColisaoDoTilemap();
    }

    /// <summary>
    /// Forca a geracao imediata da colisao de um chunk de Tilemap.
    /// Religar o TilemapCollider2D e o que faz o composto reconstruir a
    /// geometria a partir dos tiles ja carregados.
    /// </summary>
    private void GerarColisaoDoTilemap()
    {
        TilemapCollider2D colisorTiles = GetComponent<TilemapCollider2D>();

        if (colisorTiles == null)
        {
            return;
        }

        colisorTiles.enabled = false;
        colisorTiles.enabled = true;
        colisorTiles.ProcessTilemapChanges();

        CompositeCollider2D composto = GetComponent<CompositeCollider2D>();

        if (composto != null)
        {
            composto.GenerateGeometry();
        }
    }

    /// <summary>
    /// Ativa a fisica do segmento, fazendo-o desabar.
    /// Chamado pela onda de destruicao.
    /// </summary>
    public void Desabar()
    {
        if (caindo)
        {
            return;
        }

        caindo = true;

        // Interrompe o movimento proprio (plataformas moveis) para a gravidade assumir.
        MovingFallingPlatform movel = GetComponent<MovingFallingPlatform>();
        if (movel != null)
        {
            movel.enabled = false;
        }

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 2.5f;
        rb.AddTorque(Random.Range(-torqueAoCair, torqueAoCair));

        // Desliga a colisao para o jogador nao se apoiar em escombros.
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        foreach (Collider2D col in colliders)
        {
            col.enabled = false;
        }

        // Derruba junto os obstaculos que estavam sobre o segmento.
        foreach (Transform filho in transform)
        {
            Obstacle obstaculo = filho.GetComponent<Obstacle>();
            if (obstaculo != null)
            {
                obstaculo.Desabar();
            }
        }

        Destroy(gameObject, tempoAteDestruir);
    }
}
