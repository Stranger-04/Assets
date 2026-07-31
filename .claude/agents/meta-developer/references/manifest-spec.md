# Manifest 规范 — Agent 自描述段格式

> meta-developer 通过读取每个 agent 的"自描述"段来了解其依赖和知识边界。
> 所有 developer agent 必须在 md 文件中包含此段。

---

## 格式

```markdown
## 自描述

### 依赖
- **references**: <目录名列表，逗号分隔>
- **skills**: <skill 名列表，逗号分隔>
- **memory**: <memory 文件相对路径>

### 知识边界
- **擅长**: <领域描述>
- **不擅长**: <不涉及的领域>
- **版本**: <引擎/框架 + 平台>

### 触发条件
- **关键词**: <逗号分隔>
- **文件路径**: <路径 pattern>
```

---

## 字段说明

| 字段 | 类型 | 用途 | 示例 |
|------|------|------|------|
| `references` | 目录名列表 | meta 验证这些目录存在且完整 | `urp-shader-lib/, unity6-api/` |
| `skills` | skill 名列表 | meta 验证这些 skill 存在且不被误删 | `auto-manager, unity-editor` |
| `memory` | 相对路径 | meta 验证 memory 文件存在 | `../memory/MEMORY.md` |
| `擅长` | 文本 | meta 用于路由判断 | `URP Shader, Compute Shader` |
| `不擅长` | 文本 | meta 避免将该 agent 用于不适配任务 | `ShaderGraph, VFX Graph` |
| `版本` | 文本 | meta 检查 references 是否过期 | `Unity 6 URP 17+, macOS Metal` |
| `关键词` | 列表 | 全局路由表的关键词来源 | `Shader, HLSL, Compute` |
| `文件路径` | pattern | 文件操作触发 agent 的条件 | `Assets/Mine/Shaders/` |

---

## meta-developer 如何使用

```
1. ls .claude/agents/*.md | grep -v meta-developer
   → 找到所有被维护 agent

2. 读取每个 agent 的"自描述"段
   → 提取 references / skills / memory 依赖

3. 验证
   ├── references 目录是否存在且有 README.md
   ├── skills 目录是否存在且有 SKILL.md
   ├── memory 文件是否存在
   └── 关键词是否与其他 agent 冲突

4. 发现缺口 → 提示或自动补全
```

---

## 版本标记

当 manifest 格式发生变化时，在"自描述"标题后添加版本：

```markdown
## 自描述 (manifest v1)
```

meta-developer 读取版本号，向下兼容旧格式。
