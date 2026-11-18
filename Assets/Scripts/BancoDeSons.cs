using UnityEngine;

/// <summary>
/// Lista dos sons do jogo. Fica em Assets/Resources/BancoDeSons.asset e e carregado pelo
/// GerenciadorDeAudio. Troque um clipe aqui para mudar o som do evento correspondente.
/// </summary>
[CreateAssetMenu(fileName = "BancoDeSons", menuName = "Jogo/Banco de Sons")]
public class BancoDeSons : ScriptableObject
{
    [Header("Jogador")]
    public AudioClip pulo;
    [Tooltip("Tocado quando o Wilson morre (usa o jingle de game over).")]
    public AudioClip morte;

    [Header("Coleta")]
    public AudioClip fruta;
    public AudioClip macaDourada;

    [Header("Inimigos")]
    public AudioClip pisada;
    public AudioClip explosao;
    public AudioClip tiro;

    [Header("Caixas")]
    public AudioClip batida;
    public AudioClip quebra;

    [Header("Armadilhas")]
    public AudioClip trampolim;
    public AudioClip pedra;
    public AudioClip plataformaCaindo;
    [Tooltip("Zumbido da serra, em loop.")]
    public AudioClip serra;
    [Tooltip("Jato de fogo da armadilha; tambem usado na bola de fogo do dragao.")]
    public AudioClip fogo;

    [Header("Chefe")]
    [Tooltip("Passos do dragao enquanto caminha.")]
    public AudioClip passos;
    [Tooltip("Passarinhos rodando a cabeca: toca enquanto o dragao esta tonto.")]
    public AudioClip passaros;

    [Header("Fase")]
    public AudioClip vitoria;

    [Header("Musicas (em loop)")]
    public AudioClip musicaMenu;
    [Tooltip("Uma por fase, na ordem: Fase 1, 2, 3, 4 e 5.")]
    public AudioClip[] musicasFases;
    [Tooltip("Toca quando o chefe cai e segue no menu depois de zerar o jogo.")]
    public AudioClip musicaFinal;
}
