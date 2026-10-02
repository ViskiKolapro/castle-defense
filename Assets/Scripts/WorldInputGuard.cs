using UnityEngine;

// Blocks world-space Collider2D clicks while a modal UI panel is actually visible.
// This intentionally checks active GameObjects instead of only static flags, so Play Mode
// restarts cannot leave the game permanently blocked when Domain Reload is disabled.
public static class WorldInputGuard
{
    public static bool IsBlocked()
    {
        return IsActive("HeroPanel") ||
               IsActive("HeroListPanel") ||
               IsActive("CityArcherPanel") ||
               IsActive("CastleUpgradePanel") ||
               IsActive("EvolutionPanel") ||
               IsActive("EvolutionInfoPanel") ||
               IsActive("EvolutionInfoPanel2") ||
               IsActive("PausePanel") ||
               IsActive("UpdatePanel");
    }

    static bool IsActive(string objectName)
    {
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t == null || t.name != objectName || !t.gameObject.scene.IsValid()) continue;
            if (t.gameObject.activeInHierarchy) return true;
        }
        return false;
    }
}
