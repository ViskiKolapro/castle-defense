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
    }

    [Serializable] public class SquadSave
    {
        public bool purchased;
        public int level;
        public int archerCount;
    }

    [Serializable] public class SaveData
    {
        public int saveVersion = 1;
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

        Hero[] heroes = FindObjectsByType<Hero>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
                evolution1Purchased = h.evolution1Purchased
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

            Hero[] heroes = FindObjectsByType<Hero>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Hero h in heroes)
            {
                HeroSave hs = d.heroes.Find(x => x.heroName == h.heroName);
                if (hs == null) continue;
                h.heroLevel = Mathf.Clamp(hs.heroLevel, 1, h.maxHeroLevel);
                h.isPurchased = hs.isPurchased;
                h.installedSlot = hs.isInstalled ? Mathf.Clamp(hs.installedSlot, 1, HeroSlotManager.TotalSlots) : 0;
                h.SetInstalled(hs.isInstalled);
                if (hs.evolution1Purchased) h.ApplyEvolution1();
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
