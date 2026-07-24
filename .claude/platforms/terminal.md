# Terminal Platform

> 纯终端行为配置。当 Unity Editor 不可用时，所有操作退化为文件编辑 + 知识查询。

## 可用能力

- 文件读写（Read、Write、Edit）
- Shell 命令执行（Bash）
- Git 操作
- 代码分析（grep、glob、search）

## 不可用能力

- Unity 编译验证（需 `unityctl asset refresh`）
- Play Mode 测试（需 `unityctl play enter`）
- Roslyn 脚本执行（需 `unityctl script execute`）
- 截图捕获（需 `unityctl screenshot capture`）
- 场景导航（需 `unityctl scene` / `snapshot`）

## 行为约定

当 Editor 不可用时：
1. 正常执行知识加载和代码编辑
2. 在输出末尾注明"Editor 未运行，未执行编译验证"
3. 提示用户"启动 Unity Editor 后可重新运行验证"
