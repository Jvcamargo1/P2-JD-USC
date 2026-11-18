using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Visual de um botao do menu: cor do texto, cor do fundo e escala mudam quando ele esta
/// selecionado (mouse por cima, setas ou controle) ou desativado (fase ainda nao disponivel).
/// </summary>
[RequireComponent(typeof(Button))]
public class MenuBotaoVisual : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    [SerializeField] private TMP_Text rotulo;
    [SerializeField] private Image fundo;
    [SerializeField] private Color corTexto = new Color(0.93f, 0.90f, 0.82f, 1f);
    [SerializeField] private Color corTextoSelecionado = new Color(1f, 0.84f, 0.29f, 1f);
    [SerializeField] private Color corTextoDesativado = new Color(0.52f, 0.54f, 0.62f, 1f);
    [SerializeField] private Color corFundo = new Color(0.06f, 0.07f, 0.16f, 0.88f);
    [SerializeField] private Color corFundoSelecionado = new Color(0.20f, 0.24f, 0.46f, 0.95f);
    [SerializeField] private Color corFundoDesativado = new Color(0.06f, 0.07f, 0.12f, 0.6f);
    [SerializeField] private float escalaSelecionado = 1.04f;

    private Button botao;
    private bool selecionado;

    private void Awake()
    {
        botao = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (botao == null)
        {
            botao = GetComponent<Button>();
        }

        selecionado = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        Atualizar();
    }

    public void OnSelect(BaseEventData eventData)
    {
        selecionado = true;
        Atualizar();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selecionado = false;
        Atualizar();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (botao != null && botao.interactable && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    public void Atualizar()
    {
        if (botao == null)
        {
            botao = GetComponent<Button>();
        }

        bool ativo = botao != null && botao.interactable;
        bool destaque = ativo && selecionado;

        if (rotulo != null)
        {
            rotulo.color = !ativo ? corTextoDesativado : destaque ? corTextoSelecionado : corTexto;
        }

        if (fundo != null)
        {
            fundo.color = !ativo ? corFundoDesativado : destaque ? corFundoSelecionado : corFundo;
        }

        transform.localScale = destaque ? Vector3.one * escalaSelecionado : Vector3.one;
    }
}
