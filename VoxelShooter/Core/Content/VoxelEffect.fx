// VoxelEffect.fx — custom per-pixel lighting shader for MonoGame DesktopGL
// Targets vs_3_0 / ps_3_0 (SM3 supports dynamic loops; fully supported by MojoShader)
// No MonoGame macro headers needed — plain HLSL only.

// ── Sun (directional) ──────────────────────────────────────────────────────
float3 SunDirection;   // direction the ray TRAVELS (Z must be < 0 for side-scroller)
float3 SunColor;
float3 AmbientColor;

// ── Point lights ───────────────────────────────────────────────────────────
#define MAX_POINT_LIGHTS 8

float3 PointLightPosition[MAX_POINT_LIGHTS];
float3 PointLightColor[MAX_POINT_LIGHTS];
float  PointLightRadius[MAX_POINT_LIGHTS];
float  PointLightIntensity[MAX_POINT_LIGHTS];
float3 Tint;                           // per-draw colour multiplier (default 1,1,1 = no tint)

// ── Transforms ─────────────────────────────────────────────────────────────
float4x4 World;
float4x4 WorldViewProj;
float4x4 WorldInverseTranspose;

// ── Vertex shader ──────────────────────────────────────────────────────────
struct VSInput
{
    float4 Position : POSITION0;
    float3 Normal   : NORMAL0;
    float4 Color    : COLOR0;    // vertex colour = AO bake
};

struct VSOutput
{
    float4 Position    : POSITION0;
    float4 Color       : COLOR0;
    float3 WorldNormal : TEXCOORD0;
    float3 WorldPos    : TEXCOORD1;
};

VSOutput VS(VSInput input)
{
    VSOutput output;
    output.Position    = mul(input.Position, WorldViewProj);
    output.Color       = input.Color;
    output.WorldNormal = normalize(mul(input.Normal, (float3x3)WorldInverseTranspose));
    output.WorldPos    = mul(input.Position, World).xyz;
    return output;
}

// ── Pixel shader ───────────────────────────────────────────────────────────
float4 PS(VSOutput input) : COLOR0
{
    float3 normal = normalize(input.WorldNormal);

    // Directional sun (Lambert)
    // SunDirection is the direction the ray travels; negate to get vector-toward-light
    float  sunDot   = max(0, dot(normal, -SunDirection));
    float3 lighting = AmbientColor + SunColor * sunDot;

    // Point lights (quadratic attenuation) — always iterate all slots;
    // unused slots have PointLightIntensity[i]==0 so they contribute nothing.
    for (int i = 0; i < MAX_POINT_LIGHTS; i++)
    {
        if (PointLightIntensity[i] <= 0.0) continue;
        float3 toLight   = PointLightPosition[i] - input.WorldPos;
        float  dist      = length(toLight);
        float  r2        = max(0.0001, PointLightRadius[i] * PointLightRadius[i]);
        float  atten     = 1.0 / (1.0 + (dist * dist) / r2);
        float  nDotL     = max(0, dot(normal, normalize(toLight)));
        lighting += PointLightColor[i] * PointLightIntensity[i] * atten * nDotL;
    }

    // Vertex colour carries the AO bake — multiply so lit areas remain AO-shaded
    return float4(saturate(input.Color.rgb * lighting * Tint), input.Color.a);
}

// ── Technique ──────────────────────────────────────────────────────────────
technique VoxelEffect
{
    pass
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader  = compile ps_3_0 PS();
    }
}
