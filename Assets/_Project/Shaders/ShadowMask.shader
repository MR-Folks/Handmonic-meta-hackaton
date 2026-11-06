// Draws hand shadows into the Shadow play world's wall mask: each quad covers one capsule (a finger, the palm, the
// forearm, the head) already projected onto the wall from the lamp. Positions are metres on the wall. TEXCOORD0 is the
// point, TEXCOORD1 the capsule's two ends, TEXCOORD2 its radius, how soft its edge is (the penumbra), which channel it
// goes to (0 shadow, 1-3 coloured echoes) and its strength. Channels combine by taking the larger value, so overlapping
// fingers never get darker than one.
Shader "MusicInYourHands/ShadowMask"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            BlendOp Max

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 spot : TEXCOORD0;
                float4 ends : TEXCOORD1;
                float4 shape : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 spot : TEXCOORD0;
                float4 ends : TEXCOORD1;
                float4 shape : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.spot = input.spot;
                output.ends = input.ends;
                output.shape = input.shape;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 a = input.ends.xy, b = input.ends.zw, p = input.spot;
                float2 ab = b - a;
                float h = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-8));
                float d = length(p - a - ab * h);
                float r = input.shape.x, soft = max(input.shape.y, 0.002);
                float cover = smoothstep(r + soft, r - soft, d) * input.shape.w;
                float channel = input.shape.z;
                return float4(channel < 0.5 ? cover : 0, abs(channel - 1) < 0.5 ? cover : 0, abs(channel - 2) < 0.5 ? cover : 0, channel > 2.5 ? cover : 0);
            }
            ENDHLSL
        }
    }
}
