// The Sound pool's liquid surface: a see-through teal sheet whose heights come from SoundPool's wave simulation.
// Slopes catch a soft highlight and the edges fade into a thin glowing lip so the pool sits in the room without a
// hard box. (The specks of light in the water are real 3D particles, drawn by SoundPool.) It draws before the
// hands, so a hand in the water always shows through, tinted by its own underwater effect.
Shader "MusicInYourHands/Water"
{
    Properties
    {
        _Deep ("Deep colour", Color) = (0.05, 0.22, 0.32, 0.42)
        _Shallow ("Crest colour", Color) = (0.37, 0.83, 1, 1)
        _Lip ("Edge glow", Color) = (0.6, 0.92, 1, 1)
        _Dots ("Dot lattice (columns, rows)", Vector) = (56, 40, 0, 0)
        _Kick ("Beat 0-1 (set by SoundPool)", Float) = 0
        _Alpha ("Fade (set by SoundPool)", Float) = 1
        _Spec ("Highlight strength", Float) = 1.6
        _SpecPower ("Highlight sharpness (higher is smaller)", Float) = 60
        _LipBeat ("How much the beat brightens the edge", Float) = 0.5
        _CrestGain ("How quickly the water turns crest-coloured as it rises or falls", Float) = 140
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Deep;
                float4 _Shallow;
                float4 _Lip;
                float4 _Dots;
                float _Kick;
                float _Alpha;
                float _Spec;
                float _SpecPower;
                float _LipBeat;
                float _CrestGain;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float height : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.height = input.positionOS.y;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 n = normalize(input.normalWS);
                if (n.y < 0) n = -n;
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(1 - saturate(dot(n, v)), 3);
                float slope = saturate(1 - n.y) * 18;

                // Highlight from a soft light above and behind the viewer.
                float3 light = normalize(float3(0.25, 1, -0.35));
                float spec = pow(saturate(dot(reflect(-light, n), v)), _SpecPower) * _Spec;

                float crest = saturate(abs(input.height) * _CrestGain + slope * 0.4);
                float3 color = lerp(_Deep.rgb, _Shallow.rgb, saturate(crest * 0.7 + fresnel * 0.4));

                // Soft edges with a thin glowing lip.
                float2 e = min(input.uv, 1 - input.uv);
                float edge = min(e.x * 1.4, e.y);
                float inside = smoothstep(0.0, 0.05, edge);
                float lip = exp(-pow((edge - 0.012) / 0.006, 2)) * (0.7 + _Kick * _LipBeat);

                color += spec + _Lip.rgb * lip;
                float alpha = _Deep.a * 0.5 + fresnel * 0.3 + crest * 0.25 + spec;
                alpha = saturate(alpha) * inside + lip;
                return half4(color, saturate(alpha) * _Alpha);
            }
            ENDHLSL
        }
    }
}
