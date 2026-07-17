# error-patterns — 错误诊断规则表

> 运行时和编译时的常见错误模式及其自动诊断策略。

---

## 编译错误

| 错误码 / 模式 | 含义 | 诊断方向 |
|--------------|------|---------|
| `error CSxxxx` | C# 编译错误 | 读取错误文件 + 行号 → 修复语法 |
| `error CS0246` | 找不到类型或命名空间 | 检查 using 语句 → 补充引用 |
| `error CS0103` | 名称不存在 | 检查变量/方法声明 → 修正拼写或补充声明 |
| `error CS1061` | 类型不包含某方法 | 检查方法签名 / 扩展方法 / using |

## 运行时错误

| 日志模式 | 含义 | 诊断方向 |
|---------|------|---------|
| `NullReferenceException` | 空引用 | 定位变量 → 检查序列化/域重载 → 修复初始化 |
| `Thread group size must be above zero` | Compute Shader kernel 无效 | 检查 kernel index / FindKernel 返回值 → 打印调试信息 |
| `IndexOutOfRangeException` | 数组越界 | 检查数组长度 / 循环边界 |
| `MissingReferenceException` | 引用的对象已被销毁 | 检查生命周期 / Find 调用时机 |

## 静默失败

| 症状 | 可能原因 | 诊断方向 |
|------|---------|---------|
| 无日志输出 | 初始化守卫错误 | 检查 `[SerializeField]` / `Awake` vs `OnEnable` |
| 渲染无效果 | Shader 未正确绑定 | 检查 Material.SetShader / RenderGraph 纹理绑定 |
| 数据不更新 | ComputeShader 未 dispatch | 检查 kernel index / thread group / buffer binding |

## 模式差异

| 模式 | 诊断行为 |
|------|---------|
| 研发模式 | 报告诊断建议 → ⏸️ 暂停，等待人工决定 |
| 生产模式 | 自动修复 → 最多 3 次 → 失败后兜底退出 |
