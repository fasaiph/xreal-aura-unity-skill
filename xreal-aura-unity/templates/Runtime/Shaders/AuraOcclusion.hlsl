// Depth occlusion for custom URP shaders. Where a real object is closer than the virtual surface,
// RealWorldVisibility returns 0, and the shader draws the pixel black, which the Aura shows as
// see-through. Globals are set by AR Foundation's ARShaderOcclusion; the sampling follows the
// AR Foundation samples' OcclusionComputation.hlsl.
// Requires: multi_compile _ XR_LINEAR_DEPTH and multi_compile _ XR_HARD_OCCLUSION.
#ifndef AURA_OCCLUSION_INCLUDED
#define AURA_OCCLUSION_INCLUDED

// Set globally by ARShaderOcclusion while environment depth is running.
TEXTURE2D_ARRAY_FLOAT(_EnvironmentDepthTexture);
SAMPLER(sampler_EnvironmentDepthTexture);
float4x4 _EnvironmentDepthProjectionMatrices[2];
float2 _NdcLinearConversionParameters;
int _IsOcclusionOn;

float LinearizeDepth(float symmetricNdc)
{
    return _NdcLinearConversionParameters.x / (symmetricNdc + _NdcLinearConversionParameters.y);
}

// 1 where virtual content is in front of the real world, 0 where something real is in front of it.
float RealWorldVisibility(float3 positionWS)
{
    if (_IsOcclusionOn == 0)
        return 1.0;

    float4 depthSpace = mul(_EnvironmentDepthProjectionMatrices[unity_StereoEyeIndex], float4(positionWS, 1.0));
    float2 uv = (depthSpace.xy / depthSpace.w + 1.0) * 0.5;
    if (any(uv < 0.0) || any(uv > 1.0))
        return 1.0;

    float sampled = SAMPLE_TEXTURE2D_ARRAY(_EnvironmentDepthTexture, sampler_EnvironmentDepthTexture, uv, unity_StereoEyeIndex).r;
    #ifdef XR_LINEAR_DEPTH
        float environmentDepth = sampled;
    #else
        float environmentDepth = LinearizeDepth(sampled * 2.0 - 1.0);
    #endif
    float sceneDepth = LinearizeDepth(depthSpace.z / depthSpace.w);

    #ifdef XR_HARD_OCCLUSION
        return environmentDepth > sceneDepth ? 1.0 : 0.0;
    #else
        // Soft falloff over a few centimetres (wider far away, where depth is noisier),
        // so edges against real objects don't flicker.
        float tolerance = lerp(0.02, 0.5, saturate((environmentDepth - 3.5) / 2.0));
        float trustNear = saturate(environmentDepth / 0.05);
        float delta = sceneDepth - environmentDepth;
        float visible = saturate(1.0 - (delta + tolerance) / (2.0 * tolerance));
        return max(visible, 1.0 - trustNear);
    #endif
}

#endif
