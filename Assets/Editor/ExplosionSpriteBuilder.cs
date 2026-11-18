using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gera por codigo os quadros da explosao em pixel art (Assets/Sprites/Explosion).
///
/// Sao 7 quadros de 160x160 px (5 un a 32 px/un): o fogo cresce com a borda
/// irregular, depois esfria e vira um anel com fumaca. Cada "pixel" do desenho
/// tem 2x2 px reais, para o efeito ficar com o mesmo aspecto do restante do jogo.
/// Rode pelo menu Tools/Efeitos ou por ExplosionSpriteBuilder.Gerar().
/// </summary>
public static class ExplosionSpriteBuilder
{
    public const string Pasta = "Assets/Sprites/Explosion";
    public const int Quadros = 7;

    private const int Grade = 80;     // grade logica
    private const int Escala = 2;     // pixels reais por pixel logico

    // Por quadro: raio (fracao do maximo), fracao de fogo (1 = disco cheio) e opacidade da fumaca.
    private static readonly float[] Raio = { 0.30f, 0.55f, 0.80f, 1.00f, 0.96f, 0.88f, 0.74f };
    private static readonly float[] Fogo = { 1.00f, 1.00f, 1.00f, 0.88f, 0.62f, 0.36f, 0.14f };
    private static readonly float[] Fumaca = { 0.00f, 0.00f, 0.15f, 0.40f, 0.65f, 0.60f, 0.40f };

    private static readonly Color Branco = new Color(1f, 0.98f, 0.82f, 1f);
    private static readonly Color Amarelo = new Color(1f, 0.85f, 0.25f, 1f);
    private static readonly Color Laranja = new Color(1f, 0.52f, 0.12f, 1f);
    private static readonly Color Vermelho = new Color(0.86f, 0.2f, 0.1f, 1f);
    private static readonly Color Contorno = new Color(0.27f, 0.08f, 0.06f, 1f);
    private static readonly Color CinzaFumaca = new Color(0.36f, 0.34f, 0.37f, 1f);

    [MenuItem("Tools/Efeitos/Gerar explosao em pixel art")]
    private static void Menu()
    {
        Debug.Log(Gerar());
    }

    public static string Gerar()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Sprites"))
        {
            AssetDatabase.CreateFolder("Assets", "Sprites");
        }

        if (!AssetDatabase.IsValidFolder(Pasta))
        {
            AssetDatabase.CreateFolder("Assets/Sprites", "Explosion");
        }

        AssetDatabase.StartAssetEditing();

        try
        {
            for (int f = 0; f < Quadros; f++)
            {
                Texture2D tex = DesenharQuadro(f);
                File.WriteAllBytes(Caminho(f), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        for (int f = 0; f < Quadros; f++)
        {
            AssetDatabase.ImportAsset(Caminho(f));
            var imp = (TextureImporter)AssetImporter.GetAtPath(Caminho(f));
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 32f;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }

        return Quadros + " quadros gerados em " + Pasta + " (" + (Grade * Escala) + "x" + (Grade * Escala) + " px cada)";
    }

    public static string Caminho(int quadro)
    {
        return Pasta + "/Explosion_" + quadro + ".png";
    }

    private static Texture2D DesenharQuadro(int f)
    {
        int lado = Grade * Escala;
        var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
        var pixels = new Color[lado * lado];

        float meio = Grade * 0.5f;
        float raioMax = meio * 0.96f;
        float interno = 1f - Fogo[f];

        for (int gy = 0; gy < Grade; gy++)
        {
            for (int gx = 0; gx < Grade; gx++)
            {
                float dx = gx + 0.5f - meio;
                float dy = gy + 0.5f - meio;
                float angulo = Mathf.Atan2(dy, dx);
                float dist = Mathf.Sqrt((dx * dx) + (dy * dy)) / (raioMax * Raio[f]);

                // Borda irregular: ondulacoes suaves mais um ruido por pixel.
                float pontas = 1f
                    + (0.14f * Mathf.Sin((7f * angulo) + (f * 0.9f)))
                    + (0.09f * Mathf.Sin((13f * angulo) - (f * 1.3f)))
                    + (0.06f * (Ruido(gx, gy, f) - 0.5f));

                float d = dist / pontas;
                Color cor = Color.clear;

                if (d <= 1f)
                {
                    cor = CorDoFogo(d, interno, f, gx, gy);
                }
                else if (d <= 1.22f && Fumaca[f] > 0f && ((gx + gy) % 2 == 0) && Ruido(gx, gy, f + 3) > 0.35f)
                {
                    cor = new Color(CinzaFumaca.r, CinzaFumaca.g, CinzaFumaca.b, Fumaca[f] * 0.55f);
                }

                for (int py = 0; py < Escala; py++)
                {
                    for (int px = 0; px < Escala; px++)
                    {
                        pixels[(((gy * Escala) + py) * lado) + (gx * Escala) + px] = cor;
                    }
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static Color CorDoFogo(float d, float interno, int f, int gx, int gy)
    {
        // Miolo ja esfriado: fumaca tracejada.
        if (d < interno)
        {
            float alfa = Fumaca[f] * (1f - ((d / Mathf.Max(interno, 0.01f)) * 0.4f));
            bool tracejado = ((gx + gy) % 2) == 0;
            return new Color(CinzaFumaca.r, CinzaFumaca.g, CinzaFumaca.b, tracejado ? alfa * 0.6f : alfa);
        }

        float r = (d - interno) / Mathf.Max(1f - interno, 0.01f);

        if (r < 0.28f) return Branco;
        if (r < 0.52f) return Amarelo;
        if (r < 0.76f) return Laranja;
        if (r < 0.92f) return Vermelho;
        return Contorno;
    }

    private static float Ruido(int x, int y, int semente)
    {
        unchecked
        {
            int h = (x * 73856093) ^ (y * 19349663) ^ (semente * 83492791);
            h ^= h >> 13;
            h *= 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }
    }
}
