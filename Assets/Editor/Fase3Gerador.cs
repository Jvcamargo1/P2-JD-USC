using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gera o mapa de texto da Fase 3 (torre do cemiterio) em Assets/Levels/Phase3.txt.
/// O mapa gerado e a fonte editavel da fase: depois de gerado, edite o .txt a mao.
/// Por seguranca, nao sobrescreve um Phase3.txt existente (use sobrescrever = true).
///
/// Geometria (medida na fisica real): 55 andares de 2 un, vaos de 1 a 3 un entre andares
/// consecutivos, nunca uma plataforma solida logo acima da outra (o sapo bateria a cabeca).
/// Andares 1 a 44 em zigue-zague: impares a direita, pares a esquerda. Cada plataforma
/// encosta na parede do seu lado e a largura e escolhida para o vao ate a anterior ficar
/// na faixa do trecho (assim a borda interna nao deriva e sempre sobra espaco).
/// </summary>
public static class Fase3Gerador
{
    private const int W = 18;          // 2 colunas de parede + 14 de interior + 2 de parede
    private const int H = 120;
    private const int Chao = 116;      // linha do chao base (superficie em y = 0,5)
    private const int Niveis = 55;     // andar 55 = cume
    private const int ColMin = 2;
    private const int ColMax = 15;
    private const string Caminho = "Assets/Levels/Phase3.txt";

    private static readonly float[] AlturasMedidas = { -2f, 0f, 1f, 1.5f, 2f, 2.25f, 2.5f };
    private static readonly float[] VaosMedidos = { 5.65f, 5.0f, 4.55f, 4.2f, 3.8f, 3.55f, 3.0f };

    private class Plat
    {
        public char tipo = '-';   // 'I' ilha solida, 'Q' plataforma que cai, '-' nao existe
        public float x0;
        public float x1;
        public int c0;
        public int c1;
        public int cq;
        public List<string> itens = new List<string>();
    }

    [MenuItem("Tools/Fase 3/Gerar mapa da torre (nao sobrescreve)")]
    private static void Menu()
    {
        Debug.Log(Gerar(false));
    }

    private static int Linha(int andar)
    {
        return Chao - (2 * andar);
    }

    private static float VaoMaximo(float dy)
    {
        if (dy > 2.5f)
        {
            return 0f;
        }

        if (dy <= AlturasMedidas[0])
        {
            return VaosMedidos[0];
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

    private static bool Liga(Plat a, Plat b, float dy, float limite)
    {
        float vao = Mathf.Max(0f, Mathf.Max(b.x0 - a.x1, a.x0 - b.x1));
        float maximo = VaoMaximo(dy);
        // andares consecutivos nunca se sobrepoem: vao minimo de 1 un (senao o sapo bate a cabeca)
        return vao >= 1f && maximo > 0f && (vao / maximo) <= limite;
    }

    private static Plat Ilha(int c0, int c1)
    {
        return new Plat { tipo = 'I', c0 = c0, c1 = c1, x0 = c0, x1 = c1 + 1 };
    }

    private static Plat Queda(int cq)
    {
        return new Plat { tipo = 'Q', cq = cq, x0 = cq - 0.5f, x1 = cq + 1.5f };
    }

    private static bool Dentro(Plat p)
    {
        if (p.tipo == 'I')
        {
            return p.c0 >= ColMin && p.c1 <= ColMax && p.c1 >= p.c0;
        }

        return p.cq - 0.5f >= ColMin && p.cq + 1.5f <= ColMax + 1;
    }

    /// <summary>
    /// Proximo andar do zigue-zague, do lado dir (+1 direita, -1 esquerda).
    /// Plataforma que cai: 2 un, a um vao da anterior (tenta o vao sorteado e depois os outros).
    /// Ilha: encosta na parede do lado dir; a largura e a mais proxima da sorteada que deixa o vao
    /// na faixa do trecho. Se nada servir, uma segunda passada aceita vao de 1 a 3 e ate 2 un a
    /// mais de largura. Devolve null se nem assim couber.
    /// </summary>
    private static Plat Proxima(Plat anterior, int dir, char tipo, int largura, int vao, int minimo, int wmax, int gmin, int gmax)
    {
        if (tipo == 'Q')
        {
            var vaos = new List<int> { vao };

            for (int g = 1; g <= gmax; g++)
            {
                if (g != vao)
                {
                    vaos.Add(g);
                }
            }

            foreach (int g in vaos)
            {
                int cq = dir > 0
                    ? Mathf.CeilToInt(anterior.x1 + g + 0.5f)
                    : Mathf.FloorToInt(anterior.x0 - g - 1.5f);
                Plat q = Queda(cq);

                if (Dentro(q))
                {
                    return q;
                }
            }

            return null;
        }

        for (int passada = 0; passada < 2; passada++)
        {
            int gLo = passada == 0 ? gmin : 1;
            int gHi = passada == 0 ? gmax : 3;
            int wHi = passada == 0 ? wmax : wmax + 2;

            for (int d = 0; d <= wHi - minimo; d++)
            {
                for (int lado = 0; lado < 2; lado++)
                {
                    if (d == 0 && lado == 1)
                    {
                        continue;
                    }

                    int w = lado == 0 ? largura - d : largura + d;

                    if (w < minimo || w > wHi)
                    {
                        continue;
                    }

                    Plat p = dir > 0 ? Ilha(ColMax - w + 1, ColMax) : Ilha(ColMin, ColMin + w - 1);
                    float g = dir > 0 ? p.x0 - anterior.x1 : anterior.x0 - p.x1;

                    if (g >= gLo && g <= gHi)
                    {
                        return p;
                    }
                }
            }
        }

        return null;
    }

    // Largura minima que os itens de um andar exigem (mesmas regras de Colocar).
    private static int LarguraExigida(int andar)
    {
        int exigida = 4;

        if (!Itens.TryGetValue(andar, out string lista))
        {
            return exigida;
        }

        foreach (string item in lista.Split(','))
        {
            if (item == "E" || item == "Z" || item == "^^" || item == "ff" || item == "W")
            {
                exigida = Mathf.Max(exigida, 6);
            }
            else if (item == "^" || item == "1" || item == "2" || item == "3")
            {
                exigida = Mathf.Max(exigida, 5);
            }
        }

        return exigida;
    }

    private static readonly HashSet<int> NiveisQueda = new HashSet<int> { 17, 18, 19, 24, 25, 26 };
    private static readonly HashSet<int> NiveisPedra = new HashSet<int> { 30, 32, 34, 36, 38, 40, 42 };

    // ---------- decoracao: itens e armadilhas por andar ----------
    private static readonly Dictionary<int, string> Itens = new Dictionary<int, string>
    {
        { 1, "F" }, { 2, "F" }, { 3, "E,F" }, { 4, "FF" }, { 5, "^,F" }, { 6, "F" },
        { 7, "1,F" }, { 8, "F,k" }, { 9, "^^,F" }, { 10, "E,F" }, { 11, "FF" }, { 12, "^,k" },
        { 13, "FF" }, { 14, "ff,F" }, { 15, "E,F" }, { 16, "^,F" }, { 17, "F" }, { 18, "F" },
        { 19, "k" }, { 20, "2,F" }, { 21, "Z,F" }, { 22, "^^,F" }, { 23, "ff,k" }, { 24, "F" },
        { 25, "F" }, { 26, "k" }, { 27, "E,F" }, { 28, "^,k" },
        { 29, "FF" }, { 30, "W,FF" }, { 31, "F" }, { 32, "W,FF" }, { 33, "^,F" }, { 34, "W,FF" },
        { 35, "F,k" }, { 36, "W,FF" }, { 37, "ff,F" }, { 38, "W,FF" }, { 39, "E,F" }, { 40, "W,k" },
        { 41, "F" }, { 42, "W,FF" }, { 43, "FF" }, { 44, "k" },
        { 45, "T,F" }, { 48, "FF" }, { 49, "F" }, { 50, "F" }, { 52, "FF" }, { 53, "k" }, { 54, "F" }
    };

    public static string Gerar(bool sobrescrever)
    {
        if (File.Exists(Caminho) && !sobrescrever)
        {
            return "Assets/Levels/Phase3.txt ja existe; nada foi gerado (chame Gerar(true) para sobrescrever).";
        }

        for (int semente = 1; semente <= 20000; semente++)
        {
            Plat[] niv = new Plat[Niveis + 3];

            if (Tentar(semente, niv, out string falha, out char[,] grade))
            {
                string texto = Montar(grade);
                File.WriteAllText(Caminho, texto);
                AssetDatabase.ImportAsset(Caminho);
                return "Mapa gerado com a semente " + semente + " em " + Caminho + " (" + W + " x " + H + " un).";
            }
        }

        return "Nenhuma semente produziu uma torre valida em 20000 tentativas.";
    }

    private static bool Tentar(int semente, Plat[] niv, out string falha, out char[,] g)
    {
        falha = "";
        g = null;
        var rng = new System.Random(semente);

        // ---- andares 1 a 44: zigue-zague (impares a direita, pares a esquerda) ----
        // O andar 1 encosta na parede direita; o jogador nasce no chao, a esquerda (coluna 4).
        niv[1] = Ilha(10, 15);
        int dir = 1;

        for (int k = 2; k <= 44; k++)
        {
            Plat anterior = niv[k - 1];
            Plat novo;
            dir = -dir;

            if (k == 44)
            {
                novo = Ilha(2, 7);
            }
            else
            {
                char tipo = NiveisQueda.Contains(k) ? 'Q' : 'I';
                int wmin;
                int wmax;
                int gmin;
                int gmax;

                if (k <= 12) { wmin = 6; wmax = 7; gmin = 1; gmax = 2; }
                else if (k <= 28) { wmin = 4; wmax = 6; gmin = 2; gmax = 3; }
                else { wmin = 6; wmax = 8; gmin = 2; gmax = 3; }

                int minimo = Mathf.Max(4, LarguraExigida(k));

                if (NiveisPedra.Contains(k))
                {
                    minimo = Mathf.Max(minimo, 6);
                }

                wmax = Mathf.Max(wmax, minimo);
                int largura = Mathf.Clamp(rng.Next(wmin, wmax + 1), minimo, wmax);
                int vao = rng.Next(gmin, gmax + 1);

                novo = Proxima(anterior, dir, tipo, largura, vao, minimo, wmax, gmin, gmax);

                if (novo == null || !Dentro(novo))
                {
                    falha = "andar " + k + " nao cabe";
                    return false;
                }
            }

            if (!Liga(anterior, novo, 2f, 0.85f) || !Dentro(novo))
            {
                falha = "andar " + (k - 1) + " -> " + k + " nao liga";
                return false;
            }

            niv[k] = novo;
        }

        // ---- trecho final (45 a 56): layout absoluto ----
        niv[45] = Ilha(10, 15);                 // trampolim aqui
        niv[46] = new Plat();
        niv[47] = new Plat();
        niv[48] = Ilha(4, 8);                   // pouso do trampolim (+6 un)
        niv[49] = Queda(10);                    // degrau que cai entre 48 e 50
        niv[50] = Ilha(12, 15);                 // embarque do elevador
        niv[51] = new Plat();
        niv[52] = Ilha(2, 6);                   // desembarque do elevador (+4 un)
        niv[53] = Queda(9);
        niv[54] = Ilha(12, 15);
        niv[55] = Ilha(2, 9);                   // cume: bandeira e casa
        niv[56] = Ilha(12, 14);                 // saliencia da melancia

        if (!Liga(niv[44], niv[45], 2f, 0.85f))
        {
            falha = "44 -> 45 nao liga";
            return false;
        }

        // ---- grade ----
        g = new char[W, H];

        for (int c = 0; c < W; c++)
        {
            for (int r = 0; r < H; r++)
            {
                g[c, r] = '.';
            }
        }

        for (int c = 0; c < W; c++)
        {
            if (c < ColMin || c > ColMax)
            {
                g[c, 0] = 'R';
            }
            else
            {
                g[c, Chao] = 'R';
            }
        }

        g[4, Chao - 1] = 'P';

        for (int k = 1; k <= Niveis + 1; k++)
        {
            Plat p = niv[k];

            if (p == null || p.tipo == '-')
            {
                continue;
            }

            if (p.tipo == 'I')
            {
                // o andar 1 e um degrau de chao (R preenche ate embaixo): uma ilha a 2 un do chao
                // deixaria um vao em que o sapo bate a cabeca ao pular
                char solido = k == 1 ? 'R' : 'I';

                for (int c = p.c0; c <= p.c1; c++)
                {
                    g[c, Linha(k)] = solido;
                }
            }
            else
            {
                g[p.cq, Linha(k) - 1] = 'Q';
            }
        }

        // elevador entre os andares 50 e 52, trampolim e cume
        g[9, Linha(50) - 3] = 'V';
        g[11, Linha(45) - 1] = 'T';
        g[3, Linha(55) - 1] = 'K';
        g[7, Linha(55) - 1] = 'H';
        g[13, Linha(56) - 1] = 'X';

        // ---- itens e armadilhas ----
        foreach (KeyValuePair<int, string> par in Itens)
        {
            int k = par.Key;
            Plat p = niv[k];

            if (p == null || p.tipo == '-')
            {
                continue;
            }

            foreach (string item in par.Value.Split(','))
            {
                if (!Colocar(g, p, k, item, out string erro))
                {
                    falha = "andar " + k + ": " + erro;
                    return false;
                }
            }
        }

        // ---- pedras que esmagam nao podem encostar em plataformas dos lados ----
        // (embaixo fica o andar que ela golpeia; em cima pode haver a plataforma de que ela pende)
        for (int c = 0; c < W; c++)
        {
            for (int r = 0; r < H; r++)
            {
                if (g[c, r] != 'W')
                {
                    continue;
                }

                for (int cc = c - 1; cc <= c + 1; cc += 2)
                {
                    if (cc >= 0 && cc < W && "IQVMT".IndexOf(g[cc, r]) >= 0)
                    {
                        falha = "pedra da coluna " + c + " encosta em plataforma (" + cc + "," + r + ")";
                        return false;
                    }
                }
            }
        }

        // ---- trampolim: poco livre acima ----
        for (int r = Linha(45) - 2; r >= Linha(45) - 6; r--)
        {
            if ("IQVM".IndexOf(g[11, r]) >= 0 || "IQVM".IndexOf(g[10, r]) >= 0 || "IQVM".IndexOf(g[12, r]) >= 0)
            {
                falha = "poco do trampolim bloqueado na linha " + r;
                return false;
            }
        }

        return true;
    }

    private static bool Colocar(char[,] g, Plat p, int k, string item, out string erro)
    {
        erro = "";
        int linha = Linha(k) - 1;

        if (p.tipo == 'Q')
        {
            // item sobre uma plataforma que cai: no ar, acima dela
            if (item == "F" || item == "k")
            {
                g[p.cq, linha - 1] = item == "k" ? 'k' : 'F';
                return true;
            }

            erro = "item '" + item + "' nao cabe numa plataforma que cai";
            return true;
        }

        int largura = p.c1 - p.c0 + 1;
        int meio = (p.c0 + p.c1) / 2;

        switch (item)
        {
            case "F":
                ColocarSimples(g, p, linha, 'F');
                return true;
            case "FF":
                g[p.c0 + 1, linha] = 'F';
                g[p.c1 - 1, linha] = 'F';
                return true;
            case "k":
                ColocarSimples(g, p, linha, 'k');
                return true;
            case "E":
            case "Z":
                if (largura < 6) { erro = "plataforma estreita para inimigo"; return false; }
                g[meio, linha] = item == "E" ? 'E' : 'Z';
                return true;
            case "^":
                if (largura < 5) { erro = "plataforma estreita para espinho"; return false; }
                g[meio, linha] = '^';
                return true;
            case "^^":
                if (largura < 6) { erro = "plataforma estreita para espinhos"; return false; }
                g[meio, linha] = '^';
                g[meio + 1, linha] = '^';
                return true;
            case "ff":
                if (largura < 6) { erro = "plataforma estreita para chamas"; return false; }
                g[meio, linha] = 'f';
                g[meio + 1, linha] = 'f';
                return true;
            case "1":
            case "2":
            case "3":
                if (largura < 5) { erro = "plataforma estreita para caixa"; return false; }
                g[p.c1 - 1, linha] = item[0];
                return true;
            case "W":
            {
                if (largura < 6) { erro = "plataforma estreita para pedra"; return false; }
                // perto da borda interna (onde o sapo pousa e de onde pula), deixando cerca de
                // 1,8 un livres na beirada para pousar com seguranca
                int coluna = p.c0 == ColMin ? p.c1 - 2 : p.c0 + 2;
                g[coluna, Linha(k) - 3] = 'W';
                return true;
            }
            case "T":
                return true;   // o trampolim e colocado a parte (coluna fixa 11)
            default:
                erro = "item desconhecido '" + item + "'";
                return false;
        }
    }

    // Fruta ou kiwi numa celula livre, preferindo o lado de fora (perto da parede): no zigue-zague
    // o sapo pousa e sai pela borda interna, entao para pegar o item ele atravessa o perigo do meio.
    // Nunca sobrescreve outro item (antes, a fruta apagava o inimigo ou o espinho do mesmo andar).
    private static void ColocarSimples(char[,] g, Plat p, int linha, char item)
    {
        bool esquerda = p.c0 == ColMin;
        int largura = p.c1 - p.c0 + 1;

        for (int i = 1; i <= largura; i++)
        {
            int deslocamento = i % largura;   // 1, 2, ..., largura-1 e por ultimo a propria beirada (0)
            int c = esquerda ? p.c0 + deslocamento : p.c1 - deslocamento;

            if (g[c, linha] == '.')
            {
                g[c, linha] = item;
                return;
            }
        }
    }

    private static string Montar(char[,] g)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// FASE 3 (torre do cemiterio) - subida vertical. 1 caractere = 1 unidade; a fase vai de baixo para cima.");
        sb.AppendLine("// Paredes: 2 colunas de cada lado (R no topo preenche a coluna inteira). Interior: colunas 2 a 15.");
        sb.AppendLine("// Andares de 2 un a partir do chao (linha " + Chao + "); I = plataforma solida; Q = plataforma que cai (uma linha acima do andar).");
        sb.AppendLine("// W = pedra que esmaga (3 linhas acima da plataforma)   V = elevador   T = trampolim   K = bandeira   H = casa   X = melancia");
        sb.AppendLine("// Trechos: 1-12 lapides | 13-28 mausoleu | 29-44 pedras que esmagam | 45-55 trampolim, elevador e cume.");
        sb.AppendLine("// Mapa gerado por Tools > Fase 3 > Gerar mapa da torre; a partir daqui, edite a mao.");
        sb.AppendLine("@chao=" + Chao);
        sb.AppendLine("@modo=vertical");
        sb.AppendLine("@onda=0.55,0.006,1.6");
        sb.AppendLine("@ondaInicio=18");
        sb.AppendLine("@ondaAtraso=20");
        sb.AppendLine("@tempoAlvo=100");
        sb.AppendLine("@nomeFase=FASE 3");
        sb.AppendLine("@proximaFase=");
        sb.AppendLine("@velocidadeV=1.6");

        for (int r = 0; r < H; r++)
        {
            var linha = new StringBuilder();

            for (int c = 0; c < W; c++)
            {
                linha.Append(g[c, r]);
            }

            sb.AppendLine(linha.ToString());
        }

        return sb.ToString();
    }
}
