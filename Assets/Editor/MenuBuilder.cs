using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Monta a cena do menu inicial (Assets/Scenes/MainMenu.unity) e deixa o Build Settings na ordem certa:
/// menu primeiro, depois as fases. Se existir Assets/UI/Menu_Fundo.png ele vira o fundo; se nao, usa um
/// fundo provisorio gerado por codigo. Menu: Tools/Menu/Reconstruir Menu Principal.
/// </summary>
public static class MenuBuilder
{
    private const string CenaMenu = "Assets/Scenes/MainMenu.unity";
    private const string ImagemFundo = "Assets/UI/Menu_Fundo.png";
    private const string ImagemProvisoria = "Assets/UI/Menu_Fundo_Provisorio.png";
    private const string Fonte = "Assets/Fonts/PressStart2P SDF.asset";
    private const string MaterialContorno = "Assets/Fonts/PressStart2P SDF - Contorno.mat";
    private const string Painel = "Assets/UI/UI_Painel.png";
    private const string MaterialVento = "Assets/UI/Menu_Vento.mat";
    private const int TotalDeFases = 4;

    private static readonly Color Ouro = new Color(1f, 0.84f, 0.29f, 1f);
    private static readonly Color Creme = new Color(0.96f, 0.92f, 0.80f, 1f);
    private static readonly Color FundoBotao = new Color(0.06f, 0.07f, 0.16f, 0.88f);

    [MenuItem("Tools/Menu/Reconstruir Menu Principal")]
    public static void MenuConstruir()
    {
        Debug.Log(Construir());
    }

    public static string Construir()
    {
        if (Application.isPlaying)
        {
            return "ERRO: pare o Play Mode antes de reconstruir o menu.";
        }

        var ativa = EditorSceneManager.GetActiveScene();

        if (ativa.isDirty && ativa.path != CenaMenu)
        {
            return "ERRO: a cena '" + ativa.name + "' tem alteracoes nao salvas. Salve antes; nada foi feito.";
        }

        TMP_FontAsset fonte = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonte);
        Material contorno = AssetDatabase.LoadAssetAtPath<Material>(MaterialContorno);
        Sprite painel = AssetDatabase.LoadAssetAtPath<Sprite>(Painel);

        if (fonte == null || painel == null)
        {
            return "ERRO: nao achei a fonte Press Start 2P ou o UI_Painel; nada foi feito.";
        }

        var relatorio = new StringBuilder();
        Sprite imagem = PrepararFundo(out bool provisoria, out float proporcao, relatorio);

        var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // camera so para o Unity nao reclamar de "no cameras rendering"
        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = true;

        var eventos = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // canvas
        var raiz = new GameObject("Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MenuPrincipal));
        var canvas = raiz.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var escala = raiz.GetComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.matchWidthOrHeight = 0.5f;
        var menu = raiz.GetComponent<MenuPrincipal>();

        // fundo
        var fundoGo = new GameObject("Fundo", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        fundoGo.transform.SetParent(raiz.transform, false);
        var fundoImg = fundoGo.GetComponent<RawImage>();
        fundoImg.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GetAssetPath(imagem));
        fundoImg.raycastTarget = false;

        // vento (bandana, capim, copa e agua): so na arte real, porque as regioes do shader foram medidas nela
        Material vento = provisoria ? null : AssetDatabase.LoadAssetAtPath<Material>(MaterialVento);

        if (vento != null)
        {
            fundoImg.material = vento;
        }

        relatorio.AppendLine("vento: " + (vento != null ? "material Menu_Vento aplicado" : "desligado (" + (provisoria ? "fundo provisorio" : "material nao encontrado") + ")"));
        var fitter = fundoGo.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = proporcao;
        Esticar(Ret(fundoGo));

        // titulo e subtitulo
        TextMeshProUGUI titulo = Texto(raiz.transform, "Titulo", "SAPO\nEM FUGA", 104f, Ouro, TextAlignmentOptions.TopRight, fonte, contorno);
        titulo.lineSpacing = 10f;
        Ajustar(Ret(titulo.gameObject), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-100f, -80f), new Vector2(900f, 250f));

        // faixa escura atras do subtitulo, para ele ler bem sobre qualquer fundo
        var faixaGo = new GameObject("FaixaSubtitulo", typeof(RectTransform), typeof(Image));
        faixaGo.transform.SetParent(raiz.transform, false);
        Ajustar(Ret(faixaGo), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-84f, -342f), new Vector2(700f, 56f));
        var faixaImg = faixaGo.GetComponent<Image>();
        faixaImg.sprite = painel;
        faixaImg.type = Image.Type.Sliced;
        faixaImg.color = new Color(0.03f, 0.04f, 0.10f, 0.62f);
        faixaImg.raycastTarget = false;

        TextMeshProUGUI subtitulo = Texto(raiz.transform, "Subtitulo", "CORRA ANTES QUE TUDO DESMORONE", 22f, Creme, TextAlignmentOptions.TopRight, fonte, contorno);
        Ajustar(Ret(subtitulo.gameObject), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-104f, -358f), new Vector2(900f, 40f));

        TextMeshProUGUI rodape = Texto(raiz.transform, "Dica", "SETAS + ENTER OU MOUSE   |   ESC VOLTA", 18f, new Color(Creme.r, Creme.g, Creme.b, 0.85f), TextAlignmentOptions.BottomLeft, fonte, contorno);
        Ajustar(Ret(rodape.gameObject), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 40f), new Vector2(900f, 30f));

        // painel principal: JOGAR / FASES / SAIR
        var principal = new GameObject("PainelPrincipal", typeof(RectTransform), typeof(VerticalLayoutGroup));
        principal.transform.SetParent(raiz.transform, false);
        Ajustar(Ret(principal), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-90f, 70f), new Vector2(440f, 268f));
        var vlg = principal.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 20f;
        vlg.childAlignment = TextAnchor.LowerCenter;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        Button botaoJogar = Botao(principal.transform, "BotaoJogar", "JOGAR", new Vector2(440f, 76f), 36f, painel, fonte, contorno, out _);
        Button botaoFases = Botao(principal.transform, "BotaoFases", "FASES", new Vector2(440f, 76f), 36f, painel, fonte, contorno, out _);
        Button botaoSair = Botao(principal.transform, "BotaoSair", "SAIR", new Vector2(440f, 76f), 36f, painel, fonte, contorno, out _);

        // painel de fases
        var fases = new GameObject("PainelFases", typeof(RectTransform), typeof(Image));
        fases.transform.SetParent(raiz.transform, false);
        Esticar(Ret(fases));
        fases.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);

        var caixa = new GameObject("Caixa", typeof(RectTransform), typeof(Image));
        caixa.transform.SetParent(fases.transform, false);
        Ajustar(Ret(caixa), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040f, 800f));
        var caixaImg = caixa.GetComponent<Image>();
        caixaImg.sprite = painel;
        caixaImg.type = Image.Type.Sliced;
        caixaImg.color = new Color(0.05f, 0.06f, 0.14f, 0.96f);

        TextMeshProUGUI tituloFases = Texto(caixa.transform, "Titulo", "SELECIONE A FASE", 44f, Ouro, TextAlignmentOptions.Center, fonte, contorno);
        Ajustar(Ret(tituloFases.gameObject), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(940f, 70f));

        var lista = new GameObject("Lista", typeof(RectTransform), typeof(VerticalLayoutGroup));
        lista.transform.SetParent(caixa.transform, false);
        Ajustar(Ret(lista), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(900f, 516f));
        var vlgLista = lista.GetComponent<VerticalLayoutGroup>();
        vlgLista.spacing = 14f;
        vlgLista.childAlignment = TextAnchor.UpperCenter;
        vlgLista.childControlWidth = false;
        vlgLista.childControlHeight = false;
        vlgLista.childForceExpandWidth = false;
        vlgLista.childForceExpandHeight = false;

        var botoesFase = new Button[TotalDeFases];
        var textosRecorde = new TMP_Text[TotalDeFases];

        for (int i = 0; i < TotalDeFases; i++)
        {
            botoesFase[i] = Botao(lista.transform, "BotaoFase" + (i + 1), "FASE " + (i + 1), new Vector2(900f, 92f), 36f, painel, fonte, contorno, out TMP_Text rotulo);
            var rotuloRt = Ret(rotulo.gameObject);
            Ajustar(rotuloRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(360f, 60f));
            rotulo.alignment = TextAlignmentOptions.Left;

            TextMeshProUGUI recorde = Texto(botoesFase[i].transform, "Recorde", "SEM RECORDE", 22f, Creme, TextAlignmentOptions.Right, fonte, contorno);
            Ajustar(Ret(recorde.gameObject), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(480f, 60f));
            textosRecorde[i] = recorde;
        }

        Button botaoVoltar = Botao(caixa.transform, "BotaoVoltar", "VOLTAR", new Vector2(360f, 72f), 34f, painel, fonte, contorno, out _);
        Ajustar(Ret(botaoVoltar.gameObject), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(360f, 72f));

        // fade (ultimo = por cima de tudo)
        var fadeGo = new GameObject("Fade", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        fadeGo.transform.SetParent(raiz.transform, false);
        Esticar(Ret(fadeGo));
        fadeGo.GetComponent<Image>().color = Color.black;
        var fade = fadeGo.GetComponent<CanvasGroup>();
        fade.alpha = 1f;
        fade.blocksRaycasts = false;
        fade.interactable = false;

        // ligacoes do componente
        var so = new SerializedObject(menu);
        so.FindProperty("painelPrincipal").objectReferenceValue = principal;
        so.FindProperty("painelFases").objectReferenceValue = fases;
        so.FindProperty("botaoJogar").objectReferenceValue = botaoJogar;
        so.FindProperty("botaoFases").objectReferenceValue = botaoFases;
        so.FindProperty("botaoVoltar").objectReferenceValue = botaoVoltar;
        so.FindProperty("fade").objectReferenceValue = fade;
        so.FindProperty("fundo").objectReferenceValue = Ret(fundoGo);
        so.FindProperty("titulo").objectReferenceValue = Ret(titulo.gameObject);

        var arrBotoes = so.FindProperty("botoesFase");
        var arrTextos = so.FindProperty("textosRecorde");
        arrBotoes.arraySize = TotalDeFases;
        arrTextos.arraySize = TotalDeFases;

        for (int i = 0; i < TotalDeFases; i++)
        {
            arrBotoes.GetArrayElementAtIndex(i).objectReferenceValue = botoesFase[i];
            arrTextos.GetArrayElementAtIndex(i).objectReferenceValue = textosRecorde[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddPersistentListener(botaoJogar.onClick, menu.Jogar);
        UnityEventTools.AddPersistentListener(botaoFases.onClick, menu.AbrirFases);
        UnityEventTools.AddPersistentListener(botaoSair.onClick, menu.Sair);
        UnityEventTools.AddPersistentListener(botaoVoltar.onClick, menu.Voltar);

        for (int i = 0; i < TotalDeFases; i++)
        {
            UnityEventTools.AddStringPersistentListener(botoesFase[i].onClick, menu.CarregarFase, "Phase" + (i + 1));
        }

        fases.SetActive(false);

        EditorSceneManager.SaveScene(cena, CenaMenu);
        AtualizarBuild(relatorio);

        relatorio.Insert(0, "Menu montado em " + CenaMenu + (provisoria ? " (com o FUNDO PROVISORIO)" : " (com a sua imagem de fundo)") + "\n");
        return relatorio.ToString();
    }

    // ---------------------------------------------------------------- fundo

    private static Sprite PrepararFundo(out bool provisoria, out float proporcao, StringBuilder relatorio)
    {
        string caminho;

        if (File.Exists(ImagemFundo))
        {
            caminho = ImagemFundo;
            provisoria = false;

            if (File.Exists(ImagemProvisoria))
            {
                AssetDatabase.DeleteAsset(ImagemProvisoria);
            }
        }
        else
        {
            GerarImagemProvisoria();
            caminho = ImagemProvisoria;
            provisoria = true;
        }

        var importador = (TextureImporter)AssetImporter.GetAtPath(caminho);
        importador.textureType = TextureImporterType.Sprite;
        importador.spriteImportMode = SpriteImportMode.Single;
        importador.mipmapEnabled = false;
        importador.alphaIsTransparency = false;
        importador.filterMode = provisoria ? FilterMode.Point : FilterMode.Bilinear;
        importador.textureCompression = TextureImporterCompression.Uncompressed;
        importador.maxTextureSize = 4096;
        importador.SaveAndReimport();

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
        proporcao = sprite.rect.width / sprite.rect.height;
        relatorio.AppendLine("fundo: " + caminho + " (" + sprite.rect.width + "x" + sprite.rect.height + ", proporcao " + proporcao.ToString("0.00") + ", filtro " + importador.filterMode + ")");
        return sprite;
    }

    private static Color H(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    private static void GerarImagemProvisoria()
    {
        const int L = 480;
        const int A = 270;
        const int horizonte = 150;

        Color c0 = H("#1c2552"), c1 = H("#5b4a9a"), c2 = H("#e0704f"), c3 = H("#ffc766");
        Color montFundo = H("#6a4f8f"), montPerto = H("#3e3a68");
        Color aguaTopo = H("#d97a4a"), aguaBase = H("#1f2a55");
        Color sol = H("#ffe9a0"), brilho = H("#ffd27a");
        Color tronco = H("#1b1209"), folha = H("#1d3b17"), grama = H("#10241a");

        var tex = new Texture2D(L, A, TextureFormat.RGBA32, false);
        var pix = new Color[L * A];

        for (int yt = 0; yt < A; yt++)
        {
            for (int x = 0; x < L; x++)
            {
                Color cor;

                if (yt < horizonte)
                {
                    float t = Mathf.Floor(yt / (float)horizonte * 14f) / 14f;
                    cor = t < 0.45f ? Color.Lerp(c0, c1, t / 0.45f) : t < 0.8f ? Color.Lerp(c1, c2, (t - 0.45f) / 0.35f) : Color.Lerp(c2, c3, (t - 0.8f) / 0.2f);

                    float dx = x - 326f, dy = yt - 126f;

                    if (dx * dx + dy * dy <= 26f * 26f)
                    {
                        cor = sol;
                    }

                    float altFundo = 20f + 14f * Mathf.Sin(x * 0.021f + 1f) + 8f * Mathf.Sin(x * 0.057f);

                    if (yt >= horizonte - altFundo)
                    {
                        cor = montFundo;
                    }

                    float altPerto = 34f + 18f * Mathf.Sin(x * 0.013f + 2f) + 6f * Mathf.Sin(x * 0.07f);

                    if (x < 260 && yt >= horizonte - altPerto)
                    {
                        cor = montPerto;
                    }
                }
                else
                {
                    float t = Mathf.Floor((yt - horizonte) / (float)(A - horizonte) * 12f) / 12f;
                    cor = Color.Lerp(aguaTopo, aguaBase, t);

                    float meia = 8f + (yt - horizonte) / 10f;

                    if (Mathf.Abs(x - 326f) <= meia && ((yt / 2) % 2 == 0))
                    {
                        cor = brilho;
                    }
                }

                // primeiro plano: tronco, folhas e grama a esquerda
                if (x < 44)
                {
                    cor = tronco;
                }

                if (yt < 90 && x < 240)
                {
                    float d1 = (x - 60f) * (x - 60f) + (yt - 36f) * (yt - 36f);
                    float d2 = (x - 150f) * (x - 150f) + (yt - 28f) * (yt - 28f);
                    float d3 = (x - 215f) * (x - 215f) + (yt - 56f) * (yt - 56f);

                    if (d1 < 70f * 70f || d2 < 58f * 58f || d3 < 34f * 34f)
                    {
                        cor = ((x / 6 + yt / 6) % 3 == 0) ? Color.Lerp(folha, Color.black, 0.35f) : folha;
                    }
                }

                if (x < 300 && yt >= 236f + 5f * Mathf.Sin(x * 0.09f))
                {
                    cor = grama;
                }

                pix[(A - 1 - yt) * L + x] = cor;
            }
        }

        tex.SetPixels(pix);
        tex.Apply();
        File.WriteAllBytes(ImagemProvisoria, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(ImagemProvisoria);
    }

    // ---------------------------------------------------------------- build settings

    private static void AtualizarBuild(StringBuilder relatorio)
    {
        var lista = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(CenaMenu, true) };

        for (int i = 1; i <= TotalDeFases; i++)
        {
            string caminho = "Assets/Scenes/Phase" + i + ".unity";

            if (File.Exists(caminho))
            {
                lista.Add(new EditorBuildSettingsScene(caminho, true));
            }
        }

        var antes = new List<string>();

        foreach (var s in EditorBuildSettings.scenes)
        {
            antes.Add(Path.GetFileNameWithoutExtension(s.path));
        }

        EditorBuildSettings.scenes = lista.ToArray();

        var depois = new List<string>();

        foreach (var s in lista)
        {
            depois.Add(Path.GetFileNameWithoutExtension(s.path));
        }

        relatorio.AppendLine("Build Settings antes: " + string.Join(", ", antes));
        relatorio.AppendLine("Build Settings agora: " + string.Join(", ", depois));
    }

    // ---------------------------------------------------------------- utilitarios de interface

    private static RectTransform Ret(GameObject go)
    {
        return (RectTransform)go.transform;
    }

    private static void Esticar(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    private static void Ajustar(RectTransform r, Vector2 ancoraMin, Vector2 ancoraMax, Vector2 pivo, Vector2 pos, Vector2 tamanho)
    {
        r.anchorMin = ancoraMin;
        r.anchorMax = ancoraMax;
        r.pivot = pivo;
        r.anchoredPosition = pos;
        r.sizeDelta = tamanho;
    }

    private static TextMeshProUGUI Texto(Transform pai, string nome, string texto, float tamanho, Color cor, TextAlignmentOptions alinhamento, TMP_FontAsset fonte, Material material)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(pai, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = fonte;

        if (material != null)
        {
            t.fontSharedMaterial = material;
        }

        t.text = texto;
        t.fontSize = tamanho;
        t.color = cor;
        t.alignment = alinhamento;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static Button Botao(Transform pai, string nome, string rotulo, Vector2 tamanho, float tamanhoFonte, Sprite painel, TMP_FontAsset fonte, Material contorno, out TMP_Text texto)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(Image), typeof(Button), typeof(MenuBotaoVisual));
        go.transform.SetParent(pai, false);
        Ret(go).sizeDelta = tamanho;

        var img = go.GetComponent<Image>();
        img.sprite = painel;
        img.type = Image.Type.Sliced;
        img.color = FundoBotao;

        var botao = go.GetComponent<Button>();
        botao.targetGraphic = img;
        botao.transition = Selectable.Transition.None;
        botao.navigation = new Navigation { mode = Navigation.Mode.Automatic };

        TextMeshProUGUI t = Texto(go.transform, "Rotulo", rotulo, tamanhoFonte, Creme, TextAlignmentOptions.Center, fonte, contorno);
        Esticar(Ret(t.gameObject));
        texto = t;

        var so = new SerializedObject(go.GetComponent<MenuBotaoVisual>());
        so.FindProperty("rotulo").objectReferenceValue = t;
        so.FindProperty("fundo").objectReferenceValue = img;
        so.ApplyModifiedPropertiesWithoutUndo();
        return botao;
    }
}
