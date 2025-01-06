Shader "Custom/HBAO"
{
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 100

        Pass
        {
            Name "Ambient Occlusion"
            
            Cull Off
            ZWrite Off
            ZTest Always
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment ambient_occlusion_frag

            #include "HBAO.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Blur"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment blur_frag

            #include "HBAO.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Combine"

            Blend One SrcAlpha, Zero One
            BlendOp Add, Add

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment combine_frag

            #include "HBAO.hlsl"
            ENDHLSL
        }
    }
}