using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class UpdateChecker : MonoBehaviour
{
    [Header("Version")]
    [Tooltip("Version of this build. Example: 0.1.0")]
    public string currentVersion = "0.1.0";

    [Header("GitHub")]
    public string repositoryOwner = "ViskiKolapro";
    public string repositoryName = "castle-defense";
    [Tooltip("If enabled, checks GitHub automatically when the scene starts.")]
    public bool checkOnStart = true;

    [Header("UI")]
    [Tooltip("Whole update popup. Keep it disabled in the scene.")]
    public GameObject updatePanel;
    public TMP_Text titleText;
    public TMP_Text currentVersionText;
    public TMP_Text newVersionText;
    public Button updateButton;
    public Button laterButton;

    [Header("Text")]
    public string updateTitle = "Доступно обновление";
    public string currentVersionLabel = "Текущая версия";
    public string newVersionLabel = "Новая версия";

    private string downloadPageUrl;
    private bool requestRunning;

    [Serializable]
    private class GitHubRelease
    {
        public string tag_name;
        public string html_url;
        public bool draft;
        public bool prerelease;
    }

    private void Awake()
    {
        if (updatePanel != null)
            updatePanel.SetActive(false);

        if (updateButton != null)
            updateButton.onClick.AddListener(OpenUpdatePage);

        if (laterButton != null)
            laterButton.onClick.AddListener(CloseUpdatePanel);
    }

    private void Start()
    {
        if (checkOnStart)
            CheckForUpdates();
    }

    public void CheckForUpdates()
    {
        if (!requestRunning)
            StartCoroutine(CheckRoutine());
    }

    private IEnumerator CheckRoutine()
    {
        requestRunning = true;

        string apiUrl = $"https://api.github.com/repos/{repositoryOwner}/{repositoryName}/releases/latest";
        using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
        {
            request.timeout = 8;
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");
            request.SetRequestHeader("User-Agent", "CastleDefense-UpdateChecker");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                GitHubRelease release = JsonUtility.FromJson<GitHubRelease>(request.downloadHandler.text);

                if (release != null && !string.IsNullOrWhiteSpace(release.tag_name))
                {
                    string latestVersion = NormalizeVersion(release.tag_name);
                    if (IsNewerVersion(latestVersion, NormalizeVersion(currentVersion)))
                    {
                        downloadPageUrl = !string.IsNullOrWhiteSpace(release.html_url)
                            ? release.html_url
                            : $"https://github.com/{repositoryOwner}/{repositoryName}/releases/latest";

                        ShowUpdatePanel(latestVersion);
                    }
                }
            }
            else
            {
                // No popup on network/GitHub errors. The game must still start normally.
                Debug.Log($"Update check skipped: {request.responseCode} {request.error}");
            }
        }

        requestRunning = false;
    }

    private void ShowUpdatePanel(string latestVersion)
    {
        if (titleText != null)
            titleText.text = updateTitle;

        if (currentVersionText != null)
            currentVersionText.text = $"{currentVersionLabel}: Alpha {NormalizeVersion(currentVersion)}";

        if (newVersionText != null)
            newVersionText.text = $"{newVersionLabel}: Alpha {latestVersion}";

        if (updatePanel != null)
            updatePanel.SetActive(true);
    }

    public void OpenUpdatePage()
    {
        if (string.IsNullOrWhiteSpace(downloadPageUrl))
            downloadPageUrl = $"https://github.com/{repositoryOwner}/{repositoryName}/releases/latest";

        Application.OpenURL(downloadPageUrl);
    }

    public void CloseUpdatePanel()
    {
        if (updatePanel != null)
            updatePanel.SetActive(false);
    }

    private static string NormalizeVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "0.0.0";

        string result = value.Trim();
        result = result.Replace("Alpha", "", StringComparison.OrdinalIgnoreCase).Trim();
        result = result.TrimStart('v', 'V', '-', '_', ' ');

        int separator = result.IndexOfAny(new[] { '-', '+', ' ' });
        if (separator >= 0)
            result = result.Substring(0, separator);

        return result;
    }

    private static bool IsNewerVersion(string candidate, string current)
    {
        int[] a = ParseVersion(candidate);
        int[] b = ParseVersion(current);

        int length = Mathf.Max(a.Length, b.Length);
        for (int i = 0; i < length; i++)
        {
            int av = i < a.Length ? a[i] : 0;
            int bv = i < b.Length ? b[i] : 0;

            if (av > bv) return true;
            if (av < bv) return false;
        }

        return false;
    }

    private static int[] ParseVersion(string version)
    {
        string[] parts = NormalizeVersion(version).Split('.');
        int[] numbers = new int[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            string digits = "";
            foreach (char c in parts[i])
            {
                if (char.IsDigit(c)) digits += c;
                else break;
            }

            numbers[i] = int.TryParse(digits, out int value) ? value : 0;
        }

        return numbers;
    }
}
