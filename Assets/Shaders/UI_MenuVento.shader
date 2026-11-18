// Vento no fundo do menu: balanca a bandana, a copa da arvore, o capim e as folhas, e faz a agua tremer.
// Nao altera o PNG: cada pixel e lido de um ponto levemente deslocado, so dentro de regioes marcadas,
// com ondas senoidais (por isso o movimento repete sem emenda).
//
// As regioes foram medidas na arte Assets/UI/Menu_Fundo.png (1536x1024). Coordenadas: (u, v) com
// u = 0 na esquerda e v = 0 no TOPO da imagem. Se a arte for trocada, as regioes precisam ser refeitas.
Shader "UI/MenuVento"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        _Vento ("Intensidade geral (0 = parado)", Range(0, 2)) = 1
        _Velocidade ("Velocidade", Range(0, 3)) = 1
        _ForcaBandana ("Bandana", Range(0, 3)) = 1
        _ForcaCapim ("Capim e folhas", Range(0, 3)) = 1
        _ForcaCopa ("Copa da arvore", Range(0, 3)) = 1
        _ForcaAgua ("Agua", Range(0, 3)) = 1
        _TempoFixo ("Tempo fixo para testes (negativo = tempo do jogo)", Float) = -1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Vento;
            float _Velocidade;
            float _ForcaBandana;
            float _ForcaCapim;
            float _ForcaCopa;
            float _ForcaAgua;
            float _TempoFixo;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            // Mascara em elipse com borda suave: 1 no centro, 0 fora.
            float Elipse(float2 p, float2 centro, float2 raio, float suave)
            {
                float d = length((p - centro) / raio);
                return 1.0 - smoothstep(1.0 - suave, 1.0, d);
            }

            // Bandana: presa na cabeca (direita), ponta solta para a esquerda, ondulando como uma bandeira.
            // Devolve o deslocamento (x em u, y em v-do-topo).
            float2 Bandana(float2 p, float t)
            {
                const float raiz = 0.368;
                const float ponta = 0.226;
                float aoLongo = saturate((raiz - p.x) / (raiz - ponta));      // 0 na raiz, 1 na ponta
                float forca = smoothstep(0.0, 0.9, aoLongo);
                float mascara = Elipse(p, float2(0.298, 0.288), float2(0.098, 0.050), 0.55);
                float fase = t * 3.0 - aoLongo * 4.2;
                float onda = sin(fase) + 0.25 * sin(t * 5.2 - aoLongo * 6.0 + 1.3);
                float dy = onda * 0.0100 * forca * mascara;
                float dx = cos(fase) * 0.0016 * forca * mascara;
                return float2(dx, dy);
            }

            // Vermelho escuro e saturado da bandana (nuvens pessego e ceu nao entram).
            float Escarlate(float3 c)
            {
                return saturate((c.r - c.g * 1.7 - c.b * 0.6) * 5.0);
            }

            // Procura o vermelho da bandana no ponto e a 8 px ao redor (para pegar os realces alaranjados dela).
            float EscarlateProximo(float2 uvc)
            {
                float2 d = float2(8.0 / 1536.0, 8.0 / 1024.0);
                float m = Escarlate(tex2D(_MainTex, uvc).rgb);
                m = max(m, Escarlate(tex2D(_MainTex, uvc + float2(d.x, 0.0)).rgb));
                m = max(m, Escarlate(tex2D(_MainTex, uvc - float2(d.x, 0.0)).rgb));
                m = max(m, Escarlate(tex2D(_MainTex, uvc + float2(0.0, d.y)).rgb));
                m = max(m, Escarlate(tex2D(_MainTex, uvc - float2(0.0, d.y)).rgb));
                return m;
            }

            // Capim, folhas grandes da esquerda e arbustos: balanco horizontal que cresce para o alto das folhas.
            // 'verde' (0..1) vem da cor original do pixel: o que nao e verde (pedra, sapo, agua) quase nao mexe.
            float2 Capim(float2 p, float t, float verde)
            {
                float mA = Elipse(p, float2(0.085, 0.660), float2(0.125, 0.210), 0.55);
                float hA = saturate((0.87 - p.y) / 0.42);
                float mB = Elipse(p, float2(0.560, 0.845), float2(0.530, 0.200), 0.55);
                float hB = saturate((1.02 - p.y) / 0.38);
                float peso = max(mA * hA, mB * hB);
                float porte = lerp(0.08, 1.0, verde);

                float s1 = sin(t * 1.7 + p.x * 26.0 + p.y * 7.0);
                float s2 = sin(t * 2.9 + p.x * 51.0 - p.y * 11.0 + 1.7);
                float dx = (0.62 * s1 + 0.38 * s2) * 0.0030 * peso * porte;
                float dy = 0.30 * s2 * 0.0012 * peso * porte;
                return float2(dx, dy);
            }

            // Copa da arvore, ramo da esquerda e folha solta: balanco mais lento e mais curto.
            float2 Copa(float2 p, float t, float verde)
            {
                float m = Elipse(p, float2(0.340, 0.085), float2(0.370, 0.150), 0.55);
                m = max(m, Elipse(p, float2(0.155, 0.255), float2(0.075, 0.065), 0.55));
                m = max(m, Elipse(p, float2(0.100, 0.370), float2(0.045, 0.070), 0.55));
                float porte = lerp(0.25, 1.0, verde);

                float s = 0.6 * sin(t * 1.1 + p.x * 18.0 + p.y * 9.0) + 0.4 * sin(t * 2.3 + p.x * 43.0 - p.y * 14.0);
                float dx = s * 0.0022 * m * porte;
                float dy = 0.5 * cos(t * 1.7 + p.x * 30.0) * 0.0012 * m * porte;
                return float2(dx, dy);
            }

            // Agua: linhas horizontais que escorregam de lado, so onde ha agua (claro e nao verde).
            float2 Agua(float2 p, float t, float verde, float lum)
            {
                float m = Elipse(p, float2(0.775, 0.835), float2(0.165, 0.110), 0.6);
                float ehAgua = (1.0 - verde) * smoothstep(0.10, 0.20, lum);
                float dx = sin(t * 2.2 + p.y * 520.0) * 0.0028 + sin(t * 3.7 - p.y * 310.0 + 2.0) * 0.0014;
                return float2(dx * m * ehAgua, 0.0);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float2 p = float2(uv.x, 1.0 - uv.y);
                float t = (_TempoFixo >= 0.0 ? _TempoFixo : _Time.y) * _Velocidade;

                float3 c0 = tex2D(_MainTex, uv).rgb;
                float lum = dot(c0, float3(0.299, 0.587, 0.114));
                float verde = saturate((c0.g - max(c0.r, c0.b)) * 8.0);

                // bandana: so mexe onde ha (ou vai haver) o vermelho dela; o ceu e as nuvens em volta ficam parados
                float2 dBand = Bandana(p, t) * _ForcaBandana * _Vento;
                float2 uvB = uv + float2(dBand.x, -dBand.y);
                dBand *= max(EscarlateProximo(uv), EscarlateProximo(uvB));

                float2 desloc = dBand;
                desloc += Capim(p, t, verde) * _ForcaCapim * _Vento;
                desloc += Copa(p, t, verde) * _ForcaCopa * _Vento;
                desloc += Agua(p, t, verde, lum) * _ForcaAgua * _Vento;

                float2 uv2 = uv + float2(desloc.x, -desloc.y);
                return tex2D(_MainTex, uv2) * IN.color;
            }
            ENDCG
        }
    }
}
