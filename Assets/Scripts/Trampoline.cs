using UnityEngine;

/// <summary>
/// Trampolim: quando o jogador cai sobre ele, e lancado bem alto.
///
/// Usa a mesma convencao de normais das plataformas e caixas: no contato
/// recebido aqui, normal.y menor que -0.5 significa que o outro corpo esta
/// em cima. O impulso passa por PlayerController.Lancar(), que impede um
/// pulo comum de substituir o lancamento no quadro seguinte.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Trampoline : MonoBehaviour
{
    [Tooltip("Velocidade vertical dada ao jogador. 22 lanca a cerca de 7 un de altura.")]
    [SerializeField] private float impulso = 22f;
    [SerializeField] private Animator animator;

    private static readonly int ParamPular = Animator.StringToHash("Pular");

    private void OnCollisionEnter2D(Collision2D colisao)
    {
        PlayerController jogador = colisao.collider.GetComponent<PlayerController>();

        if (jogador == null)
        {
            return;
        }

        bool porCima = false;

        foreach (ContactPoint2D contato in colisao.contacts)
        {
            porCima |= contato.normal.y < -0.5f;
        }

        if (!porCima)
        {
            return;
        }

        jogador.Lancar(impulso);
        GerenciadorDeAudio.TocarEm(transform.position, b => b.trampolim, 0.7f);

        if (animator != null)
        {
            animator.SetTrigger(ParamPular);
        }
    }
}
