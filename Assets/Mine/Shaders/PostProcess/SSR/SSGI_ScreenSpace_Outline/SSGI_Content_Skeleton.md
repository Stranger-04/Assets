# SSGI 内容骨架

## 目标

屏幕空间全局光照框架，用于补充实时动态间接光。

## 总体结构

Scene Data - Depth - Normal - Color - Roughness - Motion Vector

↓

Ray Generation - Diffuse Ray - 半球随机采样 - 间接漫反射 - Specular
Ray - SSR - Stochastic SSR - SSPR - AO Ray - SSAO - HBAO

↓

Ray Intersection - Ray March - DDA - Hi-Z

输入: - Screen Depth - Screen Normal

输出: - Hit UV - Hit Depth - Hit Confidence

↓

Lighting Reconstruction

Indirect Diffuse: - Sample hit color - Diffuse BRDF - Distance
attenuation

Indirect Specular: - Reflection sample - Roughness filtering - Fresnel

AO: - Occlusion factor

↓

Filtering

-   Temporal Accumulation
-   Reprojection
-   Bilateral Blur
-   Variance Clamp

↓

Composite

Final Lighting = Direct Lighting + Indirect Diffuse + Indirect
Specular + AO

## 扩展方向

替换 Ray Intersection 层即可升级: - Raymarch - DDA - Hi-Z - Hardware Ray
Tracing
