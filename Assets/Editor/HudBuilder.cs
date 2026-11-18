using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Monta o HUD em estilo pixel art e liga as referencias do UIManager.
///
///   - Painel de informacoes (fase e pontos), contador de frutas e barra de progresso
///   - Telas de Game Over e de Vitoria (com o detalhamento da pontuacao)
///
/// Gera tambem os recursos visuais: a moldura chanfrada (16x16, fatiada em 9 partes
/// e tingida por cor), o gradiente da barra e a fonte TMP da Press Start 2P com
/// contorno e sombra. Rode pelo menu Tools/HUD ou por HudBuilder.Construir().
///
/// A Press Start 2P e monoespacada e cada caractere ocupa 1 em de largura, entao
/// a largura de um texto e aproximadamente (numero de letras x tamanho da fonte).
/// Os tamanhos abaixo foram escolhidos com essa conta.
/// </summary>
public static class HudBuilder
{
    private const string PastaUI = "Assets/UI";
    private const string CaminhoPainel = "Assets/UI/UI_Painel.png";
    private const string CaminhoBarra = "Assets/UI/UI_Barra.png";
    private const string CaminhoTtf = "Assets/Fonts/PressStart2P-Regular.ttf";
    private const string CaminhoFonte = "Assets/Fonts/PressStart2P SDF.asset";
    private const string CaminhoMaterial = "Assets/Fonts/PressStart2P SDF - Contorno.mat";
    private const string NomeFonte = "PressStart2P SDF";

    private const string SpriteMaca = "Assets/Pixel Adventure 1/Assets/Items/Fruits/Apple.png";
    private const string SpriteSapo = "Assets/Pixel Adventure 1/Assets/Main Characters/Ninja Frog/Idle (32x32).png";
    private const string SpriteBandeira = "Assets/Pixel Adventure 1/Assets/Items/Checkpoints/Checkpoint/Checkpoint (Flag Idle)(64x64).png";

    // Cada pixel das molduras ocupa 4 px na resolucao de referencia (1920x1080).
    private const float EscalaPixel = 4f;

    private const string Caracteres =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
        "\u00c1\u00c0\u00c2\u00c3\u00c9\u00ca\u00cd\u00d3\u00d4\u00d5\u00da\u00dc\u00c7" +
        "\u00e1\u00e0\u00e2\u00e3\u00e9\u00ea\u00ed\u00f3\u00f4\u00f5\u00fa\u00fc\u00e7\u00d7";

    // Paleta
    private static readonly Color CorPainel = new Color(0.22f, 0.24f, 0.38f, 0.92f);
    private static readonly Color CorCaixa = new Color(0.22f, 0.24f, 0.38f, 0.98f);
    private static readonly Color CorTrilho = new Color(0.30f, 0.30f, 0.36f, 1f);
    private static readonly Color CorOnda = new Color(0.95f, 0.32f, 0.20f, 1f);
    private static readonly Color CorAmarelo = new Color(1f, 0.84f, 0.30f, 1f);
    private static readonly Color CorRotulo = new Color(0.75f, 0.79f, 0.92f, 1f);
    private static readonly Color CorVermelho = new Color(1f, 0.40f, 0.32f, 1f);
    private static readonly Color CorVerdeClaro = new Color(0.58f, 0.92f, 0.46f, 1f);
    private static readonly Color CorBotaoVerde = new Color(0.36f, 0.72f, 0.32f, 1f);
    private static readonly Color CorBotaoAzul = new Color(0.33f, 0.55f, 0.90f, 1f);
    private static readonly Color CorEscurecer = new Color(0f, 0f, 0f, 0.55f);

    private static TMP_FontAsset fonte;
    private static Material materialContorno;
    private static Sprite spritePainel;
    private static Sprite spriteBarra;
    private static int camadaUI;

    [MenuItem("Tools/HUD/Reconstruir HUD da cena")]
    private static void Menu()
    {
        Debug.Log(Construir());
    }

    // ------------------------------------------------------------------
    // Recursos (sprites, fonte e material)
    // ------------------------------------------------------------------

    /// <summary>
    /// Cria os recursos que ainda nao existem. Pode ser chamado varias vezes.
    /// </summary>
    public static string PrepararRecursos()
    {
        var rel = new StringBuilder();

        if (!AssetDatabase.IsValidFolder(PastaUI))
        {
            AssetDatabase.CreateFolder("Assets", "UI");
        }

        if (!File.Exists(CaminhoPainel))
        {
            GerarPainel();
            rel.AppendLine("sprite de moldura gerado");
        }

        if (!File.Exists(CaminhoBarra))
        {
            GerarBarra();
            rel.AppendLine("sprite de barra gerado");
        }

        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CaminhoFonte) == null)
        {
            rel.AppendLine(CriarFonte());
        }

        if (AssetDatabase.LoadAssetAtPath<Material>(CaminhoMaterial) == null)
        {
            CriarMaterialContorno();
            rel.AppendLine("material de texto com contorno criado");
        }

        return rel.Length == 0 ? "recursos ja existiam" : rel.ToString();
    }

    private static void GerarPainel()
    {
        const int n = 16;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        Color contorno = new Color(0.07f, 0.07f, 0.09f, 1f);
        Color luz = Color.white;
        Color sombra = new Color(0.58f, 0.58f, 0.58f, 1f);
        Color fundo = new Color(0.80f, 0.80f, 0.80f, 1f);

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                bool bordaX = x == 0 || x == n - 1;
                bool bordaY = y == 0 || y == n - 1;
                Color c;

                if (bordaX && bordaY)
                {
                    c = Color.clear;                    // cantos arredondados
                }
                else if (bordaX || bordaY)
                {
                    c = contorno;
                }
                else if (y == n - 2 || x == 1)
                {
                    c = luz;                            // brilho em cima e a esquerda
                }
                else if (y == 1 || x == n - 2)
                {
                    c = sombra;                         // sombra embaixo e a direita
                }
                else
                {
                    c = fundo;
                }

                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        File.WriteAllBytes(CaminhoPainel, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(CaminhoPainel);
        ConfigurarSprite(CaminhoPainel, new Vector4(2f, 2f, 2f, 2f));
    }

    private static void GerarBarra()
    {
        // Gradiente vertical em faixas (de baixo para cima): sombra, corpo, brilho.
        float[] linhas = { 0.62f, 0.62f, 0.84f, 0.84f, 0.84f, 0.84f, 1f, 1f };
        var tex = new Texture2D(4, linhas.Length, TextureFormat.RGBA32, false);

        for (int y = 0; y < linhas.Length; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                tex.SetPixel(x, y, new Color(linhas[y], linhas[y], linhas[y], 1f));
            }
        }

        tex.Apply();
        File.WriteAllBytes(CaminhoBarra, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(CaminhoBarra);
        ConfigurarSprite(CaminhoBarra, Vector4.zero);
    }

    private static void ConfigurarSprite(string caminho, Vector4 borda)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.spritePixelsPerUnit = 100f;
        imp.spriteBorder = borda;
        imp.SaveAndReimport();
    }

    private static string CriarFonte()
    {
        Font ttf = AssetDatabase.LoadAssetAtPath<Font>(CaminhoTtf);

        if (ttf == null)
        {
            return "ERRO: fonte nao encontrada em " + CaminhoTtf;
        }

        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(
            ttf, 80, 8, GlyphRenderMode.SDFAA, 2048, 1024, AtlasPopulationMode.Dynamic, false);

        fa.name = NomeFonte;
        AssetDatabase.CreateAsset(fa, CaminhoFonte);

        fa.atlasTextures[0].name = NomeFonte + " Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);

        fa.material.name = NomeFonte + " Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);

        fa.TryAddCharacters(Caracteres, out string faltando);
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();

        return "fonte TMP criada (" + fa.characterTable.Count + " caracteres"
            + (string.IsNullOrEmpty(faltando) ? ")" : ", sem: " + faltando + ")");
    }

    private static void CriarMaterialContorno()
    {
        TMP_FontAsset fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CaminhoFonte);
        var mat = new Material(fa.material) { name = NomeFonte + " - Contorno" };

        // Contorno escuro fino (o dilate compensa o que invade a letra) e sombra solida.
        mat.SetFloat("_FaceDilate", 0.05f);
        mat.SetFloat("_OutlineWidth", 0.12f);
        mat.SetColor("_OutlineColor", new Color(0.06f, 0.06f, 0.09f, 1f));
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.55f));
        mat.SetFloat("_UnderlayOffsetX", 0.5f);
        mat.SetFloat("_UnderlayOffsetY", -0.5f);
        mat.SetFloat("_UnderlayDilate", 0.1f);
        mat.SetFloat("_UnderlaySoftness", 0f);

        AssetDatabase.CreateAsset(mat, CaminhoMaterial);
    }

    // ------------------------------------------------------------------
    // Construcao do HUD
    // ------------------------------------------------------------------

    /// <summary>
    /// Recria o Canvas do HUD na cena aberta e liga as referencias do UIManager.
    /// </summary>
    public static string Construir()
    {
        string recursos = PrepararRecursos();

        fonte = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CaminhoFonte);
        materialContorno = AssetDatabase.LoadAssetAtPath<Material>(CaminhoMaterial);
        spritePainel = AssetDatabase.LoadAssetAtPath<Sprite>(CaminhoPainel);
        spriteBarra = AssetDatabase.LoadAssetAtPath<Sprite>(CaminhoBarra);
        camadaUI = LayerMask.NameToLayer("UI");

        UIManager ui = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);

        if (ui == null)
        {
            return "ERRO: a cena precisa de um UIManager.";
        }

        // Remove o HUD anterior (qualquer Canvas na raiz da cena).
        foreach (Canvas antigo in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (antigo != null && antigo.transform.parent == null)
            {
                Object.DestroyImmediate(antigo.gameObject);
            }
        }

        // ---------- Canvas ----------
        var canvasGO = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.layer = camadaUI;

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Transform raiz = canvasGO.transform;
        Vector2 topoEsq = new Vector2(0f, 1f);
        Vector2 topoDir = new Vector2(1f, 1f);
        Vector2 topoCentro = new Vector2(0.5f, 1f);
        Vector2 centro = new Vector2(0.5f, 0.5f);
        Vector2 baseCentro = new Vector2(0.5f, 0f);

        // ---------- Painel de informacoes ----------
        // Linha 1: nome da fase. Linha 2: valor grande e rotulo pequeno a direita.
        RectTransform painelInfo = Criar("PainelInfo", raiz, topoEsq, topoEsq, new Vector2(28f, -24f), new Vector2(500f, 132f));
        Moldura(painelInfo, CorPainel);

        TMP_Text titulo = Texto(Criar("Titulo", painelInfo, topoEsq, topoEsq, new Vector2(24f, -18f), new Vector2(452f, 36f)),
            "FASE 1", 24f, CorAmarelo, TextAlignmentOptions.Left);

        RectTransform valorRT = Criar("Valor", painelInfo, topoEsq, new Vector2(0.3f, 0.5f), new Vector2(24f + (452f * 0.3f), -62f - 26f), new Vector2(452f, 52f));
        TMP_Text valor = Texto(valorRT, "000000", 40f, Color.white, TextAlignmentOptions.Left);

        TMP_Text rotulo = Texto(Criar("Rotulo", painelInfo, topoEsq, topoEsq, new Vector2(24f, -62f), new Vector2(452f, 52f)),
            "PONTOS", 16f, CorRotulo, TextAlignmentOptions.Right);

        // Aviso do modo trapaca (Konami), na linha do titulo; so aparece com o cheat ligado.
        TMP_Text avisoCheat = Texto(Criar("Cheat", painelInfo, topoEsq, topoEsq, new Vector2(24f, -20f), new Vector2(452f, 32f)),
            "CHEAT ON", 14f, CorVermelho, TextAlignmentOptions.Right);
        avisoCheat.gameObject.SetActive(false);

        // ---------- Medidor do poder de lama ----------
        RectTransform painelPoder = Criar("PainelPoder", raiz, topoEsq, topoEsq, new Vector2(28f, -168f), new Vector2(500f, 52f));
        Moldura(painelPoder, CorPainel);

        TMP_Text textoPoder = Texto(Criar("Rotulo", painelPoder, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(150f, 40f)),
            "PODER", 16f, CorRotulo, TextAlignmentOptions.Left);

        RectTransform trilhoPoder = Esticar("Trilho", painelPoder, new Vector2(170f, 14f), new Vector2(18f, 14f));
        Moldura(trilhoPoder, CorTrilho);

        RectTransform cargaPoder = Esticar("Carga", trilhoPoder, new Vector2(6f, 6f), new Vector2(0f, 6f));
        cargaPoder.anchorMax = new Vector2(0.3f, 1f);
        Image imgPoder = cargaPoder.gameObject.AddComponent<Image>();
        imgPoder.sprite = spriteBarra;
        imgPoder.color = new Color(0.62f, 0.42f, 0.26f);
        imgPoder.raycastTarget = false;

        // ---------- Contador de frutas ----------
        RectTransform painelFrutas = Criar("PainelFrutas", raiz, topoDir, topoDir, new Vector2(-28f, -24f), new Vector2(300f, 140f));
        Moldura(painelFrutas, CorPainel);

        RectTransform iconeFruta = Criar("Icone", painelFrutas, new Vector2(0f, 1f), centro, new Vector2(58f, -48f), new Vector2(112f, 112f));
        Icone(iconeFruta, PrimeiroSprite(SpriteMaca));

        TMP_Text textoFrutas = Texto(Criar("Quantidade", painelFrutas, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(112f, -48f), new Vector2(176f, 60f)),
            "0/35", 32f, Color.white, TextAlignmentOptions.Left);

        // Cronometro: fica abaixo do contador de frutas, dentro do mesmo painel.
        TMP_Text textoTempo = Texto(Criar("Tempo", painelFrutas, topoCentro, new Vector2(0.5f, 0.5f), new Vector2(0f, -114f), new Vector2(280f, 30f)),
            "TEMPO 0:00", 18f, Color.white, TextAlignmentOptions.Center);

        // ---------- Barra de progresso ----------
        RectTransform painelProgresso = Criar("PainelProgresso", raiz, topoCentro, topoCentro, new Vector2(0f, -24f), new Vector2(840f, 96f));
        Moldura(painelProgresso, CorPainel);

        RectTransform trilho = Esticar("Trilho", painelProgresso, new Vector2(36f, 32f), new Vector2(92f, 32f));
        Moldura(trilho, CorTrilho);

        RectTransform onda = Esticar("Onda", trilho, new Vector2(8f, 8f), new Vector2(0f, 8f));
        onda.anchorMax = new Vector2(0.08f, 1f);
        Image imgOnda = onda.gameObject.AddComponent<Image>();
        imgOnda.sprite = spriteBarra;
        imgOnda.color = CorOnda;
        imgOnda.raycastTarget = false;

        RectTransform marcador = Criar("Jogador", trilho, new Vector2(0.15f, 0.5f), centro, new Vector2(0f, 6f), new Vector2(76f, 76f));
        Icone(marcador, PrimeiroSprite(SpriteSapo));

        RectTransform bandeira = Criar("Bandeira", painelProgresso, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 6f), new Vector2(84f, 84f));
        Icone(bandeira, PrimeiroSprite(SpriteBandeira));

        // ---------- Dica de controles (so no inicio da primeira partida da sessao) ----------
        RectTransform dica = Criar("DicaControles", raiz, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(660f, 200f));
        Moldura(dica, CorPainel);
        Texto(Criar("Titulo", dica, topoCentro, topoCentro, new Vector2(0f, -20f), new Vector2(600f, 30f)),
            "CONTROLES", 18f, CorAmarelo, TextAlignmentOptions.Center);
        MontarControles(dica, new Vector2(0f, -62f), 18f);
        dica.gameObject.AddComponent<CanvasGroup>();
        dica.gameObject.AddComponent<DicaControles>();

        // ---------- Game Over ----------
        RectTransform painelGameOver = Esticar("PainelGameOver", raiz, Vector2.zero, Vector2.zero);
        Escurecer(painelGameOver);

        RectTransform caixaGO = Criar("Caixa", painelGameOver, centro, centro, Vector2.zero, new Vector2(800f, 500f));
        Moldura(caixaGO, CorCaixa);

        Texto(Criar("Titulo", caixaGO, topoCentro, topoCentro, new Vector2(0f, -48f), new Vector2(740f, 90f)),
            "GAME OVER", 64f, CorVermelho, TextAlignmentOptions.Center);

        TMP_Text detalheGO = Texto(Criar("Detalhe", caixaGO, topoCentro, topoCentro, new Vector2(0f, -164f), new Vector2(740f, 110f)),
            "PROGRESSO 43%\nPONTOS 1.250", 28f, Color.white, TextAlignmentOptions.Center);
        detalheGO.lineSpacing = 50f;

        TMP_Text recordeGO = Texto(Criar("Recorde", caixaGO, topoCentro, topoCentro, new Vector2(0f, -280f), new Vector2(740f, 40f)),
            "RECORDE 2.400", 22f, CorAmarelo, TextAlignmentOptions.Center);

        Button botaoReiniciar = Botao("BotaoReiniciar", caixaGO, "TENTAR DE NOVO", 26f, CorBotaoVerde, baseCentro, new Vector2(0f, 64f), new Vector2(460f, 90f));

        Texto(Criar("Dica", caixaGO, baseCentro, baseCentro, new Vector2(0f, 20f), new Vector2(460f, 40f)),
            "ou aperte R", 16f, CorRotulo, TextAlignmentOptions.Center);

        // ---------- Vitoria ----------
        RectTransform painelVitoria = Esticar("PainelVitoria", raiz, Vector2.zero, Vector2.zero);
        Escurecer(painelVitoria);

        RectTransform caixaV = Criar("Caixa", painelVitoria, centro, centro, Vector2.zero, new Vector2(860f, 700f));
        Moldura(caixaV, CorCaixa);

        TMP_Text tituloV = Texto(Criar("Titulo", caixaV, topoCentro, topoCentro, new Vector2(0f, -44f), new Vector2(800f, 80f)),
            "FASE COMPLETA!", 40f, CorAmarelo, TextAlignmentOptions.Center);

        TMP_Text itens = Texto(Criar("Itens", caixaV, topoEsq, topoEsq, new Vector2(70f, -160f), new Vector2(520f, 240f)),
            "Frutas 12/35\nInimigos 3\nTempo 0:42/1:00", 24f, Color.white, TextAlignmentOptions.TopLeft);
        itens.lineSpacing = 60f;

        TMP_Text valores = Texto(Criar("Valores", caixaV, topoDir, topoDir, new Vector2(-70f, -160f), new Vector2(260f, 240f)),
            "1.200\n600\n+360", 24f, Color.white, TextAlignmentOptions.TopRight);
        valores.lineSpacing = 60f;

        RectTransform separador = Criar("Separador", caixaV, topoCentro, topoCentro, new Vector2(0f, -404f), new Vector2(720f, 8f));
        Image imgSep = separador.gameObject.AddComponent<Image>();
        imgSep.sprite = spriteBarra;
        imgSep.color = new Color(CorRotulo.r, CorRotulo.g, CorRotulo.b, 0.7f);
        imgSep.raycastTarget = false;

        TMP_Text total = Texto(Criar("Total", caixaV, topoCentro, topoCentro, new Vector2(0f, -428f), new Vector2(740f, 64f)),
            "TOTAL 2.160", 44f, CorAmarelo, TextAlignmentOptions.Center);

        TMP_Text recordeV = Texto(Criar("Recorde", caixaV, topoCentro, topoCentro, new Vector2(0f, -502f), new Vector2(740f, 40f)),
            "NOVO RECORDE!", 22f, CorVerdeClaro, TextAlignmentOptions.Center);

        Button botaoProxima = Botao("BotaoProximaFase", caixaV, "PR\u00d3XIMA FASE", 22f, CorBotaoVerde, baseCentro, new Vector2(-190f, 36f), new Vector2(360f, 90f));
        Button botaoDeNovo = Botao("BotaoJogarNovamente", caixaV, "JOGAR DE NOVO", 22f, CorBotaoAzul, baseCentro, new Vector2(190f, 36f), new Vector2(360f, 90f));

        // ---------- Pausa (ESC) ----------
        RectTransform painelPausa = Esticar("PainelPausa", raiz, Vector2.zero, Vector2.zero);
        Escurecer(painelPausa);

        RectTransform caixaP = Criar("Caixa", painelPausa, centro, centro, Vector2.zero, new Vector2(800f, 580f));
        Moldura(caixaP, CorCaixa);

        Texto(Criar("Titulo", caixaP, topoCentro, topoCentro, new Vector2(0f, -44f), new Vector2(740f, 80f)),
            "PAUSADO", 56f, CorAmarelo, TextAlignmentOptions.Center);

        MontarControles(caixaP, new Vector2(0f, -150f), 22f);

        Button botaoContinuar = Botao("BotaoContinuar", caixaP, "CONTINUAR", 26f, CorBotaoVerde, baseCentro, new Vector2(0f, 168f), new Vector2(440f, 90f));
        Button botaoReiniciarPausa = Botao("BotaoReiniciarPausa", caixaP, "REINICIAR", 26f, CorBotaoAzul, baseCentro, new Vector2(0f, 56f), new Vector2(440f, 90f));

        painelPausa.gameObject.SetActive(false);
        painelGameOver.gameObject.SetActive(false);
        painelVitoria.gameObject.SetActive(false);

        // ---------- Liga o UIManager ----------
        var faltando = new List<string>();
        var so = new SerializedObject(ui);

        void Ligar(string campo, Object alvo)
        {
            SerializedProperty p = so.FindProperty(campo);

            if (p == null)
            {
                faltando.Add(campo);
                return;
            }

            p.objectReferenceValue = alvo;
        }

        Ligar("textoTitulo", titulo);
        Ligar("textoRotulo", rotulo);
        Ligar("textoCheat", avisoCheat);
        Ligar("textoTempo", textoTempo);
        Ligar("painelPausa", painelPausa.gameObject);
        Ligar("botaoContinuar", botaoContinuar);
        Ligar("botaoReiniciarPausa", botaoReiniciarPausa);
        Ligar("textoValor", valor);
        Ligar("grupoFrutas", painelFrutas.gameObject);
        Ligar("textoFrutas", textoFrutas);
        Ligar("iconeFruta", iconeFruta);
        Ligar("grupoProgresso", painelProgresso.gameObject);
        Ligar("preenchimentoOnda", onda);
        Ligar("marcadorJogador", marcador);
        Ligar("painelGameOver", painelGameOver.gameObject);
        Ligar("textoGameOverDetalhe", detalheGO);
        Ligar("textoGameOverRecorde", recordeGO);
        Ligar("botaoReiniciar", botaoReiniciar);
        Ligar("painelVitoria", painelVitoria.gameObject);
        Ligar("textoVitoriaTitulo", tituloV);
        Ligar("textoItens", itens);
        Ligar("textoValores", valores);
        Ligar("textoTotal", total);
        Ligar("textoRecorde", recordeV);
        Ligar("botaoProximaFase", botaoProxima);
        Ligar("botaoJogarNovamente", botaoDeNovo);
        Ligar("grupoPoder", painelPoder.gameObject);
        Ligar("preenchimentoPoder", cargaPoder);
        Ligar("imagemPoder", imgPoder);
        Ligar("textoPoder", textoPoder);
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvasGO.scene);

        return "HUD construido. Recursos: " + recursos.Trim()
            + (faltando.Count > 0 ? " | CAMPOS NAO ENCONTRADOS NO UIManager: " + string.Join(", ", faltando) : " | UIManager ligado (30 campos)");
    }

    // ------------------------------------------------------------------
    // Utilitarios de layout
    // ------------------------------------------------------------------
    private static RectTransform Criar(string nome, Transform pai, Vector2 ancora, Vector2 pivo, Vector2 posicao, Vector2 tamanho)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.layer = camadaUI;
        var rt = (RectTransform)go.transform;
        rt.SetParent(pai, false);
        rt.anchorMin = ancora;
        rt.anchorMax = ancora;
        rt.pivot = pivo;
        rt.anchoredPosition = posicao;
        rt.sizeDelta = tamanho;
        return rt;
    }

    /// <summary>Ocupa todo o pai, com margens (esquerda, baixo) e (direita, cima).</summary>
    private static RectTransform Esticar(string nome, Transform pai, Vector2 margemMin, Vector2 margemMax)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.layer = camadaUI;
        var rt = (RectTransform)go.transform;
        rt.SetParent(pai, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = margemMin;
        rt.offsetMax = -margemMax;
        return rt;
    }

    private static Image Moldura(RectTransform rt, Color cor)
    {
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = spritePainel;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f / EscalaPixel;
        img.color = cor;
        img.raycastTarget = false;
        return img;
    }

    private static void Escurecer(RectTransform rt)
    {
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = CorEscurecer;
        img.raycastTarget = true;   // bloqueia cliques no jogo por tras
    }

    private static Image Icone(RectTransform rt, Sprite sprite)
    {
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text Texto(RectTransform rt, string conteudo, float tamanho, Color cor, TextAlignmentOptions alinhamento)
    {
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = fonte;
        t.fontSharedMaterial = materialContorno;
        t.text = conteudo;
        t.fontSize = tamanho;
        t.color = cor;
        t.alignment = alinhamento;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static Button Botao(string nome, Transform pai, string rotulo, float tamanhoTexto, Color cor, Vector2 ancora, Vector2 posicao, Vector2 tamanho)
    {
        RectTransform rt = Criar(nome, pai, ancora, new Vector2(0.5f, 0f), posicao, tamanho);
        Image img = Moldura(rt, Color.white);
        img.raycastTarget = true;

        Button bt = rt.gameObject.AddComponent<Button>();
        bt.targetGraphic = img;

        // A transicao multiplica a cor da imagem (branca) pela cor do estado.
        ColorBlock cores = bt.colors;
        cores.normalColor = cor;
        cores.highlightedColor = Color.Lerp(cor, Color.white, 0.25f);
        cores.selectedColor = cor;
        cores.pressedColor = Color.Lerp(cor, Color.black, 0.25f);
        cores.colorMultiplier = 1f;
        cores.fadeDuration = 0.08f;
        bt.colors = cores;

        RectTransform rotuloRT = Esticar("Rotulo", rt, Vector2.zero, Vector2.zero);
        Texto(rotuloRT, rotulo, tamanhoTexto, Color.white, TextAlignmentOptions.Center);
        return bt;
    }

    /// <summary>
    /// Lista de controles em duas colunas (teclas a esquerda do centro, acoes a
    /// direita). A fonte e monoespacada, entao as linhas das duas colunas alinham.
    /// </summary>
    private static void MontarControles(RectTransform pai, Vector2 posicao, float tamanho)
    {
        const string teclas = "SETAS ou A/D\nESPA\u00c7O ou W\nX ou J\nESC";
        const string acoes = "MOVER\nPULAR\nLAMA (COM PODER)\nPAUSA";

        // A coluna de acoes (16 letras) e mais larga que a de teclas (12 letras):
        // desloca o conjunto para o conteudo ficar centralizado no painel.
        float deslocamento = -2f * tamanho;

        TMP_Text colunaTeclas = Texto(Criar("Teclas", pai, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(posicao.x - 20f + deslocamento, posicao.y), new Vector2(380f, tamanho * 7f)),
            teclas, tamanho, CorAmarelo, TextAlignmentOptions.TopRight);
        colunaTeclas.lineSpacing = 50f;

        TMP_Text colunaAcoes = Texto(Criar("Acoes", pai, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(posicao.x + 20f + deslocamento, posicao.y), new Vector2(420f, tamanho * 7f)),
            acoes, tamanho, Color.white, TextAlignmentOptions.TopLeft);
        colunaAcoes.lineSpacing = 50f;
    }

    private static Sprite PrimeiroSprite(string caminho)
    {
        Sprite escolhido = null;

        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(caminho))
        {
            if (o is Sprite s && (escolhido == null || s.name.EndsWith("_0")))
            {
                escolhido = s;
            }
        }

        return escolhido;
    }
}
