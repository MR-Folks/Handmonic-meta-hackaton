// The Cube field's blocks: one mesh holds every block (CubeField builds it: four sides and a top each, the block's
// number in the second UV set) and each frame the world sends every block's height, colour, glow and opacity in
// _Blocks. The vertex shader stands each block up to its height. Each block is a column of coloured light: almost
// clear at its foot, so it seems to grow out of the floor, brightening towards a glowing cap, with bright edges, so
// the field reads clearly over passthrough without hiding the room. The blocks are drawn nearest first and write
// depth, so a block hides the ones behind it instead of piling glass on glass: the room still shows through, and
// there is hardly any overdraw.
Shader "MusicInYourHands/CubeField"
{
    Properties
    {
        _Low ("Colour of the low bands", Color) = (1, 0.2, 0.56, 1)
        _Mid ("Colour of the middle bands", Color) = (0.52, 0.32, 1, 1)
        _High ("Colour of the high bands", Color) = (0.1, 0.82, 1, 1)
        _Fill ("Side opacity", Range(0, 1)) = 0.55
        _TopFill ("Top opacity", Range(0, 1)) = 0.8
        _EdgeWidth ("Edge width (m)", Float) = 0.005
        _Width ("Block width (m) (set by CubeField)", Float) = 0.17
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual
            // Separate alpha blending keeps the alpha channel right for compositing over passthrough.
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Low;
                float4 _Mid;
                float4 _High;
                float _Fill;
                float _TopFill;
                float _EdgeWidth;
                float _Width;
            CBUFFER_END

            // Per block, set by CubeField each frame: height (m), colour (0 the low bands to 1 the high ones), glow, and
            // opacity (0 hides the block). As many as CubeField.MaxBlocks.
            float4 _Blocks[640];

            struct Attributes
            {
                float4 positionOS : POSITION;   // y: 0 at the floor, 1 at the top
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;          // across the face (m); on the top, also along it
                float2 block : TEXCOORD1;       // the block's number, and a random 0-1
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 face : TEXCOORD0;        // across (m), up or along (m), the block's height (m), 1 on the top
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 color : TEXCOORD3;       // the block's colour and opacity
                float glow : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 Palette(float t)
            {
                float3 c = lerp(_Low.rgb, _Mid.rgb, saturate(t * 2));
                return lerp(c, _High.rgb, saturate(t * 2 - 1));
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4 b = _Blocks[(int)(input.block.x + 0.5)];
                float height = max(b.x, 0.004);
                float3 p = input.positionOS.xyz;
                float up = p.y * height;
                p.y = up;
                float3 positionWS = TransformObjectToWorld(p);
                output.positionWS = positionWS;
                // A hidden block folds into a single point, so nothing of it is drawn.
                output.positionCS = b.w > 0.002 ? TransformWorldToHClip(positionWS) : float4(0, 0, 0, 1);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                float top = step(0.5, input.normalOS.y);
                output.face = float4(input.uv.x, top > 0.5 ? input.uv.y : up, height, top);
                output.color = float4(Palette(b.y), b.w);
                output.glow = b.z;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float across = input.face.x, upward = input.face.y, height = input.face.z;
                bool top = input.face.w > 0.5;
                float glow = input.glow;
                float3 base = input.color.rgb;

                // Distance (m) to the nearest edge of this face: the sides' upright edges and top rim, or the top's
                // four rims. Kept at least a pixel wide (and dimmed to match) so far edges don't shimmer.
                float sides = min(across, _Width - across);
                float ends = top ? min(upward, _Width - upward) : height - upward;
                float d = min(sides, ends);
                float pixel = max(fwidth(d), 1e-5);
                float width = max(_EdgeWidth, pixel * 0.8);
                float edge = saturate((width + pixel * 0.5 - d) / pixel) * (_EdgeWidth / width);

                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                float grazing = 1 - saturate(abs(dot(n, v)));
                float lit = 1 + glow * 0.7;
                float rise = top ? 1 : saturate(upward / max(height, 1e-3));

                float3 color;
                float alpha;
                if (top)
                {
                    // The glowing cap: bright on the tall blocks, softer on the low tiles near you.
                    float tall = saturate(height / 0.3);
                    color = lerp(base, 1, 0.1 + glow * 0.45) * (1.05 + glow * 0.6);
                    alpha = _TopFill * lerp(0.4, 1, tall) * (0.85 + glow * 0.3);
                }
                else
                {
                    // Light rising up the block: almost clear at its foot, so it seems to grow out of the floor,
                    // brightest under its cap, and more solid at a glancing angle so it reads as a volume.
                    float shade = 0.85 + 0.15 * dot(n, normalize(float3(0.55, 0, 0.83)));
                    float glowUp = rise * rise;
                    float cap = exp(-(height - upward) / 0.03) * 0.45;
                    color = base * ((0.4 + 0.7 * glowUp) * shade + cap) * lit;
                    alpha = _Fill * (0.25 + glowUp + grazing * grazing * 0.5) + cap * 0.2;
                }
                float3 rim = lerp(base, 1, 0.25 + glow * 0.4) * (1.25 + glow * 0.5);
                color = lerp(color, rim, edge);
                alpha = lerp(alpha, top ? 0.95 : lerp(0.3, 0.95, rise), edge);
                return half4(color, saturate(alpha * input.color.a));
            }
            ENDHLSL
        }
    }
}
