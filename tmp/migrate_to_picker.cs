using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Script
{
    public static object Main()
    {
        var shaderDir = "Assets/Mine/Shaders/Chosen";
        var scriptDir = "Assets/Mine/Scripts/Chosen";
        var newScriptDir = "Assets/Mine/Scripts/Picker";

        // ── 1. 复制 shader 文件到 Scripts/Chosen ─────────────────

        foreach (var file in Directory.GetFiles(shaderDir))
        {
            if (file.EndsWith(".shader") || file.EndsWith(".shader.meta"))
            {
                var fileName = Path.GetFileName(file);
                var dest = Path.Combine(scriptDir, fileName);
                if (!File.Exists(dest))
                    File.Copy(file, dest);
                File.Copy(file.Replace(".meta", ""), dest.Replace(".meta", ""), true);
            }
        }

        // ── 2. 更新所有 .cs 内容（命名空间 + 引用 + shader 名） ──

        foreach (var file in Directory.GetFiles(scriptDir, "*.cs", SearchOption.AllDirectories))
        {
            var c = File.ReadAllText(file);
            c = c.Replace("namespace Mine.Chosen", "namespace Mine.Picker");
            c = c.Replace("using Mine.Chosen", "using Mine.Picker");
            c = c.Replace("\"Mine/Chosen/", "\"Mine/Picker/");
            c = c.Replace("\"Chosen/", "\"Picker/");
            File.WriteAllText(file, c);
        }

        // ── 3. 更新所有 .shader 内容（shader 名） ───────────────

        foreach (var file in Directory.GetFiles(scriptDir, "*.shader", SearchOption.AllDirectories))
        {
            var c = File.ReadAllText(file);
            c = c.Replace("\"Mine/Chosen/", "\"Mine/Picker/");
            File.WriteAllText(file, c);
        }

        // ── 4. 重命名文件夹 ───────────────────────────────────────

        AssetDatabase.Refresh();
        if (AssetDatabase.IsValidFolder(scriptDir))
        {
            var err = AssetDatabase.MoveAsset(scriptDir, newScriptDir);
            if (!string.IsNullOrEmpty(err)) return $"Rename error: {err}";
        }

        // ── 5. 清理旧 Shaders/Chosen 中的 shader 文件 ──────────────

        foreach (var file in Directory.GetFiles(shaderDir))
        {
            if (file.EndsWith(".shader") || file.EndsWith(".shader.meta"))
            {
                var relPath = file.Replace("\\", "/").Replace(Application.dataPath, "Assets");
                AssetDatabase.DeleteAsset(relPath);
            }
        }

        // ── 6. 重建 Renderer Features ──────────────────────────────

        AssetDatabase.Refresh();

        var rendererPath = "Assets/Settings/PC_Renderer.asset";
        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (rendererData != null)
        {
            // 移除旧的 Mine.Chosen 或 Mine.Picker feature
            rendererData.rendererFeatures.RemoveAll(f =>
                f == null || (f.GetType().Namespace != null &&
                    (f.GetType().Namespace == "Mine.Picker" || f.GetType().Namespace == "Mine.Chosen")));

            // 通过反射创建新 Feature（避免编译时类型依赖）
            var pickerType = System.Type.GetType("Mine.Picker.PickerFeature, Assembly-CSharp");
            var outlineType = System.Type.GetType("Mine.Picker.OutlineFeature, Assembly-CSharp");

            if (pickerType != null && outlineType != null)
            {
                var pf = ScriptableObject.CreateInstance(pickerType);
                pf.name = "PickerFeature";
                AssetDatabase.AddObjectToAsset(pf, rendererPath);
                rendererData.rendererFeatures.Add(pf);

                var of = ScriptableObject.CreateInstance(outlineType);
                of.name = "OutlineFeature";
                AssetDatabase.AddObjectToAsset(of, rendererPath);
                rendererData.rendererFeatures.Add(of);
            }
            else
            {
                return "Type resolution failed. PickerType=" + (pickerType != null) + " OutlineType=" + (outlineType != null);
            }

            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
        }

        AssetDatabase.Refresh();
        return "Migration done: Shaders→Scripts/Picker, namespace Mine.Picker, naming Picker/";
    }
}
