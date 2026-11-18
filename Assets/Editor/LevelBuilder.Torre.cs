using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Parte do LevelBuilder para fases em torre (diretiva @modo=vertical): configura onda, camera,
/// progresso e parallax para a subida, ajusta pedras que esmagam e audita se a torre e
/// escalavel com o pulo real do jogador (medido na fisica: pulo de 2,34 un).
/// </summary>
public static partial class LevelBuilder
{
    // Maior vao horizontal (borda a borda) que o sapo atravessa saltando da borda de uma
    // plataforma para outra com topo dy acima/abaixo. Valores medidos no Physics2D com os
    // parametros reais do PlayerController (velocidade 7, pulo 13, gravidade 3,5, queda x2,2).
    private static readonly float[] AlturasMedidas = { -2f, 0f, 1f, 1.5f, 2f, 2.25f, 2.5f };
    private static readonly float[] VaosMedidos = { 5.65f, 5.0f, 4.55f, 4.2f, 3.8f, 3.55f, 3.0f };

    private const float AlcanceTrampolimVertical = 6.2f;
    private const float AlcanceTrampolimHorizontal = 4.5f;
    private const float LimiteRazaoAlcancavel = 0.92f;

    [MenuItem("Tools/Level Builder/Reconstruir Fase 3")]
    private static void MenuFase3()
    {
        Debug.Log(Construir("Assets/Levels/Phase3.txt"));
    }

    private static float NumeroDiretiva(Mapa mapa, string chave, float padrao)
    {
        string texto = Tema(mapa, chave, "");

        if (texto.Length > 0 && float.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out float valor))
        {
            return valor;
        }

        return padrao;
    }

    private static void DefinirNumero(SerializedObject so, string campo, float valor)
    {
        SerializedProperty p = so.FindProperty(campo);

        if (p == null)
        {
            return;
        }

        if (p.propertyType == SerializedPropertyType.Integer)
        {
            p.intValue = Mathf.RoundToInt(valor);
        }
        else
        {
            p.floatValue = valor;
        }
    }

    private static void AjustarVelocidade(GameObject plataforma, string valor)
    {
        if (plataforma == null || valor.Length == 0 ||
            !float.TryParse(valor, NumberStyles.Float, CultureInfo.InvariantCulture, out float velocidade))
        {
            return;
        }

        MovingFallingPlatform movel = plataforma.GetComponent<MovingFallingPlatform>();

        if (movel == null)
        {
            return;
        }

        var so = new SerializedObject(movel);
        so.FindProperty("velocidade").floatValue = velocidade;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigurarPedra(GameObject pedra, float distancia, int coluna, int linha)
    {
        if (pedra == null)
        {
            return;
        }

        PedraEsmagadora comp = pedra.GetComponent<PedraEsmagadora>();

        if (comp == null)
        {
            return;
        }

        var so = new SerializedObject(comp);
        so.FindProperty("distancia").floatValue = distancia;
        so.FindProperty("faseInicial").floatValue = Mathf.Repeat((coluna * 0.9f) + (linha * 0.37f), 2.1f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Deixa a cena pronta para a subida: onda abaixo do jogador subindo, camera centrada
    /// na torre, progresso medido em altura e parallax com rolagem vertical.
    /// </summary>
    private static void ConfigurarTorre(Mapa mapa, float origemX, int largura, float yJogador, StringBuilder rel)
    {
        float centroX = origemX + (largura * 0.5f);
        float inicioOnda = NumeroDiretiva(mapa, "ondaInicio", 14f);
        float atrasoMaximo = NumeroDiretiva(mapa, "ondaAtraso", 16f);
        float alturaCamera = NumeroDiretiva(mapa, "cameraAltura", 2.5f);

        DestructionWave onda = Object.FindFirstObjectByType<DestructionWave>();

        if (onda != null)
        {
            onda.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            onda.transform.position = new Vector3(centroX, yJogador - inicioOnda, 0f);

            var so = new SerializedObject(onda);
            so.FindProperty("modoVertical").boolValue = true;
            DefinirNumero(so, "distanciaMaximaDoPlayer", atrasoMaximo);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Camera cam = Camera.main;

        if (cam != null)
        {
            cam.transform.position = new Vector3(centroX, yJogador + alturaCamera, -10f);
            CameraFollow seguir = cam.GetComponent<CameraFollow>();

            if (seguir != null)
            {
                var so = new SerializedObject(seguir);
                so.FindProperty("modoVertical").boolValue = true;
                so.FindProperty("xFixo").floatValue = centroX;
                so.FindProperty("deslocamento").vector3Value = new Vector3(0f, alturaCamera, -10f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        GameManager gm = Object.FindFirstObjectByType<GameManager>();

        if (gm != null)
        {
            var so = new SerializedObject(gm);
            so.FindProperty("modoVertical").boolValue = true;

            string[] tresValores = Tema(mapa, "onda", "").Split(',');

            if (tresValores.Length == 3 &&
                float.TryParse(tresValores[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float v0) &&
                float.TryParse(tresValores[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float aceleracao) &&
                float.TryParse(tresValores[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float vMax))
            {
                so.FindProperty("waveSpeedInicial").floatValue = v0;
                so.FindProperty("waveAceleracao").floatValue = aceleracao;
                so.FindProperty("waveSpeedMaxima").floatValue = vMax;
            }

            if (mapa.diretivas.TryGetValue("nomeFase", out string nome))
            {
                so.FindProperty("nomeFase").stringValue = nome;
            }

            if (mapa.diretivas.TryGetValue("proximaFase", out string proxima))
            {
                so.FindProperty("proximaFase").stringValue = proxima;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        ScoreManager placar = Object.FindFirstObjectByType<ScoreManager>();

        if (placar != null && mapa.diretivas.ContainsKey("tempoAlvo"))
        {
            var so = new SerializedObject(placar);
            DefinirNumero(so, "tempoAlvo", NumeroDiretiva(mapa, "tempoAlvo", 100f));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        int camadas = 0;

        foreach (ParallaxLayer camada in Object.FindObjectsByType<ParallaxLayer>(FindObjectsSortMode.None))
        {
            var so = new SerializedObject(camada);
            so.FindProperty("modoVertical").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            camadas++;
        }

        rel.AppendLine("torre configurada: centro x=" + centroX.ToString("F2") + " | onda em y=" + (yJogador - inicioOnda).ToString("F1")
            + " | camera y=" + (yJogador + alturaCamera).ToString("F1") + " | camadas de parallax verticais: " + camadas);
    }

    // ------------------------------------------------------------------
    // Auditoria da torre
    // ------------------------------------------------------------------
    private class Apoio
    {
        public string nome;
        public float x0;
        public float x1;
        public float topo;
        public float trampolimX = float.NaN;
        public string veiculo;   // as duas posicoes do mesmo elevador compartilham este nome
    }

    private static float VaoMaximo(float dy)
    {
        if (dy > 2.5f)
        {
            return 0f;
        }

        if (dy <= AlturasMedidas[0])
        {
            return VaosMedidos[0] + ((AlturasMedidas[0] - dy) * 0.35f);
        }

        for (int i = 1; i < AlturasMedidas.Length; i++)
        {
            if (dy <= AlturasMedidas[i])
            {
                float t = (dy - AlturasMedidas[i - 1]) / (AlturasMedidas[i] - AlturasMedidas[i - 1]);
                return Mathf.Lerp(VaosMedidos[i - 1], VaosMedidos[i], t);
            }
        }

        return 0f;
    }

    private static bool Aresta(Apoio a, Apoio b, out float razao)
    {
        razao = 99f;

        // subir ou descer no mesmo elevador nao e salto: o sapo vai junto com a plataforma
        if (a.veiculo != null && a.veiculo == b.veiculo)
        {
            razao = 0f;
            return true;
        }

        float dy = b.topo - a.topo;
        float vao = Mathf.Max(0f, Mathf.Max(b.x0 - a.x1, a.x0 - b.x1));
        float maximo = VaoMaximo(dy);

        if (maximo > 0f)
        {
            razao = vao / maximo;

            if (razao <= LimiteRazaoAlcancavel)
            {
                return true;
            }
        }

        if (!float.IsNaN(a.trampolimX) && dy <= AlcanceTrampolimVertical)
        {
            float distancia = Mathf.Max(0f, Mathf.Max(b.x0 - a.trampolimX, a.trampolimX - b.x1));
            float razaoTrampolim = distancia / AlcanceTrampolimHorizontal;

            if (razaoTrampolim <= 1f)
            {
                razao = Mathf.Max(0.3f, razaoTrampolim);
                return true;
            }
        }

        return false;
    }

    private static void AuditarTorre(Mapa mapa, bool[,] solido, StringBuilder rel)
    {
        int W = mapa.largura;
        int H = mapa.altura;
        int avisos = 0;
        int erros = 0;
        rel.AppendLine("--- Auditoria da torre ---");

        int paredeEsq = 0;
        while (paredeEsq < W && solido[paredeEsq, 0])
        {
            paredeEsq++;
        }

        int paredeDir = 0;
        while (paredeDir < W && solido[W - 1 - paredeDir, 0])
        {
            paredeDir++;
        }

        int colMin = paredeEsq;
        int colMax = W - 1 - paredeDir;
        rel.AppendLine("paredes: " + paredeEsq + " col a esquerda e " + paredeDir + " a direita | interior: colunas " + colMin + " a " + colMax
            + " (" + (colMax - colMin + 1) + " un de largura; a tela 16:9 mostra cerca de 17,8 un)");

        if (colMax - colMin + 1 > 16)
        {
            rel.AppendLine("AVISO: interior mais largo que 16 un; em telas mais estreitas que 16:9 as bordas ficam cortadas.");
            avisos++;
        }

        float Topo(int linha)
        {
            return 0.5f + (mapa.linhaChao - linha);
        }

        float YBase(int linha)
        {
            return 0.5f + (mapa.linhaChao - 1 - linha);
        }

        // ---------- apoios ----------
        var apoios = new List<Apoio>();

        for (int r = 0; r < H; r++)
        {
            int c = colMin;

            while (c <= colMax)
            {
                bool exposta = solido[c, r] && (r == 0 || !solido[c, r - 1]);

                if (!exposta)
                {
                    c++;
                    continue;
                }

                int c0 = c;

                while (c <= colMax && solido[c, r] && (r == 0 || !solido[c, r - 1]))
                {
                    c++;
                }

                apoios.Add(new Apoio { nome = "linha " + r + " col " + c0 + "-" + (c - 1), x0 = c0, x1 = c, topo = Topo(r) });
            }
        }

        int totalSolidos = apoios.Count;
        int colP = -1;
        int linP = -1;
        int colK = -1;
        int linK = -1;
        int colH = -1;
        int linH = -1;

        for (int c = 0; c < W; c++)
        {
            for (int r = 0; r < H; r++)
            {
                char ch = mapa.celulas[c, r];

                switch (ch)
                {
                    case 'P':
                        colP = c;
                        linP = r;
                        break;
                    case 'K':
                        colK = c;
                        linK = r;
                        break;
                    case 'H':
                        colH = c;
                        linH = r;
                        break;
                    case 'V':
                    {
                        float topoMeio = YBase(r) + 0.25f;
                        apoios.Add(new Apoio { nome = "elevador col " + c + " linha " + r + " (embaixo)", x0 = c - 0.5f, x1 = c + 1.5f, topo = topoMeio - DistanciaPlataforma, veiculo = "V" + c + "," + r });
                        apoios.Add(new Apoio { nome = "elevador col " + c + " linha " + r + " (em cima)", x0 = c - 0.5f, x1 = c + 1.5f, topo = topoMeio + DistanciaPlataforma, veiculo = "V" + c + "," + r });
                        break;
                    }
                    case 'M':
                        apoios.Add(new Apoio { nome = "plataforma movel col " + c + " linha " + r, x0 = c - 0.5f - DistanciaPlataforma, x1 = c + 1.5f + DistanciaPlataforma, topo = YBase(r) + 0.25f });
                        break;
                    case 'Q':
                        apoios.Add(new Apoio { nome = "plataforma que cai col " + c + " linha " + r, x0 = c - 0.5f, x1 = c + 1.5f, topo = YBase(r) + 0.25f });
                        break;
                }
            }
        }

        if (colP < 0 || colK < 0)
        {
            rel.AppendLine("ERRO: faltam o inicio do jogador (P) ou a bandeira (K).");
            return;
        }

        // trampolins: pertencem ao apoio solido que os sustenta
        for (int c = 0; c < W; c++)
        {
            for (int r = 0; r < H; r++)
            {
                if (mapa.celulas[c, r] != 'T')
                {
                    continue;
                }

                float base0 = YBase(r);
                bool achou = false;

                for (int i = 0; i < apoios.Count; i++)
                {
                    if (Mathf.Abs(apoios[i].topo - base0) < 0.05f && apoios[i].x0 <= c + 0.5f && c + 0.5f <= apoios[i].x1)
                    {
                        apoios[i].trampolimX = c + 0.5f;
                        achou = true;
                        break;
                    }
                }

                if (!achou)
                {
                    rel.AppendLine("ERRO: trampolim na coluna " + c + ", linha " + r + " sem plataforma embaixo.");
                    erros++;
                }
            }
        }

        // ---------- apoio inicial e final ----------
        int inicio = -1;
        int meta = -1;
        float baseP = YBase(linP);
        float baseK = YBase(linK);

        for (int i = 0; i < apoios.Count; i++)
        {
            if (Mathf.Abs(apoios[i].topo - baseP) < 0.05f && apoios[i].x0 <= colP + 0.5f && colP + 0.5f <= apoios[i].x1)
            {
                inicio = i;
            }

            if (Mathf.Abs(apoios[i].topo - baseK) < 0.05f && apoios[i].x0 <= colK + 0.5f && colK + 0.5f <= apoios[i].x1)
            {
                meta = i;
            }
        }

        if (inicio < 0 || meta < 0)
        {
            rel.AppendLine("ERRO: " + (inicio < 0 ? "o jogador (P) nao esta sobre uma plataforma. " : "") + (meta < 0 ? "a bandeira (K) nao esta sobre uma plataforma." : ""));
            return;
        }

        // ---------- caminho mais folgado (minimiza o salto mais dificil) ----------
        int n = apoios.Count;
        var pior = new float[n];
        var anterior = new int[n];
        var visitado = new bool[n];

        for (int i = 0; i < n; i++)
        {
            pior[i] = float.PositiveInfinity;
            anterior[i] = -1;
        }

        pior[inicio] = 0f;

        for (int passo = 0; passo < n; passo++)
        {
            int u = -1;

            for (int i = 0; i < n; i++)
            {
                if (!visitado[i] && !float.IsPositiveInfinity(pior[i]) && (u < 0 || pior[i] < pior[u]))
                {
                    u = i;
                }
            }

            if (u < 0)
            {
                break;
            }

            visitado[u] = true;

            for (int v = 0; v < n; v++)
            {
                if (visitado[v] || !Aresta(apoios[u], apoios[v], out float razao))
                {
                    continue;
                }

                float candidato = Mathf.Max(pior[u], razao);

                if (candidato < pior[v])
                {
                    pior[v] = candidato;
                    anterior[v] = u;
                }
            }
        }

        int alcancaveis = 0;

        for (int i = 0; i < n; i++)
        {
            if (!float.IsPositiveInfinity(pior[i]))
            {
                alcancaveis++;
            }
        }

        rel.AppendLine("apoios: " + totalSolidos + " superficies solidas + " + (n - totalSolidos) + " moveis/que caem | alcancaveis a partir do inicio: " + alcancaveis + " de " + n);

        float topoInicio = apoios[inicio].topo;
        float topoMeta = apoios[meta].topo;
        rel.AppendLine("altura da subida: " + (topoMeta - topoInicio).ToString("F1") + " un (do chao ate a bandeira)");

        if (float.IsPositiveInfinity(pior[meta]))
        {
            rel.AppendLine("ERRO: a bandeira NAO e alcancavel a partir do inicio com o pulo medido.");
            erros++;
        }
        else
        {
            var rota = new List<int>();

            for (int i = meta; i >= 0; i = anterior[i])
            {
                rota.Add(i);
            }

            rota.Reverse();
            int faceis = 0;
            int medios = 0;
            int dificeis = 0;
            float maiorRazao = 0f;
            string maisDificil = "";

            for (int k = 0; k + 1 < rota.Count; k++)
            {
                Aresta(apoios[rota[k]], apoios[rota[k + 1]], out float razao);

                if (razao <= 0.6f)
                {
                    faceis++;
                }
                else if (razao <= 0.8f)
                {
                    medios++;
                }
                else
                {
                    dificeis++;
                }

                if (razao > maiorRazao)
                {
                    maiorRazao = razao;
                    maisDificil = apoios[rota[k]].nome + " -> " + apoios[rota[k + 1]].nome + " (dy " + (apoios[rota[k + 1]].topo - apoios[rota[k]].topo).ToString("F2") + ")";
                }
            }

            rel.AppendLine("rota ate a bandeira: " + (rota.Count - 1) + " saltos | faceis " + faceis + ", medios " + medios + ", dificeis " + dificeis
                + " | salto mais dificil usa " + Mathf.RoundToInt(maiorRazao * 100f) + "% do vao maximo: " + maisDificil);
        }

        if (alcancaveis < n)
        {
            var soltos = new List<string>();

            for (int i = 0; i < n; i++)
            {
                if (float.IsPositiveInfinity(pior[i]))
                {
                    soltos.Add(apoios[i].nome);
                }
            }

            rel.AppendLine("AVISO: " + soltos.Count + " apoio(s) fora de alcance (ilhas soltas ou enfeites): " + string.Join(" | ", soltos.GetRange(0, Mathf.Min(12, soltos.Count))));
            avisos++;
        }

        // ---------- cabecadas: plataforma solida logo acima de outra, com folga menor que o sapo ----------
        int cabecadas = 0;
        var listaCabecadas = new List<string>();

        for (int i = 0; i < totalSolidos; i++)
        {
            for (int j = 0; j < totalSolidos; j++)
            {
                if (i == j || apoios[j].topo <= apoios[i].topo)
                {
                    continue;
                }

                float sobreposicao = Mathf.Min(apoios[i].x1, apoios[j].x1) - Mathf.Max(apoios[i].x0, apoios[j].x0);
                float folga = apoios[j].topo - 1f - apoios[i].topo;

                if (sobreposicao > 0.5f && folga < 1.3f)
                {
                    cabecadas++;

                    if (listaCabecadas.Count < 6)
                    {
                        listaCabecadas.Add(apoios[i].nome + " sob " + apoios[j].nome + " (folga " + folga.ToString("F1") + ")");
                    }
                }
            }
        }

        if (cabecadas > 0)
        {
            rel.AppendLine("AVISO: " + cabecadas + " trecho(s) onde uma plataforma fica logo acima de outra (o sapo bate a cabeca ao pular): " + string.Join(" | ", listaCabecadas));
            avisos++;
        }

        // ---------- itens e armadilhas ----------
        var contagem = new Dictionary<char, int>();
        int semApoio = 0;
        int pedrasBaixas = 0;
        int cabecasSemParede = 0;

        for (int c = 0; c < W; c++)
        {
            for (int r = 0; r < H; r++)
            {
                char ch = mapa.celulas[c, r];

                if ("^fEZSTW D123Vk".IndexOf(ch) < 0 || ch == ' ')
                {
                    continue;
                }

                contagem[ch] = contagem.ContainsKey(ch) ? contagem[ch] + 1 : 1;

                if ("^EZST123".IndexOf(ch) >= 0)
                {
                    bool apoiado = r + 1 < H && solido[c, r + 1];

                    if (!apoiado)
                    {
                        rel.AppendLine("ERRO: '" + ch + "' na coluna " + c + ", linha " + r + " sem plataforma embaixo.");
                        erros++;
                        semApoio++;
                    }
                }
                else if (ch == 'f')
                {
                    int rr = r;

                    while (rr + 1 < H && mapa.celulas[c, rr + 1] == 'f')
                    {
                        rr++;
                    }

                    if (!(rr + 1 < H && solido[c, rr + 1]))
                    {
                        rel.AppendLine("ERRO: chama na coluna " + c + ", linha " + r + " sem plataforma embaixo.");
                        erros++;
                        semApoio++;
                    }
                }
                else if (ch == 'W')
                {
                    int rr = r + 1;

                    while (rr < H && !solido[c, rr])
                    {
                        rr++;
                    }

                    if (rr >= H)
                    {
                        rel.AppendLine("ERRO: pedra que esmaga na coluna " + c + ", linha " + r + " sem superficie embaixo.");
                        erros++;
                    }
                    else
                    {
                        float folgaPassagem = (YBase(r) + 0.5f - 0.655f) - Topo(rr);

                        if (folgaPassagem < 1.25f)
                        {
                            rel.AppendLine("ERRO: pedra da coluna " + c + ", linha " + r + " deixa so " + folgaPassagem.ToString("F2") + " un de passagem (o sapo tem 0,95).");
                            erros++;
                            pedrasBaixas++;
                        }
                    }
                }
                else if (ch == 'D')
                {
                    int cc = c + 1;

                    while (cc < W && !solido[cc, r])
                    {
                        cc++;
                    }

                    if (cc >= W)
                    {
                        rel.AppendLine("ERRO: cabeca de espinhos na coluna " + c + ", linha " + r + " sem parede a direita.");
                        erros++;
                        cabecasSemParede++;
                    }
                }
            }
        }

        var itens = new StringBuilder();

        foreach (KeyValuePair<char, int> par in contagem)
        {
            itens.Append(par.Key).Append('=').Append(par.Value).Append("  ");
        }

        rel.AppendLine("armadilhas e itens: " + itens);

        // ---------- chegada: bandeira e casa em chao plano ----------
        if (colH < 0 || linH != linK || colH <= colK)
        {
            rel.AppendLine("AVISO: a casa (H) deve ficar na mesma linha da bandeira (K), a direita dela.");
            avisos++;
        }
        else
        {
            bool plano = true;

            for (int c = colK - 1; c <= colH + 2 && c <= colMax; c++)
            {
                if (c < colMin || !solido[c, linK + 1] || solido[c, linK])
                {
                    plano = false;
                }
            }

            rel.AppendLine(plano ? "chegada: chao plano da bandeira ate depois da casa (ok)" : "AVISO: o chao entre a bandeira e a casa nao e plano/continuo.");

            if (!plano)
            {
                avisos++;
            }
        }

        // ---------- estimativa da onda ----------
        string[] tres = Tema(mapa, "onda", "").Split(',');

        if (tres.Length == 3 &&
            float.TryParse(tres[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float v0) &&
            float.TryParse(tres[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float acel) &&
            float.TryParse(tres[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float vMax))
        {
            float yOnda = (topoInicio + 0.55f) - NumeroDiretiva(mapa, "ondaInicio", 14f);
            float t = 0f;

            while (yOnda < topoMeta && t < 900f)
            {
                yOnda += Mathf.Min(vMax, v0 + (acel * t)) * 0.05f;
                t += 0.05f;
            }

            rel.AppendLine("onda: chega a bandeira em ~" + t.ToString("F0") + " s (v0 " + v0 + ", +" + acel + "/s, max " + vMax + ") | tempoAlvo da fase: "
                + NumeroDiretiva(mapa, "tempoAlvo", -1f).ToString("F0") + " s");
        }

        rel.AppendLine(erros == 0 && avisos == 0 ? "AUDITORIA DA TORRE: OK" : "AUDITORIA DA TORRE: " + erros + " erro(s), " + avisos + " aviso(s)");
    }
}
