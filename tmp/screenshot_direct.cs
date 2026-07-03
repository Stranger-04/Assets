using UnityEngine;

public class Script
{
    public static object Main()
    {
        var filename = $"screenshot_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
        var path = System.IO.Path.Combine(Application.dataPath, "../Screenshots", filename);
        ScreenCapture.CaptureScreenshot(path, 1);
        return $"Screenshot saved: {path}";
    }
}
