// The onboarding's ghost hand: a neutral, see-through hand that shows a gesture. Almost no fill, a soft pale rim and a
// fine outline, with a slow band of light running from the wrist to the fingertips, so it reads as a demonstration
// and never as your own hand. The fingers that matter (index and thumb for a pinch) glow warm around _FocusA and
// _FocusB. The forearm fades out from the wrist, as the player's hands do.
// Pass 1 fills the hand and writes depth; pass 2 draws an inverted hull that only survives around the silhouette.
Shader "MusicInYourHands/OnboardingGhost"
{
    Properties
    {
        _Color ("Ghost colour", Color) = (0.8, 0.9, 1, 1)
        _Highlight ("Active finger colour", Color) = (1, 0.84, 0.45, 1)
        _Alpha ("Fade", Float) = 1
        _Fill ("Fill opacity", Float) = 0.045
        _RimPower ("Rim sharpness", Float) = 3
        _RimStrength ("Rim opacity", Float) = 0.5
        _OutlineWidth ("Outline width (m)", Float) = 0.002
        _FocusA ("Active point A (xyz; w strength, set by the onboarding)", Vector) = (0, 0, 0, 0)
        _FocusB ("Active point B (xyz; w strength, set by the onboarding)", Vector) = (0, 0, 0, 0)
        _FocusRadius ("Active point radius (m)", Float) = 0.026
        _WristPos ("Wrist position (set by the onboarding)", Vector) = (0, 0, 0, 0)
        _HandForward ("Wrist to fingers (set by the onboarding)", Vector) = (0, 0, 0, 0)
        _WristFade ("Wrist fade length (m)", Float) = 0.05
        _SweepSpeed ("Light band speed (sweeps per second)", Float) = 0.45
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float4 _Highlight;
            float _Alpha;
            float _Fill;
            float _RimPower;
            float _RimStrength;
            float _OutlineWidth;
            float4 _FocusA;
            float4 _FocusB;
            float _FocusRadius;
            float4 _WristPos;
            float4 _HandForward;
            float _WristFade;
            float _SweepSpeed;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        // 0 behind the wrist, 1 from a few centimetres towards the fingers. Without wrist data, no fade.
        float WristFade(float3 positionWS)
        {
            if (dot(_HandForward.xyz, _HandForward.xyz) < 1e-6) return 1;
            float along = dot(positionWS - _WristPos.xyz, _HandForward.xyz);
            return saturate((along + 0.005) / max(_WristFade, 1e-4));
        }

        // How much a point belongs to the active fingers: 1 at an active point, fading to 0 at _FocusRadius.
        float Focus(float3 positionWS)
        {
            float a = _FocusA.w * smoothstep(_FocusRadius, _FocusRadius * 0.25, distance(positionWS, _FocusA.xyz));
            float b = _FocusB.w * smoothstep(_FocusRadius, _FocusRadius * 0.25, distance(positionWS, _FocusB.xyz));
            return saturate(max(a, b));
        }

        // A soft band of light that runs from the wrist to the fingertips and starts again.
        float Sweep(float3 positionWS)
        {
            if (dot(_HandForward.xyz, _HandForward.xyz) < 1e-6) return 0;
            float along = dot(positionWS - _WristPos.xyz, _HandForward.xyz);
            float front = frac(_Time.y * _SweepSpeed) * 0.26 - 0.03;
            return exp(-pow((along - front) / 0.02, 2));
        }

        Varyings Build(Attributes input, float extrude)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz) + normalWS * extrude;
            output.positionWS = positionWS;
            output.normalWS = normalWS;
            output.positionCS = TransformWorldToHClip(positionWS);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "GhostFill"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            ZTest LEqual
            // Separate alpha blending keeps the alpha channel right for compositing over passthrough.
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            Varyings vert(Attributes input) { return Build(input, 0); }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                float rim = pow(1 - saturate(abs(dot(n, v))), _RimPower);
                float sweep = Sweep(input.positionWS);
                float focus = Focus(input.positionWS);
                float3 color = lerp(_Color.rgb, _Highlight.rgb, focus) * (0.6 + rim * 0.8 + sweep * 0.5 + focus * 0.4);
                float alpha = _Fill + rim * _RimStrength + sweep * 0.1 + focus * 0.4;
                alpha = saturate(alpha) * _Alpha * WristFade(input.positionWS);
                return half4(color, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "GhostOutline"
            Tags { "LightMode" = "UniversalForward" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            Varyings vert(Attributes input) { return Build(input, _OutlineWidth); }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float focus = Focus(input.positionWS);
                float sweep = Sweep(input.positionWS);
                float3 color = lerp(_Color.rgb, _Highlight.rgb, focus) * (1 + sweep * 0.3);
                float alpha = (0.9 + focus * 0.1) * _Alpha * WristFade(input.positionWS);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
