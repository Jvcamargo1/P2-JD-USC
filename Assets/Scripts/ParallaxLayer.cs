using UnityEngine;

/// <summary>
/// Camada de parallax com repeticao infinita na horizontal.
///
/// A camada e formada por copias do mesmo sprite lado a lado. Conforme a camera
/// anda, a camada acompanha apenas uma fracao do movimento e salta uma largura
/// inteira quando necessario, o que da a sensacao de profundidade sem emendas.
/// Roda depois da camera para nao tremer.
/// </summary>
[DefaultExecutionOrder(100)]
public class ParallaxLayer : MonoBehaviour
{
    [Tooltip("Fracao do movimento da camera que a camada acompanha. " +
             "1 = fixa na tela (ceu distante); valores menores = mais perto, rola mais rapido.")]
    [Range(0f, 1f)]
    [SerializeField] private float acompanhaCamera = 0.5f;

    [Tooltip("Deslocamento vertical em relacao a camera, em unidades.")]
    [SerializeField] private float deslocamentoY = 0f;

    [Tooltip("Fase em torre: a camada desce na tela conforme a camera sobe (rolagem vertical).")]
    [SerializeField] private bool modoVertical;

    private Transform cam;
    private float larguraCopia;
    private float yInicialCamera;

    private void Start()
    {
        if (Camera.main != null)
        {
            cam = Camera.main.transform;
            yInicialCamera = cam.position.y;
        }

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();

        if (sr != null)
        {
            larguraCopia = sr.bounds.size.x;
        }
    }

    private void LateUpdate()
    {
        if (cam == null || larguraCopia <= 0f)
        {
            return;
        }

        // Quanto o conteudo da camada ja "andou" em relacao a camera.
        float rolagem = cam.position.x * (1f - acompanhaCamera);
        float x = cam.position.x - Mathf.Repeat(rolagem, larguraCopia);

        float y = cam.position.y + deslocamentoY;

        if (modoVertical)
        {
            y -= (cam.position.y - yInicialCamera) * (1f - acompanhaCamera);
        }

        transform.position = new Vector3(x, y, transform.position.z);
    }
}
