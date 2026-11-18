// Lightning's weather: the snowflakes and raindrops falling through the room, fresh flakes lying on the snow, flakes on
// a palm, and the drops' ripples, all quads in one mesh that Lightning rebuilds each frame (already turned to the eye).
// The vertex colour is the quad's colour (rgb) and brightness (a); uv runs 0-1 over the quad; uv1.x says what it is and
// uv1.y is a number for it:
// 0 a snowflake: a soft round body and six faint arms, turned by uv1.y;
// 1 a raindrop: a streak with a bright head at uv.y = 0 and a tail thinning away to uv.y = 1;
// 2 a ripple: a thin ring at radius uv1.y (0-1 of the quad), with a faint halo;
// 3 a soft dot (settled snow, snow on a palm).
// _Flash brightens it all for a moment with the lightning. Additive, and writes alpha so it shows over passthrough.
Shader "MusicInYourHands/LightningWeather"
{
    Properties
    {
        _Flash ("Flash 0-1", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+6" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Flash;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 shape : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 shape : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                output.shape = input.shape;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 q = input.uv * 2 - 1;
                float r = length(q);
                float kind = input.shape.x;
                float3 colour = input.color.rgb;
                float a;
                if (kind < 0.5)
                {
                    // A snowflake.
                    float body = exp(-r * r * 7);
                    float angle = atan2(q.y, q.x) + input.shape.y * 6.2832;
                    float arms = pow(abs(cos(angle * 3)), 10) * saturate(1 - r * 1.5) * 0.25;
                    a = body * 0.9 + arms;
                    colour = lerp(colour, 1, saturate(body * 0.5));
                }
                else if (kind < 1.5)
                {
                    // A raindrop: the head below, the tail fading upwards.
                    float along = input.uv.y;
                    float width = lerp(0.55, 0.12, sqrt(saturate(along)));
                    float across = exp(-q.x * q.x / (width * width) * 2.5);
                    float head = exp(-(q.x * q.x * 2.5 + along * along * 220));
                    a = across * pow(1 - saturate(along), 1.4) * 0.8 + head;
                    colour = lerp(colour, 1, saturate(head * 0.6));
                }
                else if (kind < 2.5)
                {
                    // A ripple.
                    float t = 0.03 + 0.05 * (1 - input.shape.y);
                    float d = r - input.shape.y;
                    a = (exp(-d * d / (t * t)) + exp(-d * d / (t * t * 9)) * 0.2) * step(r, 1);
                    colour = lerp(colour, 1, exp(-d * d / (t * t)) * 0.3);
                }
                else
                {
                    // A soft dot.
                    float core = saturate(1 - r);
                    a = core * core;
                    colour = lerp(colour, 1, pow(core, 6) * 0.6);
                }
                colour *= 1 + _Flash * 0.9;
                return half4(colour, saturate(a * input.color.a));
            }
            ENDHLSL
        }
    }
}
