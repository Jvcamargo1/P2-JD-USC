using System.Collections;
using UnityEngine;

/// <summary>
/// Portao da arena do chefe: bloqueia a passagem para a sala da maca gigante e, quando o
/// dragao e derrotado, treme, some (fade) e libera o caminho.
/// </summary>
public class PortaoChefe : MonoBehaviour
{
    [SerializeField] private ChefeDragao chefe;
    [SerializeField] private float duracaoAbrir = 1f;
    [SerializeField] private float intensidadeTremor = 0.06f;

    private void OnEnable()
    {
        if (chefe != null)
        {
            chefe.OnDerrotado += Abrir;
        }
    }

    private void OnDisable()
    {
        if (chefe != null)
        {
            chefe.OnDerrotado -= Abrir;
        }
    }

    private void Abrir()
    {
        StartCoroutine(Abrindo());
    }

    private IEnumerator Abrindo()
    {
        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>();
        Vector3 posicaoBase = transform.position;
        float t = 0f;

        while (t < duracaoAbrir)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duracaoAbrir);
            transform.position = posicaoBase + (Vector3)(Random.insideUnitCircle * intensidadeTremor);

            foreach (SpriteRenderer s in sprites)
            {
                Color c = s.color;
                c.a = 1f - k;
                s.color = c;
            }

            yield return null;
        }

        transform.position = posicaoBase;
        gameObject.SetActive(false);
    }
}
