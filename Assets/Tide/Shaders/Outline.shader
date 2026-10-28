// 卡通描边（反面外扩）：同一个模型再画一遍，只画背面，顶点沿法线往外推固定的屏幕像素宽。
// 宽度 = _Width（以 390x844 的界面为准的像素）× _TideOutlinePx（Game.cs 按屏幕高度设，屏幕越大线越粗，看起来一样）。
// 用的网格是 Art.OutlineMesh 做过的：同一位置的顶点法线取平均，硬边那里线才不会断开。
Shader "Tide/Outline"
{
    Properties
    {
        _Color ("Color", Color) = (0.15, 0.1, 0.06, 1)
        _Width ("Width (px @844)", Float) = 1.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
            CBUFFER_END
            float _TideOutlinePx;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                float4 c = TransformObjectToHClip(i.positionOS.xyz);
                float3 nCS = TransformWorldToHClipDir(TransformObjectToWorldNormal(i.normalOS));
                float2 d = nCS.xy;
                float len = length(d);
                d = len > 1e-5 ? d / len : float2(0, 0);
                float px = _Width * max(_TideOutlinePx, 0.5);
                c.xy += d * px * 2.0 / _ScreenParams.xy * c.w;
                o.positionCS = c;
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
