# Unity Developer — Memory Index

> 按日期命名的 memory 文件索引。新 memory 以 `YYYY-MM-DD-<slug>.md` 格式添加。

---

## 技术栈（不变式）

- Unity 6 + URP 17+，macOS Metal
- 自动化：unityctl（Editor 远程控制）、Roslyn（C# 运行时注入）

## 关键架构决策

- PCSS 阴影：PSSM 4-cascade split → tiled atlas → blocker search → penumbra → variable PCF
- 交互系统：Manager/Processor 分离架构，RT 管理下放，正交相机 CustomRenderer 深度比较输入
- Shader 组织：`Assets/Mine/Shaders/` 按效果分层
- 后处理：Unity 6 Blitter API（`_BlitTexture`，非 `_MainTex`）

---

## Memory 文件

| 文件 | 日期 | 摘要 |
|------|------|------|
| [2026-07-24-pcss-integration.md](2026-07-24-pcss-integration.md) | 2026-07-24 | PCSS 软阴影完整方案 |
| [2026-07-30-interaction-system.md](2026-07-30-interaction-system.md) | 2026-07-30 | 正交交互系统：Manager/Processor 架构 + Verlet 波方程 + 移动域重投影 |
| [2026-07-30-fgd-lut-baker.md](2026-07-30-fgd-lut-baker.md) | 2026-07-30 | FGD LUT 烘焙工具 + ENVFunction 合并 + _UseFGDLut 自动检测 |
