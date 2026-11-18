// The player's hand, in the concept page's style: a dark see-through fill, a glowing outline and a soft rim,
// all pulsing towards pink on the beat. The forearm fades out from the wrist.
// Pass 1 fills the hand and writes depth; pass 2 draws an inverted hull that only survives around the silhouette.
// A gesture lights the fingers that made it (GestureFeedback through HandSkin): each vertex knows how much it belongs
// to each finger (UV2 thumb, index, middle, ring; UV3 little, then how far along the finger), so a lit finger glows
// brightest at its tip, its outline swells, and the finger beside it stays dark.
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
        _Pattern ("Effect (set by the world: 0 veins, 1 heat, 2 scanlines, 3 electric, 4 ripples, 5 specks, 6 submerged, 7 flow, 8 film, 9 uplight, 10 halftone, 11 paint)", Float) = 0
        _PatternColor ("Effect colour (set by the world)", Color) = (1, 0.83, 0.42, 1)
        _FocusPos ("Focus point (set by the world)", Vector) = (0, 0, 0, 0)
        _FocusStrength ("Focus strength (set by the world)", Float) = 0
        _Charge ("Charge 0-1 (set by the world)", Float) = 0
        _FingerGlow ("Lit fingers: thumb, index, middle, ring 0-1 (set by HandSkin)", Vector) = (0, 0, 0, 0)
        _FingerGlowLittle ("Lit little finger 0-1 (set by HandSkin)", Float) = 0
        _FingerColor ("Lit finger colour (set by HandSkin)", Color) = (1, 0.8, 0.9, 1)
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
            float4 _FingerGlow;
            float _FingerGlowLittle;
            float4 _FingerColor;
        CBUFFER_END

        float Hash(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float3 Hue(float h) { return saturate(abs(frac(h + float3(0, 2, 1) / 3) * 6 - 3) - 1); }

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
                // Hairline traces about 5 mm apart, lit only in a band that sweeps from the wrist to the fingertips
                // after each beat and fades on the way; between beats the hand is clear.
                float scan = pow(saturate(sin(along * 1300 - t * 20) * 0.5 + 0.5), 24);
                float front = _BeatAge * 0.5;
                float sweep = exp(-pow((along - front) / 0.022, 2)) * exp(-_BeatAge * 2.2);
                float k = scan * sweep * 1.6;
                return float4(c * k, k * 0.5);
            }
            if (_Pattern < 3.5)
            {
                // Electric: tiny round sparks crackle over the skin, more on the beat.
                float3 grid = positionWS * 420 + floor(t * 14) * 3.1;
                float3 cell = floor(grid);
                float d = length(frac(grid) - 0.5);
                float spark = step(0.95 - _Kick * 0.04 - _Energy * 0.02, Hash(cell)) * smoothstep(0.42, 0.08, d);
                float rim = pow(1 - saturate(abs(dot(n, v))), 3) * (0.5 + 0.5 * sin(t * 37 + along * 80));
                float k = spark * 1.4 + rim * 0.5;
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
            if (_Pattern < 5.5)
            {
                // Specks: warm round points of light twinkle in the skin, like fireflies resting on it.
                float3 grid2 = positionWS * 140;
                float3 cell2 = floor(grid2);
                float h = Hash(cell2);
                float dot2 = smoothstep(0.38, 0.05, length(frac(grid2) - 0.5));
                float twinkle = step(0.86, h) * dot2 * pow(saturate(sin(t * (2 + h * 4) + h * 40) * 0.5 + 0.5), 3);
                float k = twinkle * (1.1 + _Kick * 0.8 + _Energy * 0.4);
                return float4(c * k, k * 0.6);
            }
            if (_Pattern < 6.5)
            {
                // Submerged: below the water level (the focus point's height) the hand is tinted with drifting
                // caustics, and a bright line marks where it meets the surface.
                float depth = _FocusPos.y - positionWS.y;
                float below = smoothstep(-0.002, 0.01, depth) * _FocusStrength;
                float caustic = pow(saturate(sin(positionWS.x * 150 + t * 3.1) * sin(positionWS.z * 170 - t * 2.3) * 0.5 + 0.5), 4);
                float waterline = exp(-depth * depth / 0.00002) * _FocusStrength;
                float k = below * (0.22 + caustic * 0.8 + _Kick * 0.2) + waterline * 1.6;
                return float4(c * k, below * 0.3 + waterline * 0.7);
            }
            if (_Pattern < 7.5)
            {
                // Flow: dashes of light stream over the skin away from the wind's source (the focus point), brightest
                // on the side the wind hits.
                float3 flow = positionWS - _FocusPos.xyz;
                float d = length(flow);
                flow /= max(d, 1e-4);
                float3 across = normalize(cross(flow, float3(0, 1, 0)) + 1e-4);
                float lane = floor(dot(positionWS, across) * 160 + positionWS.y * 140);
                float dash = pow(saturate(sin(d * 90 - t * 16 + Hash(float3(lane, 1, 2)) * 40) * 0.5 + 0.5), 12);
                float facing = saturate(-dot(n, flow)) * 0.7 + 0.3;
                float k = dash * step(0.45, Hash(float3(lane, 3, 4))) * facing * (0.5 + _Energy * 0.8 + _Kick * 0.6) * _FocusStrength;
                return float4(c * k, k * 0.5);
            }
            if (_Pattern < 8.5)
            {
                // Film: a soap-film sheen, its rainbow shifting with the viewing angle and drifting slowly.
                float ndv = abs(dot(n, v));
                float3 film = Hue(frac(ndv * 1.7 + along * 4 + t * 0.06));
                float sheen = pow(1 - ndv, 1.6) * 0.9 + 0.08;
                float k = sheen * (0.55 + _Kick * 0.45 + _Energy * 0.2);
                return float4(lerp(c, film, 0.8) * k, sheen * 0.35);
            }
            if (_Pattern < 9.5)
            {
                // Uplight: the glowing plate below lights the underside of the hand, with grains of sand glinting.
                float height = positionWS.y - _FocusPos.y;
                float lit = saturate(1 - height / 0.3) * saturate(-n.y * 0.8 + 0.25) * _FocusStrength;
                float3 grains = positionWS * 260;
                float glint = step(0.82, Hash(floor(grains))) * smoothstep(0.4, 0.05, length(frac(grains) - 0.5));
                float k = lit * (0.45 + _Kick * 0.4) + glint * lit * (1.2 + _Kick);
                return float4(c * k, lit * 0.3 + glint * lit * 0.5);
            }
            if (_Pattern < 10.5)
            {
                // Halftone: printed dots, bigger towards the silhouette and on the beat, like a poster.
                float2 g = float2(dot(positionWS, float3(0.707, 0, 0.707)), positionWS.y + positionWS.x * 0.3) * 260;
                float tone = pow(1 - abs(dot(n, v)), 1.5) * 0.85 + _Kick * 0.3 + 0.06;
                float radius = sqrt(saturate(tone)) * 0.5;
                float dots = smoothstep(radius, radius - 0.12, length(frac(g) - 0.5));
                return float4(c * dots * 0.9, dots * 0.55);
            }
            // Paint: speckles of spray paint around the fingertip that sprays (the focus point), and a faint dusting.
            float near = saturate(1 - length(positionWS - _FocusPos.xyz) / 0.06) * _FocusStrength;
            float3 cells = positionWS * 320;
            float h2 = Hash(floor(cells));
            float speck = step(0.55, h2) * smoothstep(0.45, 0.15, length(frac(cells) - 0.5));
            float k = speck * (near * 1.3 + 0.06 * step(0.9, h2));
            return float4(c * k, k * 0.8);
        }

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            // How much the vertex belongs to the thumb, index, middle and ring finger; to the little finger, and how far
            // along its finger it is (0 knuckle, 1 tip). Zero on a mesh without them: then no finger ever lights.
            float4 fingers : TEXCOORD2;
            float2 little : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float fingerGlow : TEXCOORD2;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        // How brightly a vertex is lit by the lit fingers: its finger's light, fuller towards the tip.
        float FingerGlow(Attributes input)
        {
            float g = dot(input.fingers, _FingerGlow) + input.little.x * _FingerGlowLittle;
            return saturate(g) * lerp(0.3, 1, saturate(input.little.y));
        }

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

        Varyings Build(Attributes input, float extrude, float litExtrude)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float glow = FingerGlow(input);
            float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz) + normalWS * (extrude + glow * litExtrude);
            output.positionWS = positionWS;
            output.normalWS = normalWS;
            output.fingerGlow = glow;
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

            Varyings vert(Attributes input) { return Build(input, 0, 0); }

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
                // A lit finger: its own light, a glowing edge that runs white-hot towards the tip, and nearly opaque.
                float lit = input.fingerGlow;
                float edgeLit = saturate(rim * 1.4 + 0.25);
                color = lerp(color, _FingerColor.rgb, lit * 0.9);
                color += lerp(_FingerColor.rgb, 1, 0.5) * lit * edgeLit * 0.9;
                color = lerp(color, 1, lit * lit * edgeLit * 0.3);
                alpha = max(alpha, lit * 0.95);
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

            // A lit finger's outline swells by up to 2.5 mm, a bright halo round it.
            Varyings vert(Attributes input) { return Build(input, _OutlineWidth * (1 + _Kick * 0.7), 0.0025); }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float pulse = BeatPulse(input.positionWS) * step(_Pattern, 0.5);
                float3 color = lerp(_EdgeColor.rgb, _GlowColor.rgb, saturate(_Kick * 0.8 + pulse));
                color = lerp(color, 1, pulse * 0.4);
                float lit = input.fingerGlow;
                color = lerp(color, lerp(_FingerColor.rgb, 1, 0.3), lit);
                return half4(color, max(_EdgeColor.a, lit) * WristFade(input.positionWS));
            }
            ENDHLSL
        }
    }
}
