using System;
using UnityEngine;

/// <summary>
/// Poder de lama do jogador.
///
/// A cada pontosParaCarregar pontos ganhos (frutas e inimigos) o poder fica
/// ativo por alguns segundos. Enquanto ativo, segurar X ou J dispara bolas de
/// lama na direcao em que o sapo olha, e o sapo pisca em tom de lama.
/// O medidor nao carrega com o poder ativo, para os pontos dos inimigos
/// derrotados com lama nao reativarem o poder sem parar.
/// </summary>
public class PoderLama : MonoBehaviour
{
    [Header("Recarga")]
    [Tooltip("Pontos ganhos necessarios para liberar o poder.")]
    [SerializeField] private int pontosParaCarregar = 2000;
    [Tooltip("Segundos que o poder fica ativo.")]
    [SerializeField] private float duracao = 8f;

    [Header("Tiro")]
    [SerializeField] private ProjetilLama prefabProjetil;
    [Tooltip("Intervalo entre disparos, segurando a tecla.")]
    [SerializeField] private float intervaloTiro = 0.3f;
    [Tooltip("Saida do tiro em relacao ao jogador (x e espelhado conforme a direcao).")]
    [SerializeField] private Vector2 pontoDisparo = new Vector2(0.45f, 0.05f);

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteJogador;
    [SerializeField] private Color corLama = new Color(0.78f, 0.55f, 0.35f);

    private int carga;
    private int ultimosPontos;
    private float restante;
    private float proximoTiro;
    private bool estavaAtivo;

    /// <summary>O poder esta liberado agora (ou o modo trapaca esta ligado).</summary>
    public bool Ativo => restante > 0f || CheatKonami.Ativo;

    /// <summary>Quanto do medidor esta cheio (0 a 1), com o poder inativo.</summary>
    public float Carga01 => Mathf.Clamp01((float)carga / pontosParaCarregar);

    /// <summary>Quanto do tempo do poder ainda resta (0 a 1).</summary>
    public float Restante01 => CheatKonami.Ativo ? 1f : Mathf.Clamp01(restante / duracao);

    /// <summary>Disparado quando o poder e liberado.</summary>
    public event Action OnAtivou;

    private void Start()
    {
        if (ScoreManager.Instance != null)
        {
            ultimosPontos = ScoreManager.Instance.Pontos;
        }
    }

    private void Update()
    {
        AtualizarCarga();

        bool rodando = GameManager.Instance == null ||
            GameManager.Instance.Estado == GameManager.GameState.Playing;

        bool trapaca = CheatKonami.Ativo;

        if (rodando && !trapaca && restante > 0f)
        {
            restante -= Time.deltaTime;
        }

        if (!Ativo)
        {
            // Acabou o poder (ou a trapaca foi desligada): volta a cor normal.
            if (estavaAtivo)
            {
                DefinirCor(Color.white);
                estavaAtivo = false;
            }

            return;
        }

        estavaAtivo = true;

        if (!rodando)
        {
            return;
        }

        if (trapaca)
        {
            // Modo trapaca: o sapo cicla pelas cores do arco-iris.
            DefinirCor(Color.HSVToRGB(Mathf.Repeat(Time.time * 0.6f, 1f), 0.45f, 1f));
        }
        else
        {
            // Pisca entre a cor normal e o tom de lama; mais rapido no ultimo 1,5 s.
            float frequencia = restante < 1.5f ? 18f : 8f;
            float mistura = (Mathf.Sin(Time.time * frequencia) + 1f) * 0.5f;
            DefinirCor(Color.Lerp(Color.white, corLama, mistura));
        }

        bool apertando = Input.GetKey(KeyCode.X) || Input.GetKey(KeyCode.J);

        if (apertando && Time.time >= proximoTiro)
        {
            Disparar();
        }
    }

    private void AtualizarCarga()
    {
        ScoreManager placar = ScoreManager.Instance;

        if (placar == null)
        {
            return;
        }

        int ganho = placar.Pontos - ultimosPontos;
        ultimosPontos = placar.Pontos;

        if (ganho <= 0 || Ativo)
        {
            return;
        }

        carga += ganho;

        if (carga >= pontosParaCarregar)
        {
            // Guarda o excedente para a proxima recarga.
            carga -= pontosParaCarregar;
            restante = duracao;
            OnAtivou?.Invoke();
        }
    }

    private void Disparar()
    {
        if (prefabProjetil == null)
        {
            return;
        }

        proximoTiro = Time.time + intervaloTiro;
        int direcao = spriteJogador != null && spriteJogador.flipX ? -1 : 1;
        Vector3 saida = transform.position + new Vector3(pontoDisparo.x * direcao, pontoDisparo.y, 0f);

        ProjetilLama projetil = Instantiate(prefabProjetil, saida, Quaternion.identity);
        projetil.Lancar(direcao);
    }

    private void DefinirCor(Color cor)
    {
        if (spriteJogador != null)
        {
            spriteJogador.color = cor;
        }
    }
}
