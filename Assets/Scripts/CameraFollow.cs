using UnityEngine;

/// <summary>
/// Camera que segue o jogador com suavizacao, sem nunca recuar para a esquerda.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform alvo;
    [SerializeField] private Vector3 deslocamento = new Vector3(3f, 1f, -10f);
    [SerializeField] private float suavizacao = 0.15f;
    [Tooltip("Impede a camera de voltar para tras quando o jogador recua.")]
    [SerializeField] private bool travarRecuo = true;

    [Header("Modo torre (subida)")]
    [Tooltip("Fase em torre: a camera so sobe, fica centrada na torre e o recuo e travado em Y.")]
    [SerializeField] private bool modoVertical;
    [Tooltip("Modo torre: coordenada X fixa da camera (centro da torre).")]
    [SerializeField] private float xFixo;
    [Tooltip("Modo torre: quanto a camera pode descer abaixo da maior altura ja alcancada.")]
    [SerializeField] private float limiteDescida = 4.5f;

    private Vector3 velocidadeAtual;
    private float maiorX;
    private float maiorY;

    private void Start()
    {
        if (alvo != null)
        {
            maiorX = alvo.position.x;
            maiorY = alvo.position.y;
        }
    }

    private void LateUpdate()
    {
        if (alvo == null)
        {
            return;
        }

        if (modoVertical)
        {
            maiorY = Mathf.Max(maiorY, alvo.position.y);
            float alvoY = Mathf.Max(alvo.position.y, maiorY - limiteDescida);

            Vector3 destinoVertical = new Vector3(xFixo, alvoY + deslocamento.y, deslocamento.z);
            transform.position = Vector3.SmoothDamp(
                transform.position, destinoVertical, ref velocidadeAtual, suavizacao);
            return;
        }

        float alvoX = alvo.position.x;

        if (travarRecuo)
        {
            maiorX = Mathf.Max(maiorX, alvoX);
            alvoX = maiorX;
        }

        Vector3 destino = new Vector3(
            alvoX + deslocamento.x,
            alvo.position.y + deslocamento.y,
            deslocamento.z);

        transform.position = Vector3.SmoothDamp(
            transform.position, destino, ref velocidadeAtual, suavizacao);
    }
}
