public enum GameLaunchMode
{
    Story,
    CreateMatch,
    JoinMatch
}

public static class GameLaunchConfig
{
    public static GameLaunchMode CurrentMode { get; private set; } = GameLaunchMode.Story;
    public static string RoomCode { get; private set; } = "";
    public static int StoryChapter { get; private set; } = 1;
    public static string PendingMenuStatus { get; private set; } = "";

    public static bool IsOnlineMode
    {
        get { return CurrentMode == GameLaunchMode.CreateMatch || CurrentMode == GameLaunchMode.JoinMatch; }
    }

    public static void ConfigureStory(int storyChapter = 1)
    {
        CurrentMode = GameLaunchMode.Story;
        RoomCode = "";
        StoryChapter = storyChapter < 1 ? 1 : storyChapter;
        PendingMenuStatus = "";
    }

    public static void ConfigureCreateMatch(string roomCode)
    {
        CurrentMode = GameLaunchMode.CreateMatch;
        RoomCode = roomCode ?? "";
        StoryChapter = 1;
        PendingMenuStatus = "";
    }

    public static void ConfigureJoinMatch(string roomCode)
    {
        CurrentMode = GameLaunchMode.JoinMatch;
        RoomCode = roomCode ?? "";
        StoryChapter = 1;
        PendingMenuStatus = "";
    }

    public static void SetPendingMenuStatus(string message)
    {
        PendingMenuStatus = message ?? "";
    }

    public static string ConsumePendingMenuStatus()
    {
        string current = PendingMenuStatus;
        PendingMenuStatus = "";
        return current;
    }
}
