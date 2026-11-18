using UnityEngine;

/// <summary>
/// Bandeira de fim de fase. Tocar nela com o jogador vence a fase atual, desde que
/// todas as macas douradas da fase ja tenham sido coletadas; senao, o HUD avisa
/// quantas faltam. A animacao de bandeira tremulando fica num filho com Animator.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LevelFlag : MonoBehaviour
{
    [Tooltip("Intervalo minimo entre dois avisos de macas faltando, em segundos.")]
    [SerializeField] private float intervaloAviso = 2.5f;

    private bool ativada;
    private float proximoAviso;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Tocar(other);
    }

    // Se a ultima maca for pega com o jogador ainda dentro da bandeira, a vitoria
    // acontece na hora, sem precisar sair e entrar de novo.
    private void OnTriggerStay2D(Collider2D other)
    {
        Tocar(other);
    }

    private void Tocar(Collider2D other)
    {
        if (ativada)
        {
            return;
        }

        PlayerController jogador = other.GetComponent<PlayerController>();

        if (jogador == null)
        {
            return;
        }

        // Sem todas as macas douradas a fase esta perdida: como a onda de destruicao ja comeu o
        // caminho de volta, nao ha como buscar a que faltou. Avisa e encerra em game over.
        if (ScoreManager.Instance != null && !ScoreManager.Instance.TodasMacasColetadas)
        {
            ativada = true;
            ScoreManager.Instance.AvisarFaltamMacas();
            jogador.Morrer();
            return;
        }

        ativada = true;

        // A musica da fase sai de cena para o jingle de vitoria ficar sozinho; ela so volta
        // quando a proxima fase carregar.
        GerenciadorDeAudio.PararMusica();
        GerenciadorDeAudio.Tocar(b => b.vitoria, 0.8f);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.IniciarChegada(jogador);
        }
    }
}
