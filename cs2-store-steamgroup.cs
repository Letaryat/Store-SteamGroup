using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using StoreApi;
using System.Text.Json.Serialization;
using System.Xml;

namespace Store_SteamGroup
{
    public class Store_SteamGroupConfig : BasePluginConfig
    {
        [JsonPropertyName("steamgroupid")]
        public int steamgroupid { get; set; } = 123456;

        [JsonPropertyName("bonus_credits")]
        public int BonusCredits { get; set; } = 100;

        [JsonPropertyName("interval_in_seconds")]
        public int IntervalSeconds { get; set; } = 300;

        [JsonPropertyName("show_ad_message")]
        public bool ShowAdMessage { get; set; } = true;

        [JsonPropertyName("ad_message_delay_seconds")]
        public int AdMessageDelaySeconds { get; set; } = 120;

        [JsonPropertyName("ad_message")]
        public string AdMessage { get; set; } = "Join steam group to gain bonus credits!";
    }

    public class Store_SteamGroup : BasePlugin, IPluginConfig<Store_SteamGroupConfig>
    {
        public override string ModuleName => "Store Module [SteamGroup]";
        public override string ModuleVersion => "0.0.2";
        public override string ModuleAuthor => " --- ";

        private IStoreApi? storeApi;
        private float intervalInSeconds;
        private float steamgroupid;

        //public string steamgroupname;
        HashSet<SteamID> MembersCache = new HashSet<SteamID>();
        public Store_SteamGroupConfig Config { get; set; } = null!;

        private static readonly Dictionary<string, char> ColorMap = new Dictionary<string, char>
        {
            { "{default}", ChatColors.Default },
            { "{white}", ChatColors.White },
            { "{darkred}", ChatColors.DarkRed },
            { "{green}", ChatColors.Green },
            { "{lightyellow}", ChatColors.LightYellow },
            { "{lightblue}", ChatColors.LightBlue },
            { "{olive}", ChatColors.Olive },
            { "{lime}", ChatColors.Lime },
            { "{red}", ChatColors.Red },
            { "{lightpurple}", ChatColors.LightPurple },
            { "{purple}", ChatColors.Purple },
            { "{grey}", ChatColors.Grey },
            { "{yellow}", ChatColors.Yellow },
            { "{gold}", ChatColors.Gold },
            { "{silver}", ChatColors.Silver },
            { "{blue}", ChatColors.Blue },
            { "{darkblue}", ChatColors.DarkBlue },
            { "{bluegrey}", ChatColors.BlueGrey },
            { "{magenta}", ChatColors.Magenta },
            { "{lightred}", ChatColors.LightRed },
            { "{orange}", ChatColors.Orange }
        };

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            storeApi = IStoreApi.Capability.Get();

            if (storeApi == null)
            {
                return;
            }

            intervalInSeconds = Config.IntervalSeconds;
            steamgroupid = Config.steamgroupid;
            
            StartCreditTimer();

            if (Config.ShowAdMessage)
            {
                StartAdMessageTimer();
            }
		    RegisterListener<Listeners.OnMapStart>(OnMapStartHandler);

            if (hotReload)
            {
                OnMapStartHandler(string.Empty);
            }

        }

        public void OnConfigParsed(Store_SteamGroupConfig config)
        {
            Config = config;
        }

        private void StartCreditTimer()
        {
            AddTimer(intervalInSeconds, () =>
            {
                GrantCreditsToEligiblePlayers();
                StartCreditTimer();
            });
        }

        private void GrantCreditsToEligiblePlayers()
        {
            var players = Utilities.GetPlayers();

            foreach (var player in players)
            {
                if (player != null && !player.IsBot && player.IsValid)
                {
                    if(MembersCache.Contains(player.AuthorizedSteamID!)){
                            storeApi?.GivePlayerCredits(player, Config.BonusCredits);
                            player.PrintToChat(Localizer["Prefix"] + Localizer["You have been awarded", Config.BonusCredits]);
                            break;
                    }
                }
            }
        }

	private async Task FetchMembersFromGroupAsync()
	{
		try
		{
			using (HttpClient client = new HttpClient())
			{
                HttpResponseMessage response = await client.GetAsync("https://steamcommunity.com/gid/"+ Config.steamgroupid + "/memberslistxml/?xml=1");
				if (response.IsSuccessStatusCode)
				{
					string groupInfo = await response.Content.ReadAsStringAsync();
					ParseMembers(groupInfo);
				}
				else
				{
					Logger.LogError("[CS2-Store-SteamGroup] Unable to fetch group info!");
				}
			}
		}
		catch (Exception)
		{
			Logger.LogWarning("[CS2-Store-SteamGroup] Unknown error with parsing group info");
		}
	}
    private void OnMapStartHandler(string mapName)
	{
		AddTimer(1.0f, () => _ = FetchMembersFromGroupAsync());
	}
	private void ParseMembers(string groupInfo)
	{
		try
		{
			XmlDocument xmlDoc = new XmlDocument();
			xmlDoc.LoadXml(groupInfo);

			XmlNodeList? SteamIdNodes = xmlDoc.SelectNodes("//members/steamID64");

			if (SteamIdNodes != null)
			{
				MembersCache.Clear();
				foreach (XmlNode node in SteamIdNodes)
				{
					string SteamId64 = node.InnerText;
					if (!string.IsNullOrEmpty(SteamId64) && SteamID.TryParse(SteamId64, out var steamId) && steamId != null)
					{
						if (!MembersCache.Contains(steamId))
							MembersCache.Add(steamId);
					}
				}
			}
		}
		catch (Exception)
		{
			Logger.LogWarning("Unable to parse members from steam group!");
		}
	}
        private void StartAdMessageTimer()
        {
            AddTimer(Config.AdMessageDelaySeconds, () =>
            {
                BroadcastAdMessage();
                StartAdMessageTimer();
            });
        }

        private void BroadcastAdMessage()
        {
            var message = ReplaceColorPlaceholders(Config.AdMessage);
            var players = Utilities.GetPlayers();
            foreach (var player in players)
            {
                if (player != null && !player.IsBot && player.IsValid)
                {
                    player.PrintToChat(Localizer["Prefix"] + message);
                }
            }
        }

        private string ReplaceColorPlaceholders(string message)
        {
            if (!string.IsNullOrEmpty(message) && message[0] != ' ')
            {
                message = " " + message;
            }

            foreach (var colorPlaceholder in ColorMap)
            {
                message = message.Replace(colorPlaceholder.Key, colorPlaceholder.Value.ToString());
            }

            return message;
        }
    }
}
