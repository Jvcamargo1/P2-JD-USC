using UnityEngine;

/// <summary>
/// Obstaculo estatico (Spikes, Saw, Spiked Ball) posicionado sobre as plataformas.
/// Mata o jogador ao encostar.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Obstacle : MonoBehaviour
{
    [Header("Rotacao opcional")]
    [Tooltip("Graus por segundo. Use em Saw e Spiked Ball; deixe 0 em Spikes.")]
    [SerializeField] private float velocidadeRotacao = 0f;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Update()
    {
        if (!Mathf.Approximately(velocidadeRotacao, 0f))
        {
            transform.Rotate(0f, 0f, velocidadeRotacao * Time.deltaTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>();

        if (player != null)
        {
            player.Morrer();
        }
    }

    /// <summary>
    /// Faz o obstaculo cair junto com o segmento que o sustentava.
    /// </summary>
    public void Desabar()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 2.5f;
    }
}
