// =============================================================
// 文件：ToonGuofengLighting.hlsl（E1-S2，ADR-008）
// 作用：国风 Toon 光照纯函数库——ramp 分档 / 水墨阴影色 / Rim / 笔触扰动 / 分档高光。
// 规范：
//   - 【纯函数】：本文件不声明贴图、不声明 CBUFFER、不依赖任何 pass 上下文，
//     只做数值→数值映射，便于跨 shader 复用与单独推理。
//   - 跨版本单路径（C5）：不含任何版本宏。
//   - 描边：【无】。屏幕勾线 100% 归墨韵 Ink Pass（R5 红线），本库禁止出现描边函数。
// 参数哲学（ADR-008）：分档数/软硬度/色调全部是美术参数，骨架不钉视觉值。
// =============================================================

#ifndef MJ_TOON_GUOFENG_LIGHTING_INCLUDED
#define MJ_TOON_GUOFENG_LIGHTING_INCLUDED

// ---------------- 主光 ramp：smoothstep 双阈值分档（2–3 档） ----------------
// x        : ramp 输入（通常 = saturate(NdotL) * shadowAtten，可先经笔触扰动）
// threshold: 明暗交界位置
// softness : 交界软硬（越小越"刀切"，毛笔感靠笔触扰动而非大软度）
// bands    : 2 或 3（3 档时在 threshold*0.5 处插入中间档，输出 0 / 0.5 / 1）
half MJToonRamp(half x, half threshold, half softness, half bands)
{
    half hi = smoothstep(threshold - softness, threshold + softness, x);
    half midTh = threshold * 0.5;
    half mid = smoothstep(midTh - softness, midTh + softness, x);
    half three = step(2.5, bands);
    // 2 档：hi；3 档：0.5*mid + 0.5*hi（0 / 0.5 / 1 三级）
    return lerp(hi, 0.5 * mid + 0.5 * hi, three);
}

// ---------------- 笔触扰动：让明暗交界呈毛笔干湿变化 ----------------
// brushSample: 笔触法线图采样值（0..1，取单通道即可），0.5 为中性
half MJBrushJitter(half rampInput, half brushSample, half strength)
{
    return saturate(rampInput + (brushSample - 0.5h) * strength);
}

// ---------------- 水墨阴影色：乘色而非压黑（"淡墨"感的关键） ----------------
// litFactor = ramp 输出；暗部趋向 baseColor * shadowTint（冷灰偏青），亮部为 baseColor
half3 MJShadowTintMix(half3 baseColor, half3 shadowTint, half litFactor)
{
    return baseColor * lerp(shadowTint, half3(1.0h, 1.0h, 1.0h), litFactor);
}

// ---------------- Rim：菲涅尔 × 受光侧掩码（勾勒剪影，替代高光） ----------------
// lightSideMask: 0=全侧发光，1=只亮受光侧
half3 MJRimLight(half3 normalWS, half3 viewDirWS, half3 lightDirWS,
                 half3 rimColor, half rimPower, half lightSideMask)
{
    half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirWS)), rimPower);
    half mask = lerp(1.0h, saturate(dot(normalWS, lightDirWS)), lightSideMask);
    return rimColor * fresnel * mask;
}

// ---------------- 分档色块高光（默认关，国风非塑料感；uniform 分支控制） ----------------
half MJSpecularBand(half3 normalWS, half3 viewDirWS, half3 lightDirWS,
                    half threshold, half softness)
{
    half3 halfDir = normalize(viewDirWS + lightDirWS);
    half ndoth = saturate(dot(normalWS, halfDir));
    return smoothstep(threshold - softness, threshold + softness, ndoth);
}

// ---------------- 附加光：半兰伯特 × ramp 一档（950M 上限 1–3 盏点光，性能契约§5） ----------------
half MJAdditionalLightBand(half ndotl, half attenuation)
{
    half halfLambert = ndotl * 0.5h + 0.5h;
    return smoothstep(0.45h, 0.55h, halfLambert * attenuation);
}

// ---------------- 高度雾接入点（ADR-010 v2 备份路径，S2 默认 no-op） ----------------
// S2 内高度雾走墨韵全屏 Pass（E1-S3），本钩子保持恒等；【不要】在此实现雾。
half3 ApplyMJHeightFog(half3 color, float3 positionWS)
{
    return color;
}

#endif // MJ_TOON_GUOFENG_LIGHTING_INCLUDED
