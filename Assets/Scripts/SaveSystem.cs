using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveSystem : MonoBehaviour
{
    [Serializable] public class HeroSave
    {
        public string heroName;
        public int heroLevel;
        public bool isPurchased;
        public bool isInstalled;
        public int installedSlot;
        public bool evolution1Purchased;
        public bool evolution2Purchased;
        public int activeEvolution;
        public bool victoriaRedEvolution1Purchased;
        public bool victoriaRedEvolution2Purchased;
        public bool victoriaBlueEvolution1Purchased;
        public bool victoriaBlueEvolution2Purchased;
        public int victoriaActiveEvolution;
    }

    [Serializable] public class SquadSave
    {
        public bool purchased;
        public int level;
        public int archerCount;
    }

    [Serializable] public class SaveData
    {
        public int saveVersion = 6;
        public int gold;
        public int emeralds;
        public int playerLevel;
        public int currentExperience;
        public int currentWave;
        public int lastCompletedWave;
        public int castleLevel;
        public List<HeroSave> heroes = new List<HeroSave>();
        public SquadSave squad1 = new SquadSave();
        public SquadSave squad2 = new SquadSave();
    }

    PlayerProgress progress;
    string SavePath => Path.Combine(Application.persistentDataPath, "save.json");
    Coroutine autosaveRoutine;

    void Awake()
    {
        progress = GetComponent<PlayerProgress>();
    }

    IEnumerator Start()
    {
        // Let all scene Start() methods build their normal runtime objects first.
        yield return null;

        if (progress == null) progress = PlayerProgress.Instance;
        if (progress == null || !progress.enablePersistentSave) yield break;

        if (progress.resetSaveOnPlay)
        {
            DeleteSave();
            progress.resetSaveOnPlay = false;
        }

        Load();
        autosaveRoutine = StartCoroutine(AutosaveLoop());
    }

    IEnumerator AutosaveLoop()
    {
        var wait = new WaitForSecondsRealtime(1f);
        while (true)
        {
            yield return wait;
            Save();
        }
    }

    public void Save()
    {
        if (progress == null || !progress.enablePersistentSave) return;

        SaveData d = new SaveData
        {
            gold = progress.gold,
            emeralds = progress.emeralds,
            playerLevel = progress.level,
            currentExperience = progress.currentExperience
        };

        WaveSpawner waves = FindAnyObjectByType<WaveSpawner>();
        if (waves != null)
        {
            d.currentWave = Mathf.Max(1, waves.currentWave);
            d.lastCompletedWave = Mathf.Max(0, waves.lastCompletedWave);
        }

        Castle castle = FindAnyObjectByType<Castle>();
        if (castle != null) d.castleLevel = Mathf.Max(1, castle.castleLevel);

        Hero[] heroes = FindObjectsByType<Hero>(FindObjectsInactive.Include);
        foreach (Hero h in heroes)
        {
            if (h == null || string.IsNullOrWhiteSpace(h.heroName)) continue;
            d.heroes.Add(new HeroSave
            {
                heroName = h.heroName,
                heroLevel = h.heroLevel,
                isPurchased = h.isPurchased,
                isInstalled = h.isInstalled,
                installedSlot = h.installedSlot,
                evolution1Purchased = h.evolution1Purchased,
                evolution2Purchased = h.evolution2Purchased,
                activeEvolution = h.activeEvolution,
                victoriaRedEvolution1Purchased = h.victoriaRedEvolution1Purchased,
                victoriaRedEvolution2Purchased = h.victoriaRedEvolution2Purchased,
                victoriaBlueEvolution1Purchased = h.victoriaBlueEvolution1Purchased,
                victoriaBlueEvolution2Purchased = h.victoriaBlueEvolution2Purchased,
                victoriaActiveEvolution = h.victoriaActiveEvolution
            });
        }

        CityArcherManager archers = FindAnyObjectByType<CityArcherManager>();
        if (archers != null)
        {
            CopySquadToSave(archers.squad1, d.squad1);
            CopySquadToSave(archers.squad2, d.squad2);
        }

        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(d, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning("Save failed: " + e.Message);
        }
    }

    public void Load()
    {
        if (progress == null || !progress.enablePersistentSave || !File.Exists(SavePath)) return;

        try
        {
            SaveData d = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            if (d == null) return;

            progress.gold = Mathf.Max(0, d.gold);
            progress.emeralds = Mathf.Max(0, d.emeralds);
            progress.level = Mathf.Max(1, d.playerLevel);
            progress.currentExperience = Mathf.Max(0, d.currentExperience);

            WaveSpawner waves = FindAnyObjectByType<WaveSpawner>();
            if (waves != null)
            {
                waves.currentWave = Mathf.Max(1, d.currentWave);
                waves.lastCompletedWave = Mathf.Max(0, d.lastCompletedWave);
                // SaveSystem loads after WaveSpawner.Start(), so refresh the already-created HUD text now.
                waves.UpdateWaveText();
            }

            Castle castle = FindAnyObjectByType<Castle>();
            if (castle != null)
            {
                castle.castleLevel = Mathf.Max(1, d.castleLevel);
                castle.ApplyLevelStats(true);
            }

            Hero[] heroes = FindObjectsByType<Hero>(FindObjectsInactive.Include);
            foreach (Hero h in heroes)
            {
                HeroSave hs = d.heroes.Find(x => x.heroName == h.heroName);
                if (hs == null) continue;
                // Scene/Inspector stores level-1 damage. Save stores the level, so rebuild
                // permanent damage from that baseline instead of leaving it at level-1 damage.
                float levelOneDamage = h.damage - Mathf.Max(0, h.heroLevel - 1) * h.damagePerLevel;
                h.heroLevel = Mathf.Clamp(hs.heroLevel, 1, h.maxHeroLevel);
                h.damage = levelOneDamage + Mathf.Max(0, h.heroLevel - 1) * h.damagePerLevel;
                h.isPurchased = hs.isPurchased;
                h.installedSlot = hs.isInstalled ? Mathf.Clamp(hs.installedSlot, 1, HeroSlotManager.TotalSlots) : 0;
                h.SetInstalled(hs.isInstalled);
                h.evolution1Purchased = hs.evolution1Purchased || hs.evolution2Purchased;
                h.evolution2Purchased = hs.evolution2Purchased;
                // Backward compatibility: active evolution was introduced after older saves
                // were already using saveVersion 2. Treat every pre-v3 save as legacy so an
                // already purchased Bow Master evolution cannot silently become unequipped.
                int loadedEvolution = hs.activeEvolution;
                // v4 repairs saves written by the first Victoria patch: those saves could
                // preserve Bow Master ownership but accidentally write activeEvolution=0.
                // From v4 onward 0 is a real, intentional unequipped state and is preserved.
                if (d.saveVersion < 5 && loadedEvolution == 0 && h.heroName == "Bow Master")
                {
                    if (hs.evolution2Purchased) loadedEvolution = 2;
                    else if (hs.evolution1Purchased) loadedEvolution = 1;
                }
                h.SetActiveEvolution(loadedEvolution);
                h.victoriaRedEvolution1Purchased = hs.victoriaRedEvolution1Purchased || hs.victoriaRedEvolution2Purchased;
                h.victoriaRedEvolution2Purchased = hs.victoriaRedEvolution2Purchased;
                h.victoriaBlueEvolution1Purchased = hs.victoriaBlueEvolution1Purchased || hs.victoriaBlueEvolution2Purchased;
                h.victoriaBlueEvolution2Purchased = hs.victoriaBlueEvolution2Purchased;
                int loadedVictoriaEvolution = hs.victoriaActiveEvolution;
                // v6 repairs Victoria saves from the broken evolution patch where ownership
                // could survive but activeEvolution was written/read back as 0. For older
                // saves restore the strongest owned evolution. From v6 onward 0 remains an
                // intentional unequipped state.
                if (d.saveVersion < 6 && loadedVictoriaEvolution == 0 && h.heroName == "Victoria")
                {
                    if (hs.victoriaBlueEvolution2Purchased) loadedVictoriaEvolution = 4;
                    else if (hs.victoriaRedEvolution2Purchased) loadedVictoriaEvolution = 2;
                    else if (hs.victoriaRedEvolution1Purchased) loadedVictoriaEvolution = 1;
                    else if (hs.victoriaBlueEvolution1Purchased) loadedVictoriaEvolution = 3;
                }
                h.SetVictoriaActiveEvolution(loadedVictoriaEvolution);
            }

            CityArcherManager archers = FindAnyObjectByType<CityArcherManager>();
            if (archers != null)
            {
                CopySaveToSquad(d.squad1, archers.squad1);
                CopySaveToSquad(d.squad2, archers.squad2);
                archers.ApplyLoadedProgress();
            }

            HeroSlotManager slots = FindAnyObjectByType<HeroSlotManager>();
            if (slots != null) slots.ApplyLoadedProgress();
            if (castle != null) castle.ApplyHeroSlotUnlocks();

            progress.RefreshUI();
        }
        catch (Exception e)
        {
            Debug.LogWarning("Load failed; current scene defaults are kept. " + e.Message);
        }
    }

    static void CopySquadToSave(CityArcherSquad src, SquadSave dst)
    {
        if (src == null || dst == null) return;
        dst.purchased = src.purchased;
        dst.level = src.level;
        dst.archerCount = src.archerCount;
    }

    static void CopySaveToSquad(SquadSave src, CityArcherSquad dst)
    {
        if (src == null || dst == null) return;
        dst.purchased = src.purchased;
        dst.level = Mathf.Max(0, src.level);
        dst.archerCount = Mathf.Clamp(src.archerCount, 0, 10);
    }

    public void DeleteSave()
    {
        try { if (File.Exists(SavePath)) File.Delete(SavePath); }
        catch (Exception e) { Debug.LogWarning("Could not delete save: " + e.Message); }
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) Save();
    }

    void OnApplicationQuit()
    {
        Save();
    }
}
