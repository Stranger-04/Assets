# file-placement — 文件放置分支

> 由 [G3] 触发。按文件类型 + 任务类别确定写入路径。

---

## 分支树

```
[G3] 文件放置
  │
  ├── ASK: 文件类型？
  │     ├── .cs（Roslyn 脚本）→ 路径由 [G2] 决定，跳过 G3
  │     ├── .shader  → 继续
  │     ├── .compute → 继续
  │     ├── .hlsl    → 继续
  │     └── .cs（运行时/Editor）→ 继续
  │
  └── ASK: 任务类别？
        ├── 全屏后处理 → Category: PostProcess
        │     └── TargetDir: Assets/Mine/Shaders/PostProcess/<effect>/
        ├── 3D 渲染    → Category: Render
        │     └── TargetDir: Assets/Mine/Shaders/Render/<effect>/
        ├── 视觉效果   → Category: Graph
        │     └── TargetDir: Assets/Mine/Shaders/Graph/<effect>/
        └── C# 逻辑    → Category: Script
              ├── 运行时  → Assets/Mine/Scripts/<module>/
              └── Editor  → Assets/Mine/Scripts/<module>/Editor/
```

---

## 决策表

| 文件类型 | 类别 | TargetDir |
|---------|------|-----------|
| `.shader` | Graph / PostProcess / Render | `Assets/Mine/Shaders/<cat>/<effect>/` |
| `.compute` | 同上 | 与所属 shader 同目录 |
| `.hlsl` | 同上 | 同上（共享 include） |
| `.cs` RT | Script | `Assets/Mine/Scripts/<module>/` |
| `.cs` Ed | Script | `Assets/Mine/Scripts/<module>/Editor/` |
| `.cs` Roslyn | — | [G2] 决定 |

---

## 类别判断

| 效果示例 | → 类别 |
|---------|--------|
| SSR, SSSM, Kuwahara, PCSS, POSS, DDOF, SNN, RimToon | PostProcess |
| Water, Grass, XRay, RainDrops | Render |
| CelToon, Cloud, Outline, Scan, Gate, SunShadow | Graph |
| CamController, InteractionManager, FGDLutBaker, NoiseGenerator | Script |

---

## OUTPUT 格式

```
## G3: File Placement
FileType: .shader | .compute | .hlsl | .cs
Category: PostProcess | Render | Graph | Script
TargetDir: <full path>
Exists: <ls result — must check before write>
```

## Red Flags

- 未 ls 目标目录就写文件 → 退回重查
- 同名文件已存在且非本次任务目标 → 暂停确认
