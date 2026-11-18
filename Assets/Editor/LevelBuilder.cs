using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Gera uma fase a partir de um mapa de texto (1 caractere = 1 unidade do mundo).
///
/// Legenda do mapa:
///   #  chao verde      O  chao laranja      R  chao rosa/areia
///      (o chao e preenchido para baixo ate o fim do mapa)
///   B  bloco marrom (piramide de 1x1 un, estrutura solida)
///   ^  espinhos (um par por celula)     S  serra
///   E  inimigo     F  fruta     K  bandeira de chegada     P  inicio do jogador
///   1 2 3  caixa que aguenta 1, 2 ou 3 batidas (solta 2, 4 ou 6 frutas)
///   M  plataforma movel horizontal   V  plataforma movel vertical   Q  plataforma que cai
///   H  casa da chegada (posicao da porta, depois da bandeira)
///   f  chama (fogo; empilhe 2 para um pilar de 2 un)     Z  inimigo explosivo
///   T  trampolim     k  kiwi     X  melancia (uma por jogo, no ponto mais alto)
///   I  ilha flutuante (celula solida que nao preenche para baixo; precisa de 2 un de folga embaixo)
///   b  fruta extra da faixa de 100 pontos
///   W  pedra que esmaga (Rock Head): parada no ar, avisa, desce ate a primeira superficie solida e volta
///   D  cabeca de espinhos (Spike Head): desliza para a direita ate a primeira superficie solida e volta
///   Torre (fase vertical): @modo=vertical (chunks viram faixas horizontais de 4 un; a onda sobe; a camera so sobe).
///   Opcionais da torre: @onda=v0,aceleracao,vmax  @ondaInicio=14  @ondaAtraso=16  @cameraAltura=2.5  @tempoAlvo=N
///   @nomeFase=FASE 3  @proximaFase=Phase4  @velocidadeV=1.6 (elevadores)  @estiloIlha=R (#, O ou R)
///   Y = Sombra (refaz o caminho do jogador com atraso; uma por fase basta)
///   u = esteira de plataformas que sobem   d = esteira que descem
///   C = teto/parede de caverna (solido, nao auditado como ilha)
///   G = maca dourada (obrigatoria: sem todas, a bandeira nao vence a fase)
///   Diretivas opcionais no mapa: @fruta100=Fruit_X   @fruta100b=Fruit_Y   @fruta250=Fruit_Z (trocam so o visual;
///   a pontuacao e a do prefab). Sem diretivas, vale maca (100) e kiwi (250).
///   espaco = vazio (vira buraco se nao houver chao na coluna)
///
/// Linhas iniciadas por "//" sao comentarios. A linha "@chao=N" indica qual linha
/// do mapa (contando de 0 a partir do topo) e o chao base, cuja superficie fica em y = 0,5.
///
/// O terreno vira Tilemaps de 4 colunas (chunks). Cada chunk desaba sozinho quando a
/// onda de destruicao passa. Cada tile tem 0,5 un, a mesma densidade de pixels
/// dos personagens. Ao final, audita buracos, degraus e fileiras de espinhos.
/// </summary>
public static partial class LevelBuilder
{
    private const string PastaTiles = "Assets/Tiles";
    private const string PastaPrefabs = "Assets/Prefabs/";
    private const string Atlas = "Assets/Pixel Adventure 1/Assets/Terrain/Terrain Sliced (16x16).png";
    private const float TamTile = 0.5f;
    private const int ColunasPorChunk = 4;
    private const int LinhasPorChunk = 4;
    private const string CharsTerreno = "#ORN";
    private const string CharsPlataforma = "MVQ";
    private const string CharsIlha = "I";

    /// <summary>
    /// Teto/parede de caverna: pinta solido como a ilha, mas nao entra na auditoria de
    /// ilhas alcancaveis (ninguem precisa pousar no teto).
    /// </summary>
    private const string CharsTeto = "C";
    private const string SpriteCorrente = "Assets/Pixel Adventure 1/Assets/Traps/Platforms/Chain.png";
    private const float DistanciaPlataforma = 2f;

    // Origem (x, y) do bloco 3x3 de cada estilo no atlas
    private static readonly Dictionary<char, Vector2Int> Estilos = new Dictionary<char, Vector2Int>
    {
        { '#', new Vector2Int(96, 128) },
        { 'O', new Vector2Int(96, 64) },
        { 'R', new Vector2Int(96, 0) },
        // pedra cinza de tijolos (moldura com miolo escuro), do Pixel Adventure: terreno de caverna
        { 'N', new Vector2Int(0, 128) }
    };

    // Offsets dentro do bloco 3x3 de um estilo
    private static readonly Vector2Int TL = new Vector2Int(0, 32);
    private static readonly Vector2Int TM = new Vector2Int(16, 32);
    private static readonly Vector2Int TR = new Vector2Int(32, 32);
    private static readonly Vector2Int ML = new Vector2Int(0, 16);
    private static readonly Vector2Int MM = new Vector2Int(16, 16);
    private static readonly Vector2Int MR = new Vector2Int(32, 16);
    private static readonly Vector2Int BL = new Vector2Int(0, 0);
    private static readonly Vector2Int BM = new Vector2Int(16, 0);
    private static readonly Vector2Int BR = new Vector2Int(32, 0);

    // Cantos internos (concavos), ao lado do bloco 3x3
    private static readonly Vector2Int CantoDiagEsqVazia = new Vector2Int(64, 16);
    private static readonly Vector2Int CantoDiagDirVazia = new Vector2Int(48, 16);

    // Bloco marrom em piramide: 2x2 tiles
    private static readonly Vector2Int BlocoTL = new Vector2Int(208, 144);
    private static readonly Vector2Int BlocoTR = new Vector2Int(224, 144);
    private static readonly Vector2Int BlocoBL = new Vector2Int(208, 128);
    private static readonly Vector2Int BlocoBR = new Vector2Int(224, 128);

    private class Mapa
    {
        public int largura;
        public int altura;
        public int linhaChao;
        public char[,] celulas;
        public Dictionary<string, string> diretivas = new Dictionary<string, string>();
        public bool vertical;
    }

    private static Dictionary<Vector2Int, Sprite> sprites;
    private static readonly Dictionary<Vector2Int, Tile> tiles = new Dictionary<Vector2Int, Tile>();

    [MenuItem("Tools/Level Builder/Reconstruir Fase 1")]
    private static void MenuFase1()
    {
        Debug.Log(Construir("Assets/Levels/Phase1.txt"));
    }

    /// <summary>
    /// Reconstroi o objeto "Nivel" da cena aberta a partir do mapa de texto.
    /// Retorna um relatorio com contagens e a auditoria de jogabilidade.
    /// </summary>
    public static string Construir(string caminhoMapa)
    {
        var rel = new StringBuilder();
        Mapa mapa = Ler(caminhoMapa);
        mapa.vertical = Tema(mapa, "modo", "") == "vertical";
        CarregarSprites();
        PrepararTiles();

        int W = mapa.largura;
        int H = mapa.altura;

        int colP = -1;
        int linP = -1;
        for (int c = 0; c < W; c++)
        {
            for (int r = 0; r < H; r++)
            {
                if (mapa.celulas[c, r] == 'P')
                {
                    colP = c;
                    linP = r;
                }
            }
        }

        if (colP < 0)
        {
            return "ERRO: o mapa precisa de um 'P' (inicio do jogador).";
        }

        // O jogador define x = 0; o topo da linha de chao base fica em y = 0,5.
        float origemX = -(colP + 0.5f);
        float origemY = 0.5f - (H - mapa.linhaChao);

        // ---------- terreno em celulas de 1 un ----------
        var solido = new bool[W, H];
        var estilo = new char[W, H];
        var alturaColuna = new int[W];

        for (int c = 0; c < W; c++)
        {
            alturaColuna[c] = -1;

            for (int r = 0; r < H; r++)
            {
                char ch = mapa.celulas[c, r];

                if (CharsTerreno.IndexOf(ch) < 0)
                {
                    continue;
                }

                alturaColuna[c] = mapa.linhaChao - r;

                for (int rr = r; rr < H; rr++)
                {
                    solido[c, rr] = true;
                    estilo[c, rr] = ch;
                }

                break;
            }
        }

        char EstiloDaIlha = Tema(mapa, "estiloIlha", "R")[0];

        if (!Estilos.ContainsKey(EstiloDaIlha))
        {
            EstiloDaIlha = 'R';
        }

        // ilhas flutuantes: celulas solidas que nao preenchem para baixo
        for (int c = 0; c < W; c++)
        {
            for (int r = 0; r < H; r++)
            {
                if (CharsIlha.IndexOf(mapa.celulas[c, r]) >= 0 || CharsTeto.IndexOf(mapa.celulas[c, r]) >= 0)
                {
                    solido[c, r] = true;
                    estilo[c, r] = EstiloDaIlha;
                }
            }
        }

        // ---------- cria a hierarquia ----------
        GameObject antigo = GameObject.Find("Nivel");
        if (antigo != null)
        {
            Object.DestroyImmediate(antigo);
        }

        var nivel = new GameObject("Nivel");
        var gridGO = new GameObject("Grid");
        gridGO.transform.SetParent(nivel.transform, false);
        gridGO.transform.position = new Vector3(origemX, origemY, 0f);
        var grid = gridGO.AddComponent<Grid>();
        grid.cellSize = new Vector3(TamTile, TamTile, 0f);

        int camadaChao = LayerMask.NameToLayer("Ground");
        var chunks = new Dictionary<int, Tilemap>();

        Tilemap ObterChunk(int indice)
        {
            if (chunks.TryGetValue(indice, out Tilemap existente))
            {
                return existente;
            }

            var go = new GameObject("Chunk_" + indice.ToString("00"));
            go.transform.SetParent(gridGO.transform, false);
            go.layer = camadaChao;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            var tm = go.AddComponent<Tilemap>();
            var tr = go.AddComponent<TilemapRenderer>();
            tr.sortingOrder = 0;

            var composto = go.AddComponent<CompositeCollider2D>();
            composto.geometryType = CompositeCollider2D.GeometryType.Polygons;

            var tc = go.AddComponent<TilemapCollider2D>();
            tc.compositeOperation = Collider2D.CompositeOperation.Merge;

            go.AddComponent<PlatformSegment>();

            chunks[indice] = tm;
            return tm;
        }

        // Torre: o terreno vira faixas horizontais (cada faixa desaba quando a onda sobe ate ela).
        int IndiceChunk(int c, int r)
        {
            return mapa.vertical ? (H - 1 - r) / LinhasPorChunk : c / ColunasPorChunk;
        }

        // Quanto a pedra percorre ate bater: ate a primeira celula solida abaixo (ou a direita).
        float DistanciaGolpe(int c, int r, bool horizontal, float meioTamanho)
        {
            if (!horizontal)
            {
                float centroInicio = origemY + (H - 1 - r) + 0.5f;

                for (int rr = r + 1; rr < H; rr++)
                {
                    if (solido[c, rr])
                    {
                        float topo = origemY + (H - 1 - rr) + 1f;
                        return Mathf.Max(0.5f, centroInicio - (topo + meioTamanho));
                    }
                }

                return 4f;
            }

            float centroX = origemX + c + 0.5f;

            for (int cc = c + 1; cc < W; cc++)
            {
                if (solido[cc, r])
                {
                    float borda = origemX + cc;
                    return Mathf.Max(0.5f, borda - meioTamanho - centroX);
                }
            }

            return 4f;
        }

        int W2 = W * 2;
        int H2 = H * 2;

        bool EhSolido(int x, int y)
        {
            if (y >= H2) return false;          // acima do mapa: vazio
            if (y < 0) return true;             // abaixo do mapa: considera chao
            if (x < 0 || x >= W2) return true;  // laterais do mapa: considera chao
            return solido[x / 2, H - 1 - (y / 2)];
        }

        Vector2Int Escolher(int x, int y)
        {
            bool cima = EhSolido(x, y + 1);
            bool baixo = EhSolido(x, y - 1);
            bool esq = EhSolido(x - 1, y);
            bool dir = EhSolido(x + 1, y);
            bool diagEsq = EhSolido(x - 1, y + 1);
            bool diagDir = EhSolido(x + 1, y + 1);

            if (!cima)
            {
                if (!esq && dir) return TL;
                if (!dir && esq) return TR;
                return TM;
            }

            if (!baixo)
            {
                if (!esq && dir) return BL;
                if (!dir && esq) return BR;
                return BM;
            }

            if (!esq && dir) return ML;
            if (!dir && esq) return MR;
            if (!diagEsq) return CantoDiagEsqVazia;
            if (!diagDir) return CantoDiagDirVazia;
            return MM;
        }

        // ---------- pinta o terreno ----------
        int totalTiles = 0;
        for (int x = 0; x < W2; x++)
        {
            int c = x / 2;

            for (int y = 0; y < H2; y++)
            {
                int r = H - 1 - (y / 2);

                if (!solido[c, r])
                {
                    continue;
                }

                Vector2Int origem = Estilos[estilo[c, r]];
                Tile tile = ObterTile(origem + Escolher(x, y));
                ObterChunk(IndiceChunk(c, r)).SetTile(new Vector3Int(x, y, 0), tile);
                totalTiles++;
            }
        }

        // ---------- blocos marrons ----------
        int totalBlocos = 0;
        for (int c = 0; c < W; c++)
        {
            for (int r = 0; r < H; r++)
            {
                if (mapa.celulas[c, r] != 'B')
                {
                    continue;
                }

                int tx = c * 2;
                int ty = (H - 1 - r) * 2;
                Tilemap tm = ObterChunk(IndiceChunk(c, r));
                tm.SetTile(new Vector3Int(tx, ty + 1, 0), ObterTile(BlocoTL));
                tm.SetTile(new Vector3Int(tx + 1, ty + 1, 0), ObterTile(BlocoTR));
                tm.SetTile(new Vector3Int(tx, ty, 0), ObterTile(BlocoBL));
                tm.SetTile(new Vector3Int(tx + 1, ty, 0), ObterTile(BlocoBR));
                totalBlocos++;
            }
        }

        foreach (Tilemap tm in chunks.Values)
        {
            tm.CompressBounds();
        }

        // ---------- objetos ----------
        var contagem = new Dictionary<char, int>();
        var ausentes = new HashSet<string>();

        GameObject Instanciar(string nome, Vector3 pos, Transform pai)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PastaPrefabs + nome + ".prefab");

            if (prefab == null)
            {
                ausentes.Add(nome);
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pai);
            go.transform.position = pos;
            return go;
        }

        for (int c = 0; c < W; c++)
        {
            for (int r = 0; r < H; r++)
            {
                char ch = mapa.celulas[c, r];
                float cx = origemX + c + 0.5f;
                float yBase = origemY + (H - 1 - r);
                Transform chunk = ObterChunk(IndiceChunk(c, r)).transform;
                bool contar = true;

                switch (ch)
                {
                    case '^':
                        Instanciar("Obstacle_Spikes", new Vector3(cx - 0.25f, yBase + 0.25f, 0f), chunk);
                        Instanciar("Obstacle_Spikes", new Vector3(cx + 0.25f, yBase + 0.25f, 0f), chunk);
                        break;
                    case 'S':
                        Instanciar("Obstacle_Saw", new Vector3(cx, yBase + 0.5f, 0f), chunk);
                        break;
                    case 'E':
                        AjustarInimigoTorre(Instanciar("Enemy_MaskDude", new Vector3(cx, yBase, 0f), nivel.transform), mapa, c, r, W, origemX);
                        break;
                    case 'K':
                        Instanciar("LevelFlag", new Vector3(cx, yBase, 0f), nivel.transform);
                        break;
                    case 'F':
                        Instanciar(Tema(mapa, "fruta100", "Fruit_Apple"), new Vector3(cx, yBase + 0.5f, 0f), nivel.transform);
                        break;
                    case 'b':
                        Instanciar(Tema(mapa, "fruta100b", Tema(mapa, "fruta100", "Fruit_Apple")), new Vector3(cx, yBase + 0.5f, 0f), nivel.transform);
                        break;
                    case 'I':
                        break;
                    case '1':
                    case '2':
                    case '3':
                        AjustarFrutaCaixa(Instanciar("Box_" + ch, new Vector3(cx, yBase + 0.5f, 0f), chunk),
                            ch == '3' ? Tema(mapa, "fruta250", "Fruit_Kiwi") : Tema(mapa, "fruta100", "Fruit_Apple"));
                        break;
                    case 'u':
                    case 'd':
                        CriarEsteira(mapa, c, r, ch == 'u', nivel.transform, origemX, origemY, H, solido, Instanciar);
                        break;
                    case 'M':
                    case 'V':
                        GameObject plataformaMovel = Instanciar("Platform_MovingGrey", new Vector3(cx, yBase + 0.125f, 0f), nivel.transform);
                        CriarPlataformaMovel(plataformaMovel, ch == 'V', chunk);
                        // elevadores vizinhos entram defasados, para a fila subir e descer em onda
                        DefasarPlataforma(plataformaMovel, mapa, c, r, ch);
                        AjustarVelocidade(plataformaMovel, Tema(mapa, ch == 'V' ? "velocidadeV" : "velocidadeM", ""));
                        break;
                    case 'k':
                        Instanciar(Tema(mapa, "fruta250", "Fruit_Kiwi"), new Vector3(cx, yBase + 0.5f, 0f), nivel.transform);
                        break;
                    case 'Y':
                        // Sombra: refaz o caminho do jogador com atraso (fica invisivel ate o jogador partir)
                        Instanciar("Enemy_Sombra", new Vector3(cx, yBase + 0.5f, 0f), nivel.transform);
                        break;
                    case 'G':
                        // maca dourada: coletavel obrigatorio para a bandeira liberar a vitoria
                        Instanciar("Fruit_GoldenApple", new Vector3(cx, yBase + 0.7f, 0f), nivel.transform);
                        break;
                    case 'X':
                        Instanciar("Fruit_Melon", new Vector3(cx, yBase + 0.5f, 0f), nivel.transform);
                        break;
                    case 'H':
                        Instanciar("House_Goal", new Vector3(cx, yBase, 0f), nivel.transform);
                        break;
                    case 'f':
                    {
                        // Chamas lado a lado formam um grupo e compartilham a fase do ciclo.
                        int inicioGrupo = c;
                        while (inicioGrupo > 0 && mapa.celulas[inicioGrupo - 1, r] == 'f')
                        {
                            inicioGrupo--;
                        }

                        float fase = Mathf.Repeat(inicioGrupo * 1.3f, 3.6f);
                        ConfigurarChama(Instanciar("Trap_Fire", new Vector3(cx - 0.25f, yBase, 0f), chunk), fase);
                        ConfigurarChama(Instanciar("Trap_Fire", new Vector3(cx + 0.25f, yBase, 0f), chunk), fase);
                        break;
                    }
                    case 'Z':
                        AjustarInimigoTorre(Instanciar("Enemy_Bomber", new Vector3(cx, yBase, 0f), nivel.transform), mapa, c, r, W, origemX);
                        break;
                    case 'T':
                        Instanciar("Trap_Trampoline", new Vector3(cx, yBase, 0f), chunk);
                        break;
                    case 'Q':
                        Instanciar("Platform_FallingFan", new Vector3(cx, yBase + 0.125f, 0f), nivel.transform);
                        break;
                    case 'W':
                        ConfigurarPedra(Instanciar("Trap_RockHead", new Vector3(cx, yBase + 0.5f, 0f), chunk), DistanciaGolpe(c, r, false, 0.66f), c, r);
                        break;
                    case 'D':
                        ConfigurarPedra(Instanciar("Trap_SpikeHead", new Vector3(cx, yBase + 0.82f, 0f), chunk), DistanciaGolpe(c, r, true, 0.85f), c, r);
                        break;
                    default:
                        contar = false;
                        break;
                }

                if (contar)
                {
                    contagem[ch] = contagem.ContainsKey(ch) ? contagem[ch] + 1 : 1;
                }
            }
        }

        // ---------- jogador, onda e camera ----------
        float yJogador = origemY + (H - 1 - linP) + 0.55f;
        GameObject jogador = GameObject.FindGameObjectWithTag("Player");

        if (jogador != null)
        {
            jogador.transform.position = new Vector3(0f, yJogador, 0f);
        }

        DestructionWave onda = Object.FindFirstObjectByType<DestructionWave>();

        if (onda != null)
        {
            onda.transform.position = new Vector3(-16f, 0f, 0f);
        }

        if (Camera.main != null)
        {
            Camera.main.transform.position = new Vector3(3.5f, yJogador + 1f, -10f);
        }

        if (mapa.vertical)
        {
            ConfigurarTorre(mapa, origemX, W, yJogador, rel);
        }

        Physics2D.SyncTransforms();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        // ---------- relatorio ----------
        rel.AppendLine("Mapa " + W + " x " + H + " un | chao base na linha " + mapa.linhaChao
            + " | origem (" + origemX.ToString("F1") + ", " + origemY.ToString("F1") + ")");
        rel.AppendLine(chunks.Count + " chunks | " + totalTiles + " tiles de terreno | " + totalBlocos + " blocos marrons");

        var itens = new StringBuilder();
        foreach (KeyValuePair<char, int> par in contagem)
        {
            itens.Append(par.Key).Append("=").Append(par.Value).Append("  ");
        }

        rel.AppendLine("objetos: " + itens);

        if (ausentes.Count > 0)
        {
            rel.AppendLine("prefabs ainda inexistentes (ignorados): " + string.Join(", ", ausentes));
        }

        if (mapa.vertical)
        {
            AuditarTorre(mapa, solido, rel);
        }
        else
        {
            Auditar(mapa, solido, alturaColuna, rel);
        }

        return rel.ToString();
    }

    // ------------------------------------------------------------------
    // Auditoria de jogabilidade
    // ------------------------------------------------------------------
    private static void Auditar(Mapa mapa, bool[,] solido, int[] alturaColuna, StringBuilder rel)
    {
        int W = mapa.largura;
        int H = mapa.altura;

        // Capacidade de pulo, lida do jogador da cena
        float vel = 7f;
        float forca = 13f;
        float mult = 2.2f;
        float coyote = 0.12f;
        float gravidade = Mathf.Abs(Physics2D.gravity.y) * 3.5f;
        GameObject jogador = GameObject.FindGameObjectWithTag("Player");

        if (jogador != null)
        {
            var pc = jogador.GetComponent<PlayerController>();
            var rb = jogador.GetComponent<Rigidbody2D>();

            if (pc != null && rb != null)
            {
                var so = new SerializedObject(pc);
                vel = so.FindProperty("velocidade").floatValue;
                forca = so.FindProperty("forcaPulo").floatValue;
                mult = so.FindProperty("multiplicadorQueda").floatValue;
                coyote = so.FindProperty("coyoteTime").floatValue;
                gravidade = Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale;
            }
        }

        float apice = forca * forca / (2f * gravidade);

        float Alcance(float subida)
        {
            float h = apice - subida;
            if (h < 0f) return 0f;
            float tDescida = Mathf.Sqrt(2f * h / (gravidade * mult));
            return (vel * ((forca / gravidade) + tDescida)) + (vel * coyote);
        }

        rel.AppendLine("--- AUDITORIA --- pulo: altura max " + apice.ToString("F2")
            + " un | alcance no plano " + Alcance(0f).ToString("F1") + " un (com coyote)");

        int problemas = 0;

        // cadeia de apoios sobre um buraco: chao, ilhas, plataformas (a movel cobre todo o curso dela)
        bool Cadeia(int iniBur, int fimBur, int alturaEsq, int alturaDir, out float chegaAte)
        {
            var apEsq = new List<float>();
            var apDir = new List<float>();
            var apTopo = new List<float>();

            apEsq.Add(-1000f);
            apDir.Add(iniBur);
            apTopo.Add(alturaEsq + 0.5f);

            for (int rI = 0; rI < H; rI++)
            {
                int cI = iniBur;

                while (cI < fimBur)
                {
                    if (CharsIlha.IndexOf(mapa.celulas[cI, rI]) < 0)
                    {
                        cI++;
                        continue;
                    }

                    int fI = cI;

                    while (fI + 1 < fimBur && CharsIlha.IndexOf(mapa.celulas[fI + 1, rI]) >= 0)
                    {
                        fI++;
                    }

                    apEsq.Add(cI);
                    apDir.Add(fI + 1);
                    apTopo.Add((mapa.linhaChao - rI) + 0.5f);
                    cI = fI + 1;
                }
            }

            for (int cP = iniBur; cP < fimBur; cP++)
            {
                for (int rP = 0; rP < H; rP++)
                {
                    char chP = mapa.celulas[cP, rP];

                    if (CharsPlataforma.IndexOf(chP) < 0)
                    {
                        continue;
                    }

                    float meio = chP == 'M' ? DistanciaPlataforma + 1f : 1f;
                    apEsq.Add(cP + 0.5f - meio);
                    apDir.Add(cP + 0.5f + meio);
                    apTopo.Add((mapa.linhaChao - rP) - 0.25f);
                }
            }

            apEsq.Add(fimBur);
            apDir.Add(1000f);
            apTopo.Add(alturaDir + 0.5f);

            int nAp = apEsq.Count;
            var alcancados = new bool[nAp];
            alcancados[0] = true;
            bool cresceu = true;

            while (cresceu)
            {
                cresceu = false;

                for (int u = 0; u < nAp; u++)
                {
                    if (!alcancados[u])
                    {
                        continue;
                    }

                    for (int v = 0; v < nAp; v++)
                    {
                        if (alcancados[v])
                        {
                            continue;
                        }

                        float vao = Mathf.Max(0f, apEsq[v] - apDir[u]);
                        float sobe = apTopo[v] - apTopo[u];

                        if (sobe <= 2f && vao <= Alcance(Mathf.Max(0f, sobe)) * 0.85f)
                        {
                            alcancados[v] = true;
                            cresceu = true;
                        }
                    }
                }
            }

            chegaAte = 0f;

            for (int k = 0; k < nAp - 1; k++)
            {
                if (alcancados[k])
                {
                    chegaAte = Mathf.Max(chegaAte, apDir[k]);
                }
            }

            return alcancados[nAp - 1];
        }

        // buracos
        int col = 0;
        while (col < W)
        {
            if (alturaColuna[col] >= 0)
            {
                col++;
                continue;
            }

            int ini = col;
            while (col < W && alturaColuna[col] < 0)
            {
                col++;
            }

            int largura = col - ini;
            int antes = ini > 0 ? alturaColuna[ini - 1] : 0;
            int depois = col < W ? alturaColuna[col] : antes;
            int subida = depois - antes;
            float alcance = Alcance(Mathf.Max(0, subida));
            bool comPlataforma = false;
            for (int cp = ini; cp < col; cp++)
            {
                for (int rp = 0; rp < H; rp++)
                {
                    comPlataforma |= CharsPlataforma.IndexOf(mapa.celulas[cp, rp]) >= 0;
                }
            }

            // trampolim 3 a 7 colunas antes de uma ponte de blocos B (>= 3 blocos) que chega perto do fim do buraco
            int colTramp = -1;

            for (int ct = Mathf.Max(0, ini - 3); ct < ini; ct++)
            {
                for (int rt = 0; rt < H; rt++)
                {
                    if (mapa.celulas[ct, rt] == 'T')
                    {
                        colTramp = ct;
                    }
                }
            }

            int ponteIni = -1;
            int ponteFim = -1;

            for (int cb = ini; cb < Mathf.Min(W, col + 4); cb++)
            {
                bool temBloco = false;

                for (int rb = 0; rb < H; rb++)
                {
                    temBloco |= mapa.celulas[cb, rb] == 'B';
                }

                if (temBloco)
                {
                    if (ponteIni < 0)
                    {
                        ponteIni = cb;
                    }

                    ponteFim = cb;
                }
            }

            int colsPonte = ponteIni >= 0 ? ponteFim - ponteIni + 1 : 0;
            bool comPonte = colTramp >= 0 && ponteIni >= 0
                && ponteIni - colTramp >= 3 && ponteIni - colTramp <= 7
                && colsPonte >= 3 && (col - 1 - ponteFim) <= 3;
            float cadeiaAte = 0f;
            bool cadeiaOk = !comPonte && largura > alcance * 0.85f && Cadeia(ini, col, antes, depois, out cadeiaAte);
            string infoEsteira = "";
            bool esteiraOk = !comPonte && !cadeiaOk && largura > alcance * 0.85f
                && AuditarEsteiras(mapa, ini, col, H, Alcance(0f), out infoEsteira);
            bool ok = comPonte || cadeiaOk || esteiraOk || largura <= alcance * 0.85f;

            if (!ok)
            {
                problemas++;
            }

            rel.AppendLine("buraco x=" + ini + ".." + (col - 1) + " largura " + largura + " (altura " + antes
                + " -> " + depois + ") alcance " + alcance.ToString("F1") + (cadeiaOk ? " | cadeia de apoios ok" : "") + (esteiraOk ? " | esteiras: " + infoEsteira : "") + (comPonte ? " | com trampolim e ponte de " + colsPonte + " blocos" : "") + (ok ? " ok" : "  <<< DIFICIL DEMAIS (os apoios chegam ate x=" + cadeiaAte.ToString("F0") + ")"));
        }

        // degraus
        for (int c = 0; c + 1 < W; c++)
        {
            if (alturaColuna[c] < 0 || alturaColuna[c + 1] < 0)
            {
                continue;
            }

            int dh = alturaColuna[c + 1] - alturaColuna[c];

            if (dh > 2)
            {
                problemas++;
                rel.AppendLine("degrau de " + dh + " un em x=" + c + "  <<< ALTO DEMAIS (pulo maximo " + apice.ToString("F1") + ")");
            }
        }

        // fileiras de espinhos
        for (int r = 0; r < H; r++)
        {
            int c = 0;

            while (c < W)
            {
                if (mapa.celulas[c, r] != '^')
                {
                    c++;
                    continue;
                }

                int ini = c;
                while (c < W && mapa.celulas[c, r] == '^')
                {
                    c++;
                }

                if (c - ini > 3)
                {
                    problemas++;
                    rel.AppendLine("fileira de espinhos x=" + ini + ".." + (c - 1) + " com " + (c - ini) + " celulas  <<< LONGA DEMAIS (max 3)");
                }
            }
        }

        // grupos de chamas: precisam deixar uma janela segura para atravessar
        float apagada = 1.5f;
        var prefabChama = AssetDatabase.LoadAssetAtPath<GameObject>(PastaPrefabs + "Trap_Fire.prefab");

        if (prefabChama != null)
        {
            var soChama = new SerializedObject(prefabChama.GetComponent<FireTrap>());
            apagada = soChama.FindProperty("tempoApagada").floatValue;
        }

        for (int r = 0; r < H; r++)
        {
            int c = 0;

            while (c < W)
            {
                if (mapa.celulas[c, r] != 'f')
                {
                    c++;
                    continue;
                }

                int ini = c;

                while (c < W && mapa.celulas[c, r] == 'f')
                {
                    c++;
                }

                // Em pilares empilhados, so a linha de baixo entra no relatorio.
                if (r + 1 < H && mapa.celulas[ini, r + 1] == 'f')
                {
                    continue;
                }

                int larg = c - ini;
                float travessia = (larg + 0.6f) / vel;
                float janela = apagada - travessia;
                bool ok = janela >= 1f;

                if (!ok)
                {
                    problemas++;
                }

                rel.AppendLine("chamas x=" + ini + ".." + (c - 1) + " (largura " + larg + "): travessia " + travessia.ToString("F2")
                    + " s | janela segura " + janela.ToString("F2") + " s" + (ok ? " ok" : "  <<< SEM JANELA SEGURA (min 1 s)"));
            }
        }

        // inimigos explosivos: quem pisa precisa ter uma saida livre quando o pavio acender
        for (int c = 0; c < W; c++)
        {
            for (int r = 0; r < H; r++)
            {
                if (mapa.celulas[c, r] != 'Z')
                {
                    continue;
                }

                bool[] livre = new bool[2];

                for (int lado = 0; lado < 2; lado++)
                {
                    int dir = lado == 0 ? -1 : 1;
                    bool bloqueado = false;

                    for (int d = 1; d <= 3 && !bloqueado; d++)
                    {
                        int cc = c + (dir * d);

                        // Fim do mapa, degrau ou buraco contam como saida.
                        if (cc < 0 || cc >= W || alturaColuna[cc] != alturaColuna[c])
                        {
                            break;
                        }

                        bloqueado = "^SfB".IndexOf(mapa.celulas[cc, r]) >= 0;
                    }

                    livre[lado] = !bloqueado;
                }

                bool temSaida = livre[0] || livre[1];

                if (!temSaida)
                {
                    problemas++;
                }

                rel.AppendLine("explosivo x=" + c + ": saida livre a esquerda=" + livre[0] + " direita=" + livre[1]
                    + (temSaida ? " ok" : "  <<< SEM SAIDA"));
            }
        }

        // objetos sem apoio
        for (int c = 0; c < W; c++)
        {
            for (int r = 0; r < H - 1; r++)
            {
                char ch = mapa.celulas[c, r];

                if ("^SEKTHfZ".IndexOf(ch) < 0)
                {
                    continue;
                }

                char abaixo = mapa.celulas[c, r + 1];
                bool apoio = solido[c, r + 1] || abaixo == 'B' || abaixo == 'f';

                if (!apoio)
                {
                    problemas++;
                    rel.AppendLine("'" + ch + "' em x=" + c + " sem apoio embaixo  <<< FLUTUANDO");
                }
            }
        }

        // ilhas flutuantes: folga por baixo e alcance de pulo ate a ilha
        var ilhaVista = new bool[W, H];
        for (int ic = 0; ic < W; ic++)
        {
            for (int ir = 0; ir < H; ir++)
            {
                if (CharsIlha.IndexOf(mapa.celulas[ic, ir]) < 0 || ilhaVista[ic, ir])
                {
                    continue;
                }

                int ilhaIni = ic;
                int ilhaFim = ic;

                while (ilhaFim + 1 < W && CharsIlha.IndexOf(mapa.celulas[ilhaFim + 1, ir]) >= 0)
                {
                    ilhaFim++;
                }

                for (int ik = ilhaIni; ik <= ilhaFim; ik++)
                {
                    ilhaVista[ik, ir] = true;
                }

                int nivelIlha = mapa.linhaChao - ir;
                int folga = int.MaxValue;

                for (int ik = ilhaIni; ik <= ilhaFim; ik++)
                {
                    if (alturaColuna[ik] >= 0)
                    {
                        folga = Mathf.Min(folga, nivelIlha - alturaColuna[ik] - 1);
                    }
                }

                bool sobreBuraco = alturaColuna[ilhaIni] < 0 && alturaColuna[ilhaFim] < 0;
                bool alcancavel = sobreBuraco;
                int apoioCol = -1;

                // Apoio dos dois lados: a ilha pode ser alcancada vindo da esquerda ou voltando
                // de um ponto mais a frente. Blocos marrons (B) tambem servem de apoio.
                for (int passo = 1; passo <= 6 && !alcancavel; passo++)
                {
                    foreach (int cc in new[] { ilhaIni - passo, ilhaFim + passo })
                    {
                        if (cc < 0 || cc >= mapa.largura || alcancavel)
                        {
                            continue;
                        }

                        int espaco = passo - 1;

                        for (int rr = 0; rr < H && !alcancavel; rr++)
                        {
                            bool ehChao = alturaColuna[cc] >= 0 && mapa.linhaChao - alturaColuna[cc] == rr;
                            bool ehApoio = (CharsIlha + "B").IndexOf(mapa.celulas[cc, rr]) >= 0;

                            if (!ehChao && !ehApoio)
                            {
                                continue;
                            }

                            int subir = nivelIlha - (mapa.linhaChao - rr);

                            if (subir <= 2 && espaco <= Alcance(Mathf.Max(0, subir)) * 0.85f)
                            {
                                alcancavel = true;
                                apoioCol = cc;
                            }
                        }
                    }
                }

                bool folgaOk = folga == int.MaxValue || folga >= 2;

                if (!folgaOk || !alcancavel)
                {
                    problemas++;
                }

                rel.AppendLine("ilha x=" + ilhaIni + ".." + ilhaFim + " (nivel " + nivelIlha + "): folga "
                    + (folga == int.MaxValue ? "livre" : folga + " un") + (folgaOk ? "" : " <<< BAIXA DEMAIS (min 2)")
                    + " | " + (sobreBuraco ? "sobre buraco (validada pela cadeia dele)" : alcancavel ? "alcancavel a partir de x=" + apoioCol : "<<< INALCANCAVEL"));
            }
        }

        // casa de chegada: tocha, barril e cerca ficam de -3,5 a +6,5 un da porta e precisam de chao plano
        for (int c = 0; c < W; c++)
        {
            for (int r = 0; r < H; r++)
            {
                if (mapa.celulas[c, r] != 'H')
                {
                    continue;
                }

                const int antes = 4;
                const int depois = 7;
                int faltam = 0;

                for (int i = c - antes; i <= c + depois; i++)
                {
                    if (i < 0 || i >= W || alturaColuna[i] != alturaColuna[c])
                    {
                        faltam++;
                    }
                }

                if (faltam > 0)
                {
                    problemas++;
                }

                rel.AppendLine("casa x=" + c + ": chao plano de -" + antes + " a +" + depois + " un da porta "
                    + (faltam == 0 ? "ok" : " <<< FALTAM " + faltam + " colunas (tocha, barril e cerca ficariam flutuando)"));
            }
        }

        rel.AppendLine(problemas == 0 ? "AUDITORIA: nenhum problema encontrado" : "AUDITORIA: " + problemas + " problema(s)");
    }

    /// <summary>
    /// Define em que ponto do ciclo a chama comeca (grupos vizinhos ficam defasados).
    /// </summary>
    private static void ConfigurarChama(GameObject chama, float fase)
    {
        if (chama == null)
        {
            return;
        }

        var so = new SerializedObject(chama.GetComponent<FireTrap>());
        so.FindProperty("faseInicial").floatValue = fase;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Configura o eixo e o alcance da plataforma movel e desenha a corrente do
    /// trajeto. A corrente fica presa ao chunk, para cair junto com o chao.
    /// </summary>
    /// <summary>
    /// Defasa plataformas moveis do mesmo tipo que estao em sequencia na mesma linha,
    /// para a fila subir e descer em onda em vez de todas juntas.
    /// </summary>
    /// <summary>
    /// Monta uma esteira de plataformas numa coluna: varias plataformas igualmente
    /// espacadas percorrendo o mesmo trecho vertical, todas no mesmo sentido.
    /// O trecho vai do chao/base ate o teto daquela coluna.
    /// </summary>
    private static void CriarEsteira(Mapa mapa, int c, int r, bool sobe, Transform pai,
        float origemX, float origemY, int H, bool[,] solido,
        System.Func<string, Vector3, Transform, GameObject> instanciar)
    {
        float cx = origemX + c + 0.5f;

        // topo: primeira celula solida acima (teto). base: primeira solida abaixo (chao).
        int rTeto = -1;
        for (int rr = r - 1; rr >= 0; rr--)
        {
            if (solido[c, rr]) { rTeto = rr; break; }
        }

        int rChao = H;
        for (int rr = r + 1; rr < H; rr++)
        {
            if (solido[c, rr]) { rChao = rr; break; }
        }

        float yCima = origemY + (H - 1 - (rTeto + 1)) + 0.5f;
        float yBaixo = origemY + (H - 1 - (rChao - 1)) + 0.25f;

        if (yCima - yBaixo < 2f)
        {
            Debug.LogWarning("Esteira em c=" + c + " r=" + r + " tem vao vertical pequeno demais.");
            return;
        }

        float vao = yCima - yBaixo;
        float espacoAlvo = LerFloat(Tema(mapa, "esteiraEspaco", "4.5"), 4.5f);
        float velocidade = LerFloat(Tema(mapa, "esteiraVel", "1.6"), 1.6f);
        int quantidade = Mathf.Max(2, Mathf.RoundToInt(vao / espacoAlvo));
        float espacamento = vao / quantidade;

        // colunas vizinhas comecam em alturas diferentes
        float fase = (c * 1.7f) % espacamento;

        for (int i = 0; i < quantidade; i++)
        {
            float y = yBaixo + ((fase + (i * espacamento)) % vao);
            GameObject plat = instanciar("Platform_MovingGrey", new Vector3(cx, y, 0f), pai);

            if (plat == null)
            {
                continue;
            }

            MovingFallingPlatform comum = plat.GetComponent<MovingFallingPlatform>();

            if (comum != null)
            {
                comum.enabled = false;
            }

            PlataformaEsteira esteira = plat.GetComponent<PlataformaEsteira>();

            if (esteira == null)
            {
                esteira = plat.AddComponent<PlataformaEsteira>();
            }

            esteira.Configurar(sobe ? PlataformaEsteira.Sentido.Sobe : PlataformaEsteira.Sentido.Desce,
                yBaixo, yCima, velocidade);
        }
    }

    private static float LerFloat(string texto, float padrao)
    {
        return float.TryParse(texto, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : padrao;
    }

    /// <summary>
    /// Buraco largo atravessado por colunas de esteira: vale se o vao horizontal entre colunas
    /// vizinhas (e entre as pontas e a primeira/ultima coluna) for pulavel. Como as plataformas
    /// se movem na vertical, so a distancia horizontal importa; usa 70% do alcance de folga.
    /// </summary>
    private static bool AuditarEsteiras(Mapa mapa, int ini, int fim, int H, float alcancePlano, out string info)
    {
        var cols = new List<int>();

        for (int c = ini; c < fim; c++)
        {
            for (int r = 0; r < H; r++)
            {
                char ch = mapa.celulas[c, r];

                if (ch == 'u' || ch == 'd')
                {
                    cols.Add(c);
                    break;
                }
            }
        }

        info = "";

        if (cols.Count < 2)
        {
            return false;
        }

        float limite = alcancePlano * 0.7f;
        float maior = Mathf.Max((cols[0] + 0.5f - 1f) - ini, fim - (cols[cols.Count - 1] + 0.5f + 1f));

        for (int i = 0; i + 1 < cols.Count; i++)
        {
            maior = Mathf.Max(maior, (cols[i + 1] - cols[i]) - 2f);
        }

        info = cols.Count + " colunas, vao max " + maior.ToString("F1") + " un (limite " + limite.ToString("F1") + ")";
        return maior <= limite;
    }

    private static void DefasarPlataforma(GameObject plataforma, Mapa mapa, int c, int r, char ch)
    {
        if (plataforma == null)
        {
            return;
        }

        // conta quantas plataformas iguais existem antes desta, na mesma linha
        int indice = 0;
        int total = 1;

        for (int cc = 0; cc < mapa.largura; cc++)
        {
            if (cc == c || mapa.celulas[cc, r] != ch)
            {
                continue;
            }

            total++;

            if (cc < c)
            {
                indice++;
            }
        }

        if (total < 2)
        {
            return;
        }

        MovingFallingPlatform movel = plataforma.GetComponent<MovingFallingPlatform>();

        if (movel == null)
        {
            return;
        }

        var so = new SerializedObject(movel);
        so.FindProperty("defasagem").floatValue = (indice * 2f * Mathf.PI) / total;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CriarPlataformaMovel(GameObject plataforma, bool vertical, Transform chunk)
    {
        if (plataforma == null)
        {
            return;
        }

        MovingFallingPlatform movel = plataforma.GetComponent<MovingFallingPlatform>();

        if (movel != null)
        {
            var so = new SerializedObject(movel);
            so.FindProperty("eixo").enumValueIndex = vertical ? 1 : 0;
            so.FindProperty("distancia").floatValue = DistanciaPlataforma;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Sprite elo = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteCorrente);

        if (elo == null)
        {
            return;
        }

        var corrente = new GameObject("Corrente");
        corrente.transform.SetParent(chunk, true);
        corrente.transform.position = plataforma.transform.position;
        Vector3 direcao = vertical ? Vector3.up : Vector3.right;

        for (float d = -DistanciaPlataforma; d <= DistanciaPlataforma + 0.001f; d += 0.25f)
        {
            var go = new GameObject("Elo");
            go.transform.SetParent(corrente.transform, false);
            go.transform.localPosition = direcao * d;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = elo;
            sr.sortingOrder = -1;
        }
    }

    // ------------------------------------------------------------------
    // Leitura do mapa
    // ------------------------------------------------------------------
    /// <summary>
    /// Torre: o inimigo patrulha a plataforma inteira, mas so persegue quem esta em pe nela
    /// (mesmo nivel). Quem passa pulando entre plataformas e ignorado; do contrario o inimigo
    /// correria ate a borda e ficaria parado onde o jogador precisa aterrissar.
    /// So age em mapas verticais.
    /// </summary>
    private static void AjustarInimigoTorre(GameObject inimigo, Mapa mapa, int c, int r, int W, float origemX)
    {
        if (inimigo == null || !mapa.vertical || r + 1 >= mapa.altura)
        {
            return;
        }

        const string Superficie = "IRO#B";

        if (Superficie.IndexOf(mapa.celulas[c, r + 1]) < 0)
        {
            return;
        }

        int x0 = c;
        int x1 = c;

        while (x0 - 1 >= 0 && Superficie.IndexOf(mapa.celulas[x0 - 1, r + 1]) >= 0)
        {
            x0--;
        }

        while (x1 + 1 < W && Superficie.IndexOf(mapa.celulas[x1 + 1, r + 1]) >= 0)
        {
            x1++;
        }

        float inicio = x0;
        float fim = x1 + 1;
        float comprimento = fim - inicio;
        float centro = (inicio + fim) * 0.5f;
        float patrulha = Mathf.Max(0.8f, (comprimento * 0.5f) - 0.7f);

        Vector3 pos = inimigo.transform.position;
        pos.x = origemX + centro;
        inimigo.transform.position = pos;

        var so = new SerializedObject(inimigo.GetComponent<Enemy>());
        so.FindProperty("perseguirSoNoMesmoNivel").boolValue = true;
        so.FindProperty("distanciaPatrulha").floatValue = patrulha;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static string Tema(Mapa mapa, string chave, string padrao)
    {
        return mapa.diretivas.TryGetValue(chave, out string valor) && valor.Length > 0 ? valor : padrao;
    }

    private static GameObject AjustarFrutaCaixa(GameObject caixa, string nomePrefabFruta)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PastaPrefabs + nomePrefabFruta + ".prefab");
        Box box = caixa != null ? caixa.GetComponent<Box>() : null;

        if (prefab == null || box == null)
        {
            return caixa;
        }

        var so = new SerializedObject(box);
        so.FindProperty("prefabFruta").objectReferenceValue = prefab;
        so.ApplyModifiedPropertiesWithoutUndo();
        return caixa;
    }

    private static Mapa Ler(string caminho)
    {
        string[] brutas = File.ReadAllLines(caminho);
        var linhas = new List<string>();
        int linhaChao = -1;
        var diretivas = new Dictionary<string, string>();

        foreach (string bruta in brutas)
        {
            string l = bruta.TrimEnd('\r');

            if (l.StartsWith("//"))
            {
                continue;
            }

            if (l.StartsWith("@chao="))
            {
                linhaChao = int.Parse(l.Substring(6).Trim());
                continue;
            }

            if (l.StartsWith("@"))
            {
                int igual = l.IndexOf('=');

                if (igual > 1)
                {
                    diretivas[l.Substring(1, igual - 1).Trim()] = l.Substring(igual + 1).Trim();
                }

                continue;
            }

            linhas.Add(l);
        }

        while (linhas.Count > 0 && linhas[linhas.Count - 1].Trim().Length == 0)
        {
            linhas.RemoveAt(linhas.Count - 1);
        }

        int altura = linhas.Count;
        int largura = 0;

        foreach (string l in linhas)
        {
            largura = Mathf.Max(largura, l.Length);
        }

        var celulas = new char[largura, altura];

        for (int r = 0; r < altura; r++)
        {
            for (int c = 0; c < largura; c++)
            {
                celulas[c, r] = c < linhas[r].Length ? linhas[r][c] : ' ';
            }
        }

        return new Mapa { largura = largura, altura = altura, linhaChao = linhaChao, celulas = celulas, diretivas = diretivas };
    }

    // ------------------------------------------------------------------
    // Tiles
    // ------------------------------------------------------------------
    private static void CarregarSprites()
    {
        sprites = new Dictionary<Vector2Int, Sprite>();

        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(Atlas))
        {
            if (o is Sprite s)
            {
                sprites[new Vector2Int((int)s.rect.x, (int)s.rect.y)] = s;
            }
        }
    }

    /// <summary>
    /// Cria de uma vez os assets de Tile usados (em lote, para nao importar um a um).
    /// </summary>
    private static void PrepararTiles()
    {
        if (!AssetDatabase.IsValidFolder(PastaTiles))
        {
            AssetDatabase.CreateFolder("Assets", "Tiles");
        }

        var necessarios = new List<Vector2Int>
        {
            BlocoTL, BlocoTR, BlocoBL, BlocoBR
        };

        Vector2Int[] offsets = { TL, TM, TR, ML, MM, MR, BL, BM, BR, CantoDiagEsqVazia, CantoDiagDirVazia };

        foreach (Vector2Int origem in Estilos.Values)
        {
            foreach (Vector2Int off in offsets)
            {
                necessarios.Add(origem + off);
            }
        }

        AssetDatabase.StartAssetEditing();

        try
        {
            foreach (Vector2Int pos in necessarios)
            {
                string caminho = CaminhoTile(pos);

                if (AssetDatabase.LoadAssetAtPath<Tile>(caminho) != null || !sprites.ContainsKey(pos))
                {
                    continue;
                }

                var t = ScriptableObject.CreateInstance<Tile>();
                t.sprite = sprites[pos];
                t.colliderType = Tile.ColliderType.Grid;
                AssetDatabase.CreateAsset(t, caminho);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        tiles.Clear();
    }

    private static string CaminhoTile(Vector2Int pos)
    {
        return PastaTiles + "/Terrain_" + pos.x + "_" + pos.y + ".asset";
    }

    private static Tile ObterTile(Vector2Int pos)
    {
        if (tiles.TryGetValue(pos, out Tile existente) && existente != null)
        {
            return existente;
        }

        var tile = AssetDatabase.LoadAssetAtPath<Tile>(CaminhoTile(pos));
        tiles[pos] = tile;
        return tile;
    }
}
