using UnityEngine;
public class Script {
    public static object Main() {
        PCSSFeature.s_DebugPass = 0;
        PCSSFeature.s_DebugPCSSMode = 0;
        return "PCSS normal mode (min 4px PCF)";
    }
}
