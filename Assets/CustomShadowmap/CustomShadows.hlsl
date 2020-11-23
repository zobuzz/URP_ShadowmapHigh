#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
//#include "Core.hlsl"

//add by sj
#define INV_1024 0.000976563f
#define INV_2048 0.0004882813f

TEXTURE2D_SHADOW(_ZorroShadowmapTexture);
SAMPLER_CMP(sampler__ZorroShadowmapTexture);

sampler2D _CustomLightShadowmapTexture;

// GLES3 causes a performance regression in some devices when using CBUFFER.
#ifndef SHADER_API_GLES3
CBUFFER_START(MainLightShadows)
#endif
// Last cascade is initialized with a no-op matrix. It always transforms
// shadow coord to half3(0, 0, NEAR_PLANE). We use this trick to avoid
// branching since ComputeCascadeIndex can return cascade index = MAX_SHADOW_CASCADES
float4x4    _CustomLightWorldToShadow;
float4x4    _ZorroShadowMatrix;
float4      _ZorroShadowParams;

#ifndef SHADER_API_GLES3
CBUFFER_END
#endif


#endif
