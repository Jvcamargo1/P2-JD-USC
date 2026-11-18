using UnityEngine;

/// <summary>
/// Bola de fogo do dragao. Anda em linha reta, mata o jogador ao tocar e some ao bater numa
/// parede/chao (camada de chao) ou depois de um tempo maximo.
/// A parede e checada por sobreposicao (nao por evento de colisao) porque o corpo e cinematico.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ProjetilFogo : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Sprite[] quadros;
    [SerializeField] private float quadrosPorSegundo = 12f;
    [SerializeField] private LayerMask camadaChao;
    [Tooltip("Raio usado para detectar parede.")]
    [SerializeField] private float raioParede = 0.15f;
    [SerializeField] private float vidaMaxima = 6f;

    private Rigidbody2D rb;
    private Vector2 velocidade;
    private float nasceu;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        nasceu = Time.time;
    }

    /// <summary>Define a velocidade; o sprite vira para o lado do movimento.</summary>
    public void Disparar(Vector2 novaVelocidade)
    {
        velocidade = novaVelocidade;

        if (visual != null)
        {
            visual.flipX = velocidade.x < 0f;
        }
    }

    private void FixedUpdate()
    {
        rb.MovePosition(rb.position + (velocidade * Time.fixedDeltaTime));

        if (Physics2D.OverlapCircle(rb.position, raioParede, camadaChao) != null)
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (Time.time - nasceu > vidaMaxima)
        {
            Destroy(gameObject);
            return;
        }

        if (visual != null && quadros != null && quadros.Length > 0)
        {
            int i = Mathf.FloorToInt((Time.time - nasceu) * quadrosPorSegundo) % quadros.Length;
            visual.sprite = quadros[i];
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController jogador = other.GetComponentInParent<PlayerController>();

        if (jogador != null)
        {
            jogador.Morrer();
            Destroy(gameObject);
        }
    }
}
