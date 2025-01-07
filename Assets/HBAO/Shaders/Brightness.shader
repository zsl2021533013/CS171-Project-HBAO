Shader "URP/Brightness Saturation And Contrast"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST;
        half _Brightness;
        half _Saturation;
        half _Contrast;
        CBUFFER_END

        ENDHLSL

        Pass
        {
            // 开启深度测试 关闭剔除 关闭深度写入
            ZTest Always Cull Off ZWrite Off

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment frag
            
            SAMPLER(sampler_BlitTexture);

            half4 frag(Varyings i): SV_Target
            {
                // 纹理采样
                half4 renderTex = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, i.texcoord);
                
                // 调整亮度 = 原颜色 * 亮度值
                half3 finalColor = renderTex.rgb * _Brightness;

                // 调整饱和度
                // 亮度值（饱和度为0的颜色） = 每个颜色分量 * 特定系数
                half luminance = 0.2125 * renderTex.r + 0.7154 * renderTex.g + 0.0721 * renderTex.b;
                half3 luminanceColor = half3(luminance, luminance, luminance);
                // 插值亮度值和原图
                finalColor = lerp(luminanceColor, finalColor, _Saturation);

                // 调整对比度
                // 对比度为0的颜色
                half3 avgColor = half3(0.5, 0.5, 0.5);
                finalColor = lerp(avgColor, finalColor, _Contrast);

                float depth = SampleSceneDepth(i.texcoord);
                float3 normal = SampleSceneNormals(i.texcoord);

                return half4(depth, normal);
            }
            ENDHLSL
        }
    }

    Fallback Off
}