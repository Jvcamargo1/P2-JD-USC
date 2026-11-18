using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cria os prefabs da pedra que esmaga (Rock Head, desce) e da cabeca de espinhos
/// (Spike Head, desliza de lado) a partir dos sprites do Pixel Adventure.
/// Menu: Tools/Fase 3/Criar prefabs das pedras.
/// </summary>
public static class PedrasBuilder
{
    private const string Pasta = "Assets/Pixel Adventure 1/Assets/Traps/";
    private const string PastaPrefabs = "Assets/Prefabs/";
    private const float Ppu = 32f;

    [MenuItem("Tools/Fase 3/Criar prefabs das pedras")]
    private static void Menu()
    {
        Debug.Log(Criar());
    }

    public static string Criar()
    {
        var rel = new StringBuilder();

        string[] folhas =
        {
            "Rock Head/Idle.png", "Rock Head/Blink (42x42).png", "Rock Head/Bottom Hit (42x42).png",
            "Spike Head/Idle.png", "Spike Head/Blink (54x52).png", "Spike Head/Right Hit (54x52).png"
        };

        foreach (string folha in folhas)
        {
            AjustarPpu(Pasta + folha, rel);
        }

        if (System.Type.GetType("PedraEsmagadora, Assembly-CSharp") == null)
        {
            return "ERRO: o script PedraEsmagadora ainda nao compilou.";
        }

        rel.AppendLine(Montar("Trap_RockHead",
            "Rock Head/Idle.png", "Rock Head/Blink (42x42).png", "Rock Head/Bottom Hit (42x42).png",
            Vector2.down, new Vector2(1.15f, 1.15f),
            16f, 90f, 3f, 1.6f, 0.5f, 0.45f));

        rel.AppendLine(Montar("Trap_SpikeHead",
            "Spike Head/Idle.png", "Spike Head/Blink (54x52).png", "Spike Head/Right Hit (54x52).png",
            Vector2.right, new Vector2(1.55f, 1.45f),
            9f, 40f, 4f, 1.8f, 0.5f, 0.5f));

        AssetDatabase.SaveAssets();
        return rel.ToString();
    }

    private static void AjustarPpu(string caminho, StringBuilder rel)
    {
        var importador = AssetImporter.GetAtPath(caminho) as TextureImporter;

        if (importador == null)
        {
            rel.AppendLine("sprite nao encontrado: " + caminho);
            return;
        }

        if (!Mathf.Approximately(importador.spritePixelsPerUnit, Ppu))
        {
            importador.spritePixelsPerUnit = Ppu;
            importador.SaveAndReimport();
            rel.AppendLine("PPU 32: " + caminho);
        }
    }

    private static Sprite[] Quadros(string caminho)
    {
        return AssetDatabase.LoadAllAssetsAtPath(Pasta + caminho)
            .OfType<Sprite>()
            .OrderBy(s => s.name, System.StringComparer.Ordinal)
            .ToArray();
    }

    private static string Montar(string nome, string idle, string piscar, string impacto,
        Vector2 direcao, Vector2 tamanhoColisor,
        float velocidadeGolpe, float aceleracao, float velocidadeVolta,
        float espera, float aviso, float tempoImpacto)
    {
        Sprite[] parado = Quadros(idle);
        Sprite[] quadrosPiscar = Quadros(piscar);
        Sprite[] quadrosImpacto = Quadros(impacto);

        if (parado.Length == 0 || quadrosPiscar.Length == 0 || quadrosImpacto.Length == 0)
        {
            return nome + ": sprites nao encontrados (" + parado.Length + "/" + quadrosPiscar.Length + "/" + quadrosImpacto.Length + ")";
        }

        var raiz = new GameObject(nome);

        var rb = raiz.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        var colisor = raiz.AddComponent<BoxCollider2D>();
        colisor.size = tamanhoColisor;
        colisor.isTrigger = true;

        raiz.AddComponent<Obstacle>();
        var pedra = raiz.AddComponent<PedraEsmagadora>();

        var visual = new GameObject("Visual");
        visual.transform.SetParent(raiz.transform, false);
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = parado[0];
        sr.sortingOrder = 3;

        var so = new SerializedObject(pedra);
        so.FindProperty("direcao").vector2Value = direcao;
        so.FindProperty("distancia").floatValue = 2f;
        so.FindProperty("velocidadeGolpe").floatValue = velocidadeGolpe;
        so.FindProperty("aceleracaoGolpe").floatValue = aceleracao;
        so.FindProperty("velocidadeVolta").floatValue = velocidadeVolta;
        so.FindProperty("tempoEspera").floatValue = espera;
        so.FindProperty("tempoAviso").floatValue = aviso;
        so.FindProperty("tempoImpacto").floatValue = tempoImpacto;
        so.FindProperty("render").objectReferenceValue = sr;

        SerializedProperty propPiscar = so.FindProperty("quadrosPiscar");
        propPiscar.arraySize = quadrosPiscar.Length;

        for (int i = 0; i < quadrosPiscar.Length; i++)
        {
            propPiscar.GetArrayElementAtIndex(i).objectReferenceValue = quadrosPiscar[i];
        }

        SerializedProperty bater = so.FindProperty("quadrosImpacto");
        bater.arraySize = quadrosImpacto.Length;

        for (int i = 0; i < quadrosImpacto.Length; i++)
        {
            bater.GetArrayElementAtIndex(i).objectReferenceValue = quadrosImpacto[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        string caminho = PastaPrefabs + nome + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
        Object.DestroyImmediate(raiz);

        return nome + ": prefab criado em " + caminho + " | sprite " + parado[0].bounds.size.x.ToString("0.00") + "x" + parado[0].bounds.size.y.ToString("0.00")
            + " un | piscar=" + quadrosPiscar.Length + " quadros, impacto=" + quadrosImpacto.Length + " quadros | ok=" + (prefab != null);
    }
}
