namespace KrakenMobile.Services;

public class Game
{
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string TitleId { get; set; } = string.Empty;
    public string ServiceConfigId { get; set; } = string.Empty;
    public int CurrentAchievements { get; set; }
    public int TotalAchievements { get; set; }
    public int Gamerscore { get; set; }
    public int TotalGamerscore { get; set; }
    public double Progress => TotalAchievements > 0 ? (double)CurrentAchievements / TotalAchievements * 100 : 0;
    public bool IsTitleBased { get; set; } = true;
}

public class Achievement
{
    public int Index { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public int Gamerscore { get; set; }
    public bool IsSecret { get; set; }
    public bool IsUnlocked { get; set; }
    public string DateUnlocked { get; set; } = string.Empty;
    public string ProgressState { get; set; } = "Locked";
    public bool IsUnlockable => ProgressState != "Achieved";
    public string RarityCategory { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class XboxApiService
{
    private XboxRestApi? _restApi;
    private string? _xuid;

    public bool IsInitialized => _restApi != null && !string.IsNullOrEmpty(_xuid);

    public XboxApiService()
    {
        RefreshClient();
    }

    public void RefreshClient()
    {
        var token = XboxAuthService.GetXblToken();
        _xuid = XboxAuthService.Xuid;
        if (!string.IsNullOrEmpty(token))
            _restApi = new XboxRestApi(token);
        else
            _restApi = null;
    }

    public async Task<(string gamertag, string xuid, string profilePic, int gamerscore, string bio, string accountTier)> GetProfileAsync()
    {
        if (_restApi == null) return ("Not Logged In", "", "", 0, "", "");
        try
        {
            var basicProfile = await _restApi.GetBasicProfileAsync();
            var gamertag = basicProfile?.ProfileUsers?.FirstOrDefault()?.Settings
                ?.FirstOrDefault(s => s.Id == "Gamertag")?.Value ?? "Unknown";

            var profile = await _restApi.GetProfileAsync(_xuid ?? "");
            var person = profile?.People?.FirstOrDefault();
            var detail = person?.Detail;

            return (
                gamertag,
                _xuid ?? "",
                person?.DisplayPicRaw ?? "",
                int.TryParse(person?.GamerScore, out var gs) ? gs : 0,
                detail?.Bio ?? "",
                detail?.AccountTier ?? ""
            );
        }
        catch
        {
            return ("Error", _xuid ?? "", "", 0, "", "");
        }
    }

    public async Task<List<Game>> GetGamesListAsync(string? overrideXuid = null)
    {
        if (_restApi == null) return new List<Game>();
        var targetXuid = overrideXuid ?? _xuid;
        if (string.IsNullOrEmpty(targetXuid)) return new List<Game>();

        try
        {
            var titlesList = await _restApi.GetGamesListAsync(targetXuid);
            if (titlesList?.Titles == null) return new List<Game>();

            var games = new List<Game>();
            int index = 0;
            foreach (var title in titlesList.Titles.Where(t => t.Achievement != null && t.Achievement.TotalAchievements > 0))
            {
                games.Add(new Game
                {
                    Index = index++,
                    Title = title.Name ?? "Unknown",
                    Image = title.DisplayImage ?? "",
                    TitleId = title.TitleId ?? "",
                    ServiceConfigId = title.ServiceConfigId ?? "",
                    CurrentAchievements = title.Achievement?.CurrentAchievements ?? 0,
                    TotalAchievements = title.Achievement?.TotalAchievements ?? 0,
                    Gamerscore = title.Achievement?.CurrentGamerscore ?? 0,
                    TotalGamerscore = title.Achievement?.TotalGamerscore ?? 0,
                    IsTitleBased = !string.IsNullOrEmpty(title.ServiceConfigId)
                });
            }
            return games;
        }
        catch
        {
            return new List<Game>();
        }
    }

    public async Task<List<Achievement>> GetAchievementsAsync(string xuid, string titleId)
    {
        if (_restApi == null) return new List<Achievement>();
        try
        {
            var response = await _restApi.GetAchievementsForTitleAsync(xuid, titleId);
            if (response?.achievements == null) return new List<Achievement>();

            var achievements = new List<Achievement>();
            int index = 0;
            foreach (var a in response.achievements)
            {
                var isUnlocked = a.progressState == "Achieved";
                var gs = 0;
                if (a.rewards != null)
                {
                    var gsReward = a.rewards.FirstOrDefault(r => r.valueType == "Gamerscore");
                    if (gsReward != null) int.TryParse(gsReward.value, out gs);
                }

                achievements.Add(new Achievement
                {
                    Index = index++,
                    Id = a.id,
                    Name = a.name ?? "",
                    Description = a.description ?? a.lockedDescription ?? "",
                    Image = a.mediaAssets?.FirstOrDefault()?.url ?? "",
                    Gamerscore = gs,
                    IsSecret = a.isSecret,
                    IsUnlocked = isUnlocked,
                    DateUnlocked = a.progression?.timeUnlocked ?? "",
                    ProgressState = a.progressState ?? "Locked",
                    RarityCategory = a.rarity?.currentCategory ?? "",
                    Category = a.achievementType ?? ""
                });
            }
            return achievements;
        }
        catch
        {
            return new List<Achievement>();
        }
    }

    public async Task<bool> UnlockTitleBasedAchievementAsync(string serviceConfigId, string titleId, string xuid, string achievementId)
    {
        if (_restApi == null) return false;
        try
        {
            await _restApi.UnlockTitleBasedAchievementAsync(serviceConfigId, titleId, xuid, achievementId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<int> SendHeartbeatAsync(string xuid, string spoofedTitleId)
    {
        if (_restApi == null) return 0;
        try
        {
            return await _restApi.SendHeartbeatAsync(xuid, spoofedTitleId) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }
}
