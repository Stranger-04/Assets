using UnityEngine;
using UnityEditor;
using System.Reflection;

public class Script
{
    public static object Main()
    {
        // 设置 Game View 分辨率
        var gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gameViewType == null)
            return "GameView type not found";

        var gameView = EditorWindow.GetWindow(gameViewType);
        if (gameView == null)
            return "GameView window not found";

        // 设置 Game View 为 Free Aspect，手动调整尺寸
        var sizeProp = gameViewType.GetProperty("targetSize", BindingFlags.Instance | BindingFlags.NonPublic);
        var selectedSizeIndexProp = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        // 打印当前状态
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"GameView found: {gameView != null}");

        if (selectedSizeIndexProp != null)
        {
            var idx = selectedSizeIndexProp.GetValue(gameView);
            sb.AppendLine($"Current size index: {idx}");
            // 设置为 Free Aspect (index 0)
            selectedSizeIndexProp.SetValue(gameView, 0);
        }

        if (sizeProp != null)
        {
            var s = sizeProp.GetValue(gameView);
            sb.AppendLine($"Current target size: {s}");
        }

        sb.AppendLine("Game view size reset.");
        return sb.ToString();
    }
}
