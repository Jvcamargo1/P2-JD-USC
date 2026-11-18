using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plataforma de esteira: anda sempre no mesmo sentido (so sobe ou so desce) e,
/// ao chegar na ponta, reaparece na outra ponta, como as esteiras de plataformas
/// das cavernas do Mario.
///
/// Varias delas em fila, na mesma coluna e defasadas, formam o fluxo continuo.
/// Quem esta em cima e carregado junto, menos no instante em que a plataforma
/// volta para a outra ponta: ali ela some de debaixo do jogador, que cai.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlataformaEsteira : MonoBehaviour
{
    public enum Sentido { Sobe, Desce }

    [Header("Movimento")]
    [SerializeField] private Sentido sentido = Sentido.Sobe;
    [SerializeField] private float velocidade = 1.6f;

    [Tooltip("Altura em que a plataforma reaparece (ponta de baixo do ciclo).")]
    [SerializeField] private float yMinimo;

    [Tooltip("Altura em que a plataforma some (ponta de cima do ciclo).")]
    [SerializeField] private float yMaximo;

    private Rigidbody2D rb;
    private readonly List<Rigidbody2D> passageiros = new List<Rigidbody2D>();

    /// <summary>Configura o ciclo no momento em que a fase e montada.</summary>
    public void Configurar(Sentido novoSentido, float baixo, float cima, float vel)
    {
        sentido = novoSentido;
        yMinimo = baixo;
        yMaximo = cima;
        velocidade = vel;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        // A esteira anda sozinha: desliga o vaivem da plataforma comum, se houver.
        MovingFallingPlatform comum = GetComponent<MovingFallingPlatform>();

        if (comum != null)
        {
            comum.enabled = false;
        }
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance != null &&
            GameManager.Instance.Estado != GameManager.GameState.Playing)
        {
            return;
        }

        if (yMaximo <= yMinimo)
        {
            return;
        }

        float passo = velocidade * Time.fixedDeltaTime * (sentido == Sentido.Sobe ? 1f : -1f);
        Vector2 posicao = rb.position;
        float novoY = posicao.y + passo;
        bool voltou = false;

        if (sentido == Sentido.Sobe && novoY > yMaximo)
        {
            novoY = yMinimo + (novoY - yMaximo);
            voltou = true;
        }
        else if (sentido == Sentido.Desce && novoY < yMinimo)
        {
            novoY = yMaximo - (yMinimo - novoY);
            voltou = true;
        }

        var destino = new Vector2(posicao.x, novoY);

        if (voltou)
        {
            // Reapareceu na outra ponta: teletransporte (sem MovePosition, que varreria quem esta
            // por perto a centenas de un/s). Quem estava em cima fica para tras.
            passageiros.Clear();
            rb.position = destino;
            transform.position = destino;
            return;
        }

        rb.MovePosition(destino);

        for (int i = passageiros.Count - 1; i >= 0; i--)
        {
            Rigidbody2D passageiro = passageiros[i];

            if (passageiro == null)
            {
                passageiros.RemoveAt(i);
                continue;
            }

            passageiro.position += new Vector2(0f, passo);
        }
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
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        Rigidbody2D outro = collision.rigidbody;

        if (outro != null)
        {
            passageiros.Remove(outro);
        }
    }

    /// <summary>Contato vindo de cima, para nao carregar quem bate de lado.</summary>
    private bool EstaEmCima(Collision2D collision)
    {
        foreach (ContactPoint2D contato in collision.contacts)
        {
            if (contato.normal.y < -0.5f)
            {
                return true;
            }
        }

        return false;
    }
}
