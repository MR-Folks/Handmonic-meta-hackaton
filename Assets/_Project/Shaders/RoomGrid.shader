// The Breathing room's surfaces: a faint glowing grid on a wall or floor quad (its size in metres in _Size), lit
// brighter as waves pass. Up to eight waves (_Waves: world position, age) ripple out as bright rings. On the floor
// (_Floor = 1), rings also flow in towards _Center, the spot where the player stands.
Shader "MusicInYourHands/RoomGrid"
{
    Properties
    {
        _Color ("Grid", Color) = (0.62, 0.56, 1, 1)
        _WaveColor ("Waves", Color) = (1, 0.45, 0.75, 1)
        _Size ("Quad size (m) (set by BreathingRoom)", Vector) = (3, 2.5, 0, 0)
        _Spacing ("Grid spacing (m)", Float) = 0.25
        _Floor ("Floor 0-1", Float) = 0
        _Center ("Where the player stands", Vector) = (0, 0, 0, 0)
        _Breath ("Breath 0-1 (set by BreathingRoom)", Float) = 0
        _Clock ("Time (set by BreathingRoom)", Float) = 0
        _Alpha ("Fade", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float4 _Color;
                float4 _WaveColor;
                float4 _Size;
                float _Spacing;
                float _Floor;
                float4 _Center;
                float _Breath;
                float _Clock;
                float _Alpha;
                float4 _Waves[8];
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }

            float GridLine(float2 m, float width)
            {
                float2 g = abs(frac(m / _Spacing + 0.5) - 0.5) * _Spacing;
                return smoothstep(width, 0, min(g.x, g.y));
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 m = input.uv * _Size.xy;
                float grid = GridLine(m, 0.006 + _Breath * 0.004);

                // Waves: bright rings that spread from where they started, fading as they go.
                float wave = 0;
                [unroll] for (int i = 0; i < 8; i++)
                {
                    float age = _Waves[i].w;
                    if (age < 0 || age > 3) continue;
                    float d = distance(input.positionWS, _Waves[i].xyz);
                    float front = age * 1.4;
                    wave += exp(-pow((d - front) / 0.07, 2)) * exp(-age * 1.1);
                }

                // On the floor, rings flow in towards where you stand.
                float flow = 0;
                if (_Floor > 0.5)
                {
                    float d = distance(input.positionWS.xz, _Center.xz);
                    flow = pow(saturate(sin(d * 9 + _Clock * 2.5) * 0.5 + 0.5), 24) * saturate(1 - d / 2.4) * 0.8;
                }

                // Fade out towards the quad's edges and corners, so the room has no hard frame.
                float2 e = min(input.uv, 1 - input.uv);
                float edge = smoothstep(0, 0.08, min(e.x, e.y));

                float3 color = _Color.rgb * (grid * (0.25 + _Breath * 0.45) + flow) + _WaveColor.rgb * wave * (0.4 + grid * 1.6);
                float alpha = (grid * (0.1 + _Breath * 0.22) + flow * 0.5 + wave * (0.25 + grid)) * edge * _Alpha;
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
