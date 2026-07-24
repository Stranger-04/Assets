# Unity Lab — Memory

> 项目事实、活跃上下文、关键架构决策。会话启动时首先加载，结束时最后更新。

---

## 活跃上下文

- **当前分支**：unity6
- **最近工作**：PCSS 软阴影（4-cascade shadow mapping + blocker search + penumbra + variable PCF）
- **状态**：PCSS 最终集成调试中

## 技术栈

- Unity 6 + URP 17+
- 渲染管线：Forward+ / Deferred
- 关键包：unityctl（Editor 自动化）

## 关键架构决策

- **PCSS 阴影**：PSSM 4-cascade split → tiled atlas → blocker search → penumbra → variable PCF
- **Shader 组织**：`Assets/Mine/Shaders/` 按效果分层（Graph/、Volume/、PCSS/）
- **后处理**：Unity 6 Blitter API（非旧版 Graphics.Blit），纹理绑定 `_MainTex` → `_BlitTexture`

## 参考

- [Unity 6 后处理 Shader 差异](references/unity6-shader-differences.md) — RenderGraph、Blitter、纹理绑定、Vertex Shader 等完整差异速查
- [错误诊断规则表](../learnings/error-patterns.md) — 编译/运行时错误模式
- [安全红线](../learnings/safety.md) — 安全约束 + 经验教训

## 最近会话

| 日期 | 摘要 |
|------|------|
| 2026-07-24 | .claude 架构重构：memory → agent → platform → skill → CLI → script → memory |
