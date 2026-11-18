// The Finger frame's camera filters, included by Portal.shader (which defines the music values _Pulse, _Bands, _High,
// _Clock and _EdgeColor, and Hash2). Through the window you see the real room behind it, as the headset's passthrough
// camera saw it, lined up with the room around the window: each window pixel's eye ray is followed to the room surface
// behind it (the scanned floor, ceiling, walls and furniture tops in _Surfaces, or stand-in walls 2 m away), and that
// point is projected into the camera image with _CamMatrixL/R, made from the camera's lens and where the camera was
// when it took the image (each eye uses the camera on its side when both run). Six filters then redraw the image:
// comic ink, heat vision, neon edges, bass tiles, night vision and a kaleidoscope, each moving with the music.
#ifndef FINGER_FRAME_FILTERS_INCLUDED
#define FINGER_FRAME_FILTERS_INCLUDED

#define FF_MAX_SURFACES 16

// Set per draw through the window's MaterialPropertyBlock, and declared outside UnityPerMaterial: arrays inside it
// didn't reach the Quest build.
TEXTURE2D(_CamTexL);
TEXTURE2D(_CamTexR);
float4x4 _CamMatrixL;
float4x4 _CamMatrixR;
float4 _CamTexel;       // 1/width, 1/height, width, height of the camera image
float _CamFade;         // 0 while the camera starts, 1 once its image shows
// Per surface four rows: its plane (normal into the room, offset), the planes along its two sides, its half size.
float4 _Surfaces[FF_MAX_SURFACES * 4];
float _SurfaceCount;
float _DepthCap;        // the farthest a room point may be (m)
float4 _Center;         // the window's middle (world)
float _BeatAge;         // seconds since the last beat
float _BeatFlash;       // how strongly the beat flashes in the filters: 1, or a quarter with Less flashing
float _Bass;            // the bass, smoothed, 0-1
float _Beats;           // beats so far
float _Spin;            // the kaleidoscope's turn (radians)

// Where a window pixel looks into the room, and where the camera saw that.
struct Lens
{
    float2 uv;      // in the camera image
    float seen;     // 1 where the camera saw it, fading out at the edge of its image
    float3 p;       // the room point (world)
    float3 r, u;    // two directions along its surface
    float2 window;  // the window's own 0-1 coordinates
    float2 centre;  // where the camera saw the room behind the window's middle
};

// The first room surface an eye ray meets, from the room side, within the surface's bounds; or _DepthCap away.
float3 RoomPoint(float3 eye, float3 dir, out float3 r, out float3 u)
{
    float best = _DepthCap;
    r = float3(1, 0, 0);
    u = float3(0, 1, 0);
    int count = (int)_SurfaceCount;
    [loop] for (int i = 0; i < count; i++)
    {
        float4 n = _Surfaces[i * 4];
        float facing = -dot(dir, n.xyz);
        if (facing < 1e-3) continue;
        float t = (dot(eye, n.xyz) + n.w) / facing;
        if (t < 0.05 || t >= best) continue;
        float3 p = eye + dir * t;
        float4 a = _Surfaces[i * 4 + 1];
        float4 b = _Surfaces[i * 4 + 2];
        float4 s = _Surfaces[i * 4 + 3];
        if (abs(dot(p, a.xyz) + a.w) > s.x || abs(dot(p, b.xyz) + b.w) > s.y) continue;
        best = t;
        r = a.xyz;
        u = b.xyz;
    }
    return eye + dir * best;
}

// Where the camera of this eye saw a world point, 0-1 across its image.
float2 CameraUV(float3 p, out float seen)
{
    float4x4 m = unity_StereoEyeIndex == 1 ? _CamMatrixR : _CamMatrixL;
    float3 h = mul(m, float4(p, 1)).xyz;
    float2 uv = h.xy / max(h.z, 1e-4);
    float2 border = min(uv, 1 - uv);
    seen = step(1e-3, h.z) * smoothstep(0, 0.03, min(border.x, border.y));
    return uv;
}

// The camera image of this eye (linear colour).
float3 Cam(float2 uv)
{
    float3 c;
    if (unity_StereoEyeIndex == 1) c = SAMPLE_TEXTURE2D_LOD(_CamTexR, sampler_LinearClamp, uv, 0).rgb;
    else c = SAMPLE_TEXTURE2D_LOD(_CamTexL, sampler_LinearClamp, uv, 0).rgb;
    return c;
}

// The camera point behind the window's middle, the same for the whole window (the vertex shader finds it).
float2 CentreUV()
{
    float3 eye = GetCameraPositionWS();
    float3 r, u;
    float seen;
    return CameraUV(RoomPoint(eye, normalize(_Center.xyz - eye + float3(0, 0, 1e-5)), r, u), seen);
}

Lens MakeLens(float3 eye, float3 dir, float2 window, float2 centre)
{
    Lens l;
    l.p = RoomPoint(eye, dir, l.r, l.u);
    l.uv = CameraUV(l.p, l.seen);
    l.window = window;
    l.centre = centre;
    return l;
}

float Luma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

// Brightness as the eye judges it (the camera's colours are linear), 0-1.
float Bright(float3 c) { return sqrt(saturate(Luma(c))); }

// A colour as the eye sees it, its saturation scaled.
float3 Vivid(float3 c, float amount)
{
    float3 g = sqrt(saturate(c));
    float grey = dot(g, 1.0 / 3);
    return saturate(grey + (g - grey) * amount);
}

float3 Rainbow(float h) { return saturate(abs(frac(h + float3(0, 2.0 / 3, 1.0 / 3)) * 6 - 3) - 1); }

// How sharply the brightness changes at uv: a Sobel filter over the 8 neighbours <radius> texels away, 0 to about 4.
float Sobel(float2 uv, float radius)
{
    float2 d = _CamTexel.xy * radius;
    float tl = Bright(Cam(uv + float2(-d.x, d.y))), tc = Bright(Cam(uv + float2(0, d.y))), tr = Bright(Cam(uv + d));
    float ml = Bright(Cam(uv - float2(d.x, 0))), mr = Bright(Cam(uv + float2(d.x, 0)));
    float bl = Bright(Cam(uv - d)), bc = Bright(Cam(uv - float2(0, d.y))), br = Bright(Cam(uv + float2(d.x, -d.y)));
    float gx = tr + 2 * mr + br - tl - 2 * ml - bl;
    float gy = tl + 2 * tc + tr - bl - 2 * bc - br;
    return length(float2(gx, gy));
}

// The same from four neighbours, cheaper, 0 to about 1.4.
float Cross4(float2 uv, float radius)
{
    float2 d = _CamTexel.xy * radius;
    float l = Bright(Cam(uv - float2(d.x, 0))), r = Bright(Cam(uv + float2(d.x, 0)));
    float b = Bright(Cam(uv - float2(0, d.y))), t = Bright(Cam(uv + float2(0, d.y)));
    return length(float2(r - l, t - b));
}

// The average brightness a little way around uv.
float Around(float2 uv, float radius)
{
    float2 d = _CamTexel.xy * radius;
    return (Bright(Cam(uv + float2(d.x, 0))) + Bright(Cam(uv - float2(d.x, 0))) + Bright(Cam(uv + float2(0, d.y))) + Bright(Cam(uv - float2(0, d.y)))) * 0.25;
}

// A soft ring spreading out from the window's middle after each beat, 0-1.
float BeatRing(float2 window, float speed, float width)
{
    float r = length((window - 0.5) * float2(1.3, 1));
    float d = (r - _BeatAge * speed) / width;
    return exp(-d * d) * exp(-_BeatAge * 2.2) * _BeatFlash;
}

// Coordinates along the room surface (m), for patterns that stay put on the room.
float2 OnSurface(Lens l) { return float2(dot(l.p, l.r), dot(l.p, l.u)); }

// Comic ink: the room as a printed comic. Flat poster colours in four tones, bolder on the kick; halftone dots
// printed on the room's surfaces in the shadows, swelling with the bass; and ink lines along every edge, thicker on
// the kick.
float3 ComicInk(Lens l)
{
    float3 c = Cam(l.uv);
    float b = Bright(c);
    float3 g = Vivid(c, 1.8 + _Pulse * 0.5);
    float grey = max(dot(g, 1.0 / 3), 0.02);
    float tones = 4;
    float level = floor(b * tones) + smoothstep(0.4, 0.6, frac(b * tones));
    level = lerp(0.16, 1.0, saturate(level / tones));
    float3 color = saturate(g / grey * level * 0.92);
    // The lightest tone is paper.
    color = lerp(color, float3(1, 0.96, 0.86), smoothstep(0.82, 1.0, level) * 0.5);

    float2 s = OnSurface(l) / 0.011;
    s = float2(s.x + s.y, s.y - s.x) * 0.7071;
    float2 f = frac(s) - 0.5;
    float size = saturate(0.8 - b) * (0.55 + _Bass * 0.3);
    float aa = max(fwidth(s.x), 1e-3);
    float dots = smoothstep(size + aa, size - aa, length(f));
    color *= 1 - dots * 0.6;

    float ink = smoothstep(0.3, 0.6, Sobel(l.uv, 1.1 + _Pulse * 0.9));
    color = lerp(color, float3(0.05, 0.035, 0.1), ink);
    return color * color;
}

// From cold to hot: deep blue, violet, crimson, orange, yellow, near white.
float3 HeatPalette(float h)
{
    h = saturate(h) * 5;
    float3 c = lerp(float3(0.0, 0.0, 0.05), float3(0.12, 0.0, 0.34), saturate(h));
    c = lerp(c, float3(0.6, 0.0, 0.3), saturate(h - 1));
    c = lerp(c, float3(1.0, 0.16, 0.02), saturate(h - 2));
    c = lerp(c, float3(1.0, 0.62, 0.0), saturate(h - 3));
    return lerp(c, float3(1.0, 1.0, 0.72), saturate(h - 4));
}

// Heat vision: brighter than its surroundings and warm-coloured reads hot (so it works in a dim room or a bright one):
// hands, faces and lamps glow, walls stay cool. The whole picture warms with the bass, and a ring of heat spreads out
// from the middle on each beat. Soft, like a thermal camera's picture.
float3 HeatVision(Lens l)
{
    float2 d = _CamTexel.xy * 1.2;
    float3 c = (Cam(l.uv + d) + Cam(l.uv - d) + Cam(l.uv + float2(d.x, -d.y)) + Cam(l.uv + float2(-d.x, d.y))) * 0.25;
    float b = Bright(c);
    float around = Around(l.uv, 12);
    float warm = saturate((c.r - c.b) * 3.5 - 0.35) * smoothstep(0.08, 0.3, b);
    float heat = 0.02 + b * 0.28 + (b - around) * 1.6 + warm * 0.45 + _Bass * 0.1;
    heat += BeatRing(l.window, 0.9, 0.12) * 0.16;
    return HeatPalette(heat);
}

// Neon edges: the room dims to a faint glow and every edge in it lights up like a neon tube, pink, violet and cyan
// running across the window and shifting with the music. A bright ring runs out through the lines on each beat, and
// the kick splits their colours apart.
float3 NeonEdges(Lens l)
{
    float3 c = Cam(l.uv);
    float3 room = lerp(Luma(c).xxx, c, 0.3) * 0.06 + float3(0.003, 0.0, 0.01);
    float2 o = float2(_CamTexel.x * (0.4 + _Pulse * 2.4), 0);
    float3 lines = smoothstep(0.035, 0.18, float3(Cross4(l.uv + o, 1.0), Cross4(l.uv, 1.0), Cross4(l.uv - o, 1.0)));
    float halo = smoothstep(0.02, 0.3, (Cross4(l.uv, 2.5) + Cross4(l.uv, 4.5)) * 0.5) * 0.4;
    float wave = dot(l.window, float2(1.2, 0.7)) + _Clock * 0.12 + _Bands.y * 0.6;
    float3 neon = lerp(float3(1.0, 0.16, 0.6), float3(0.12, 0.85, 1.0), 0.5 + 0.5 * sin(wave * 3.1416));
    neon = lerp(neon, float3(0.62, 0.28, 1.0), 0.45 * (0.5 + 0.5 * sin(wave * 1.7 + 2)));
    neon *= 1.2 + BeatRing(l.window, 1.0, 0.1) * 1.6 + _High.y * 0.3;
    // A whiter core along the brightest lines, as in a real tube.
    return room + lines * neon + pow(lines.g, 4) * 0.15 + halo * neon;
}

// Bass tiles: the room laid in square tiles on its own surfaces, each the colour the camera saw at its middle, 3 to
// 7.5 cm across and growing with the bass. A few tiles light up on each beat, a different few each time.
float3 BassTiles(Lens l)
{
    float size = lerp(0.03, 0.075, _Bass);
    float2 s = OnSurface(l) / size;
    float2 cell = floor(s);
    float2 f = s - cell - 0.5;
    float3 middle = l.p + (l.r * (cell.x + 0.5 - s.x) + l.u * (cell.y + 0.5 - s.y)) * size;
    float seen;
    float3 g = Vivid(Cam(CameraUV(middle, seen)), 1.4);
    // A few brightness steps, each keeping the tile's own colour.
    float grey = max(dot(g, 1.0 / 3), 0.02);
    g = saturate(g / grey * (floor(grey * 6 + 0.5) / 6 + 0.04));
    // A rounded tile with a dark seam, its upper edge catching the light.
    float aa = max(fwidth(s.x), 1e-3) * 1.2;
    float box = length(max(abs(f) - 0.3, 0)) - 0.13;
    float tile = smoothstep(aa, -aa, box);
    g *= 0.92 + (f.y - f.x) * 0.2;
    float pop = step(0.9, Hash2(cell + _Beats * 17.17)) * exp(-_BeatAge * 3) * _BeatFlash;
    g = saturate(g + pop * 0.35 * (g + 0.3));
    float3 color = lerp(float3(0.03, 0.03, 0.04), g, tile);
    return color * color;
}

// Night vision: the room amplified in green phosphor. The gain follows the light around each point, so a dim room
// comes up as clear as a lit one; fine grain grows with the highs, there are scanlines and an eyepiece's round edge, a
// gentle lift on the kick and a soft bright band sweeping down after each beat.
float3 NightVision(Lens l)
{
    float3 c = Cam(l.uv);
    float b = Bright(c);
    float v = 1 - exp(-b * 1.6 / (0.35 + Around(l.uv, 26)) * (1 + _Pulse * 0.2));
    v = smoothstep(0.1, 0.9, v + (b - Around(l.uv, 3)) * 0.8);
    v += (Hash2(floor(l.uv * _CamTexel.zw) + frac(floor(_Clock * 24) * 0.618) * 400) - 0.5) * (0.07 + _High.y * 0.05);
    v *= 0.9 + 0.1 * sin(l.window.y * 300);
    float band = l.window.y - (1.1 - _BeatAge * 1.4);
    v += exp(-band * band * 60) * exp(-_BeatAge * 1.2) * 0.16 * _BeatFlash;
    float2 q = (l.window - 0.5) * float2(1.6, 2);
    v = saturate(v) * smoothstep(1.25, 0.55, length(q));
    return float3(0.18, 1.0, 0.24) * pow(v, 1.6) + float3(0.6, 1, 0.65) * pow(v, 8) * 0.3;
}

// One corner of the kaleidoscope's triangle of side s, by its colour 0-2.
float2 MirrorCorner(float colour, float s)
{
    return colour < 0.5 ? float2(0, 0) : colour < 1.5 ? float2(s, 0) : float2(0.5 * s, 0.8660254 * s);
}

// A three-mirror kaleidoscope: the plane tiled with equilateral triangles of side s, each the mirror image of its
// neighbours, folded back onto the first one. Each lattice corner has one of three colours ((i - j) mod 3), every
// triangle has one corner of each, and mirroring keeps a corner's colour, so the point's weights on its triangle's
// corners, given to the first triangle's corners of the same colours, are the mirrored point. Also gives the
// distance to the nearest mirror (as a share of the triangle's height) and which triangle it was.
float2 MirrorFold(float2 p, float s, out float edge, out float2 cell)
{
    float u = p.y / (0.8660254 * s);
    float v = p.x / s - 0.5 * u;
    cell = floor(float2(v, u));
    float2 f = float2(v, u) - cell;
    float upper = step(1, f.x + f.y);
    float2 c0 = upper > 0.5 ? cell + 1 : cell;
    float2 c1 = upper > 0.5 ? cell + float2(0, 1) : cell + float2(1, 0);
    float2 c2 = upper > 0.5 ? cell + float2(1, 0) : cell + float2(0, 1);
    float3 w = upper > 0.5 ? float3(f.x + f.y - 1, 1 - f.x, 1 - f.y) : float3(1 - f.x - f.y, f.x, f.y);
    float3 colour = fmod(fmod(float3(c0.x - c0.y, c1.x - c1.y, c2.x - c2.y), 3) + 3, 3);
    edge = min(w.x, min(w.y, w.z));
    cell += upper * 0.5;
    return w.x * MirrorCorner(colour.x, s) + w.y * MirrorCorner(colour.y, s) + w.z * MirrorCorner(colour.z, s);
}

// Kaleidoscope: a triangle of the room behind the window's middle, mirrored over and over like a three-mirror
// kaleidoscope, the glass of each triangle faintly tinted. It turns slowly (faster with the mids), the patch it
// mirrors drifts round so new things come into it, it draws in a little on the kick, and a glint runs along the
// mirrors. It brightens itself in a dim room.
float3 Kaleidoscope(Lens l)
{
    float aspect = _CamTexel.z * _CamTexel.y;
    float2 d = (l.uv - l.centre) * float2(aspect, 1) * (1 - _Pulse * 0.06);
    float2x2 turn = float2x2(cos(_Spin), -sin(_Spin), sin(_Spin), cos(_Spin));
    const float side = 0.2;
    float edge;
    float2 cell;
    float2 folded = MirrorFold(mul(turn, d) + float2(0.5, 0.29) * side, side, edge, cell) - float2(0.5, 0.29) * side;
    float2 drift = float2(cos(_Spin * 0.35), sin(_Spin * 0.35)) * 0.05;
    float2 q = l.centre + (drift + mul(folded, turn)) / float2(aspect, 1);
    float exposure = clamp(0.62 / (Around(l.centre, 60) + 0.08), 1, 3);
    float3 g = Vivid(Cam(q), 1.6) * exposure;
    g = saturate((g - 0.5) * 1.2 + 0.5) * (0.92 + _Bands.x * 0.12);
    g = lerp(g, g * (0.6 + 0.8 * Rainbow(Hash2(cell) + _Clock * 0.02)), 0.25);
    g += _EdgeColor.rgb * exp(-edge * side * 0.866 / 0.0015) * (0.12 + _Pulse * 0.2);
    return g * g;
}

// While the camera starts: a soft, slowly turning glow in the window.
float3 Waiting(Lens l)
{
    float2 p = l.window - 0.5;
    float swirl = 0.5 + 0.5 * sin(atan2(p.y, p.x) * 3 + length(p) * 18 - _Clock * 2.5);
    return _EdgeColor.rgb * (0.03 + 0.05 * swirl) * smoothstep(0.75, 0.0, length(p));
}

float3 CameraFilter(float id, Lens l)
{
    float3 color;
    if (id < 0.5) color = ComicInk(l);
    else if (id < 1.5) color = HeatVision(l);
    else if (id < 2.5) color = NeonEdges(l);
    else if (id < 3.5) color = BassTiles(l);
    else if (id < 4.5) color = NightVision(l);
    else color = Kaleidoscope(l);
    // Past the edge of the camera's image (it sees less than you do) the window darkens softly.
    if (id < 4.5) color = lerp(_EdgeColor.rgb * 0.02, color, l.seen);
    return lerp(Waiting(l), color, _CamFade);
}

#endif
