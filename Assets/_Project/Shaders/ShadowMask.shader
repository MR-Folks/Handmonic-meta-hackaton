// Draws body shadows into the Shadow play world's wall mask: each quad covers one tapered capsule (a forearm, an upper
// arm, the shoulders, the neck, the head) already projected onto the wall from the lamp. Positions are metres on the
// wall. TEXCOORD0 is the point, TEXCOORD1 the capsule's two ends, TEXCOORD2 its radius and how soft its edge is (the
// penumbra) at the first end, which channel it goes to (0 shadow, 1-3 coloured echoes) and its strength, and TEXCOORD3
// the radius and softness at the second end (a forearm is thinner at the wrist than at the elbow, and the end nearer
// the lamp casts a bigger, softer shadow). Channels combine by taking the larger value, so overlapping limbs never get
// darker than one.
// Pass 1 ("Fill") draws a mesh already flattened onto the wall (the hands' own meshes) solid, with _Fill the amount
// for each channel.
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
                float2 taper : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 spot : TEXCOORD0;
                float4 ends : TEXCOORD1;
                float4 shape : TEXCOORD2;
                float2 taper : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.spot = input.spot;
                output.ends = input.ends;
                output.shape = input.shape;
                output.taper = input.taper;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 a = input.ends.xy, b = input.ends.zw, p = input.spot;
                float2 ab = b - a;
                float h = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-8));
                float d = length(p - a - ab * h);
                // The radius and the softness change evenly from one end to the other.
                float r = lerp(input.shape.x, input.taper.x, h), soft = max(lerp(input.shape.y, input.taper.y, h), 0.002);
                float cover = smoothstep(r + soft, r - soft, d) * input.shape.w;
                float channel = input.shape.z;
                return float4(channel < 0.5 ? cover : 0, abs(channel - 1) < 0.5 ? cover : 0, abs(channel - 2) < 0.5 ? cover : 0, channel > 2.5 ? cover : 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Fill"
            Cull Off
            ZWrite Off
            ZTest Always
            BlendOp Max

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _Fill;

            float4 vert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            float4 frag() : SV_Target
            {
                return _Fill;
            }
            ENDHLSL
        }
    }
}
