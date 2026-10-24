// The player's hand, in the concept page's style: a dark see-through fill, a glowing outline and a soft rim,
// all pulsing towards pink on the beat. The forearm fades out from the wrist.
// Pass 1 fills the hand and writes depth; pass 2 draws an inverted hull that only survives around the silhouette.
Shader "MusicInYourHands/Hand"
{
    Properties
    {
        _FillColor ("Fill", Color) = (0.19, 0.11, 0.36, 0.62)
        _EdgeColor ("Edge", Color) = (0.62, 0.56, 1, 1)
        _GlowColor ("Beat glow", Color) = (1, 0.37, 0.68, 1)
        _OutlineWidth ("Outline width (m)", Float) = 0.0025
        _RimPower ("Rim sharpness", Float) = 2.5
        _RimStrength ("Rim strength", Float) = 0.9
        _WristFade ("Wrist fade length (m)", Float) = 0.045
        _WristPos ("Wrist position (set by HandSkin)", Vector) = (0, 0, 0, 0)
        _HandForward ("Wrist to fingers (set by HandSkin)", Vector) = (0, 0, 0, 0)
        _Kick ("Beat 0-1 (set by HandSkin)", Float) = 0
        _Energy ("Music energy 0-1 (set by HandSkin)", Float) = 0
        _BeatAge ("Seconds since the beat (set by HandSkin)", Float) = 10
        _PulseSpeed ("Pulse speed (m/s)", Float) = 0.55
        _PulseWidth ("Pulse width (m)", Float) = 0.022
        _PulseLength ("Wrist to fingertips (m)", Float) = 0.2
        _Pattern ("Effect (set by the world: 0 veins, 1 heat, 2 scanlines, 3 electric, 4 ripples, 5 specks)", Float) = 0
        _PatternColor ("Effect colour (set by the world)", Color) = (1, 0.83, 0.42, 1)
        _FocusPos ("Focus point (set by the world)", Vector) = (0, 0, 0, 0)
        _FocusStrength ("Focus strength (set by the world)", Float) = 0
        _Charge ("Charge 0-1 (set by the world)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _FillColor;
            float4 _EdgeColor;
            float4 _GlowColor;
            float _OutlineWidth;
            float _RimPower;
            float _RimStrength;
            float _WristFade;
            float4 _WristPos;
            float4 _HandForward;
            float _Kick;
            float _Energy;
            float _BeatAge;
            float _PulseSpeed;
            float _PulseWidth;
            float _PulseLength;
            float _Pattern;
            float4 _PatternColor;
            float4 _FocusPos;
            float _FocusStrength;
            float _Charge;
        CBUFFER_END

        float Hash(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        // Each world dresses the hand differently. Returns light to add (rgb) and extra opacity (a).
        float4 HandEffect(float3 positionWS, float3 n, float3 v)
        {
            float t = _Time.y;
            float along = dot(positionWS - _WristPos.xyz, _HandForward.xyz);
            float3 palm = _WristPos.xyz + _HandForward.xyz * 0.07;
            float3 c = _PatternColor.rgb;
            if (_Pattern < 0.5) return 0;
            if (_Pattern < 1.5)
            {
                // Heat: the side of the hand facing the orb glows, with energy flowing towards it.
                float3 toFocus = _FocusPos.xyz - positionWS;
                float d = length(toFocus);
                float facing = saturate(dot(n, toFocus / max(d, 1e-4)));
                float near = saturate(1 - d / 0.35);
                float flow = pow(saturate(sin(d * 90 + t * 10) * 0.5 + 0.5), 4);
                float k = facing * near * _FocusStrength * (0.5 + flow * 0.8 + _Kick * 0.7);
                return float4(c * k, k * 0.6);
            }
            if (_Pattern < 2.5)
            {
                // Oscilloscope: bright scanlines sweep from the wrist to the fingertips.
                float scan = pow(saturate(sin(along * 140 - t * 12) * 0.5 + 0.5), 10);
                float sweep = pow(saturate(sin(along * 18 - t * 4) * 0.5 + 0.5), 3);
                float k = scan * (0.35 + sweep * 0.8 + _Kick * 0.6);
                return float4(c * k, k * 0.5);
            }
            if (_Pattern < 3.5)
            {
                // Electric: tiny sparks crackle over the skin, more on the beat.
                float3 cell = floor(positionWS * 260 + floor(t * 14) * 3.1);
                float spark = step(0.965 - _Kick * 0.04 - _Energy * 0.02, Hash(cell));
                float rim = pow(1 - saturate(abs(dot(n, v))), 3) * (0.5 + 0.5 * sin(t * 37 + along * 80));
                float k = spark * 1.3 + rim * 0.5;
                return float4(c * k, saturate(spark + rim * 0.3));
            }
            if (_Pattern < 4.5)
            {
                // Water: ripples spread across the palm, stronger as it fills with caught light.
                float r = length(positionWS - palm);
                float ripple = pow(saturate(sin(r * 120 - t * 8) * 0.5 + 0.5), 6) * saturate(1 - r / 0.13);
                float k = ripple * (0.35 + _Charge * 1.2 + _Kick * 0.3) + _Charge * 0.25 * saturate(1 - r / 0.1);
                return float4(c * k, k * 0.5);
            }
            // Specks: warm points of light twinkle in the skin, like fireflies resting on it.
            float3 cell2 = floor(positionWS * 95);
            float h = Hash(cell2);
            float twinkle = step(0.9, h) * pow(saturate(sin(t * (2 + h * 4) + h * 40) * 0.5 + 0.5), 3);
            float k = twinkle * (0.9 + _Kick * 0.8 + _Energy * 0.4);
            return float4(c * k, k * 0.6);
        }

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
            return saturate((along + 0.01) / max(_WristFade, 1e-4));
        }

        // A band of light that runs from the wrist to the fingertips after each beat, fading as it reaches them.
        float BeatPulse(float3 positionWS)
        {
            if (dot(_HandForward.xyz, _HandForward.xyz) < 1e-6) return 0;
            float along = dot(positionWS - _WristPos.xyz, _HandForward.xyz);
            float front = _BeatAge * _PulseSpeed;
            float band = exp(-pow((along - front) / max(_PulseWidth, 1e-4), 2));
            float travelled = saturate(front / max(_PulseLength, 1e-3));
            return band * (1 - travelled * travelled);
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
            Name "HandFill"
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
                float rim = pow(1 - saturate(abs(dot(n, v))), _RimPower) * (_RimStrength + _Kick * 0.6);
                float3 edge = lerp(_EdgeColor.rgb, _GlowColor.rgb, _Kick * 0.8);
                float pulse = BeatPulse(input.positionWS) * step(_Pattern, 0.5);
                float3 color = lerp(_FillColor.rgb, edge, saturate(rim)) + _GlowColor.rgb * (_Energy * 0.08);
                color += lerp(_GlowColor.rgb, 1, 0.25) * pulse * 0.9;
                float4 effect = HandEffect(input.positionWS, n, v);
                color += effect.rgb;
                float alpha = max(lerp(_FillColor.a, 1, saturate(rim)), pulse * 0.85);
                alpha = saturate(alpha + effect.a) * WristFade(input.positionWS);
                return half4(color, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "HandOutline"
            Tags { "LightMode" = "UniversalForward" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            Varyings vert(Attributes input) { return Build(input, _OutlineWidth * (1 + _Kick * 0.7)); }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float pulse = BeatPulse(input.positionWS) * step(_Pattern, 0.5);
                float3 color = lerp(_EdgeColor.rgb, _GlowColor.rgb, saturate(_Kick * 0.8 + pulse));
                color = lerp(color, 1, pulse * 0.4);
                return half4(color, _EdgeColor.a * WristFade(input.positionWS));
            }
            ENDHLSL
        }
    }
}
