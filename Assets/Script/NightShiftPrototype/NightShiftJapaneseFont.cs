using TMPro;
using UnityEngine;

public static class NightShiftJapaneseFont
{
    private const string ResourceName = "NSP_Japanese SDF";
    private static TMP_FontAsset cachedFont;
    private static bool attemptedFallback;

    public static TMP_FontAsset Font
    {
        get
        {
            if (cachedFont == null)
                cachedFont = Resources.Load<TMP_FontAsset>(ResourceName);

            if (cachedFont == null && !attemptedFallback)
            {
                attemptedFallback = true;
                Font osFont = UnityEngine.Font.CreateDynamicFontFromOSFont(
                    new[] { "Noto Sans CJK JP", "Noto Sans JP", "Yu Gothic UI", "Meiryo", "sans-serif" },
                    64);
                if (osFont != null)
                    cachedFont = TMP_FontAsset.CreateFontAsset(osFont);
            }

            return cachedFont;
        }
    }

    public static void Apply(TMP_Text label)
    {
        if (label != null && Font != null)
            label.font = Font;
    }

    public static void ApplyToChildren(Transform root)
    {
        if (root == null || Font == null)
            return;

        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
            label.font = Font;
    }
}
